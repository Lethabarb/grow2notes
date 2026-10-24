import { onlineManager, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../api/api.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import type { Draft } from './noteQueries.ts';
import type { SaveIndicator } from './SaveIndicator.tsx';
import { capped, sentContent, useAutosave, type DeadEnd, type DraftContent } from './useAutosave.ts';

describe('capped', () => {
  it('leaves a text of the limit or shorter as it is', () => {
    expect(capped('Jane made toast.', 16)).toBe('Jane made toast.');
    expect(capped('', 16)).toBe('');
  });

  it('cuts a longer text to the limit, counted as UTF-16 code units, as .NET counts a string', () => {
    // "é" written as e and a combining accent is two code units, as the server counts it too.
    expect(capped('Zoé went out', 4)).toBe('Zoé');
    expect(capped('x'.repeat(20_050), 20_000)).toHaveLength(20_000);
  });

  it('stops short of a surrogate pair that the limit would split, rather than send half a character', () => {
    // "😀" is a surrogate pair, two code units, the 4th and 5th here.
    expect(capped('abc😀d', 4)).toBe('abc');
    expect(capped('abc😀d', 5)).toBe('abc😀');
  });
});

describe('sentContent', () => {
  const content = {
    narrative: 'Jane made toast.',
    tickedGoalIds: ['g1'],
    pickedGroupIds: ['p1'],
    tickedCommonItemIds: ['c1'],
    isFlagged: true,
    flagReason: 'Unwell.',
  };

  it('sends the text, the ticks, the picks, the flag and the reason, as autosave and Submit alike send them', () => {
    expect(sentContent(content)).toEqual(content);
  });

  it('sends no reason while the box is unticked, or while the reason is blank', () => {
    expect(sentContent({ ...content, isFlagged: false })).toEqual({ ...content, isFlagged: false, flagReason: null });
    expect(sentContent({ ...content, flagReason: '   ' })).toEqual({ ...content, flagReason: null });
  });

  it('cuts the text and the reason to their limits', () => {
    const sent = sentContent({ ...content, narrative: 'x'.repeat(20_050), flagReason: 'y'.repeat(210) });

    expect(sent.narrative).toBe('x'.repeat(20_000));
    expect(sent.flagReason).toBe('y'.repeat(200));
  });
});

// The engine alone, under a page of its own (autosave-status.md *Behaviour* 1, 4 and 6; T10's Notes): what the note
// form shows of it is in NotePage.test.tsx.

const antiforgeryPath = '/api/auth/antiforgery';

// One of the calls that sign someone in, after whose answer `api()` holds no antiforgery token.
const signInPath = '/api/auth/passkey';

/** A new note's template, as `GET {base}/draft` gives it, with no goals, whose first save sends its lists' version. */
const template: Draft = {
  baseVersion: 0,
  isSaved: false,
  listsVersion: 'lists-1',
  guidePrompts: '',
  goals: [],
  commonItemGroups: [],
  narrative: '',
  isFlagged: false,
  flagReason: null,
};

/** The worker's own draft, last saved at 9:40 am, whose saves send no lists' version. */
const reopened: Draft = {
  baseVersion: 0,
  isSaved: true,
  guidePrompts: '',
  goals: [],
  commonItemGroups: [],
  narrative: '',
  isFlagged: false,
  flagReason: null,
  savedAtUtc: '2026-09-30T23:40:00Z',
};

/** A submitted note's pending edit, based on version 2, as `GET {base}/draft` gives it before any save. */
const edit: Draft = { ...template, baseVersion: 2, listsVersion: undefined };

/** The same pending edit reopened, last saved at 9:40 am. */
const reopenedEdit: Draft = { ...edit, isSaved: true, savedAtUtc: '2026-09-30T23:40:00Z' };

/** How the server meets a save: an answer, or a failure as `fetch` has one. */
type Answer = () => Promise<Response>;

/** A save's answer: when it was saved, at 9:42 am unless given another time. */
function saved(savedAtUtc = '2026-09-30T23:42:00Z'): Answer {
  const body = JSON.stringify({ savedAtUtc });
  return () => Promise.resolve(new Response(body, { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its `code`, if any, and its headers. */
function problem(status: number, code?: string, headers: Record<string, string> = {}): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, code });
  return () =>
    Promise.resolve(
      new Response(body, { status, headers: { 'Content-Type': 'application/problem+json', ...headers } }),
    );
}

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** `answer`, held back until `release()` is called. */
function held(answer: Answer): { answer: Answer; release: () => void } {
  let release = () => {};
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { answer: () => released.then(answer), release };
}

const fetchMock = vi.fn<typeof fetch>();

/** A save as it was sent: when, by the fake clock, its body, its size in bytes as UTF-8, and its `keepalive`. */
type SentSave = { at: number; body: Record<string, unknown>; bytes: number; keepalive: boolean | undefined };

/** Every save sent, in order. */
let sentSaves: SentSave[] = [];

/** How the server answers a read of the draft, for Load the other version. */
let draftRead: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** How the server answers the saves still to come, in turn, the last one's answer given again once all have been. */
let saveAnswers: Answer[] = [];

/** Answers each save from now on with the next of `answers`, and the last one's again once all have been given. */
function answerSaves(...answers: Answer[]) {
  saveAnswers = answers;
}

/** The waits between one save sent and the next, in milliseconds. */
function gaps(): number[] {
  return sentSaves.slice(1).map(({ at }, index) => at - sentSaves[index].at);
}

/** Each save's number and text. */
function sentTexts(): [unknown, unknown][] {
  return sentSaves.map(({ body }) => [body.seq, body.narrative]);
}

/**
 * Renders autosave for a note of its own, whose numbering no other test's has reached, opened as `opened`, under a
 * query client of its own, made as `main.tsx` makes the app's: what its indicator says, in turn, and `type()`, which
 * puts a text in the form as one input, a change.
 */
function renderAutosave(opened: Draft = template, overrides: Partial<DraftContent> = {}) {
  const client = createQueryClient();
  const participantId = crypto.randomUUID();
  const content: DraftContent = {
    narrative: '',
    tickedGoalIds: [],
    pickedGroupIds: [],
    tickedCommonItemIds: [],
    isFlagged: false,
    flagReason: '',
    ...overrides,
  };
  const said: SaveIndicator['kind'][] = [];
  const deadEnds: DeadEnd[] = [];
  const others: Draft[] = [];
  const { result } = renderHook(
    () =>
      useAutosave({
        participantId,
        noteDate: '2026-10-01',
        opened,
        read: () => ({ ...content }),
        onIndicator: (indicator) => {
          said.push(indicator.kind);
        },
        onListsChanged: () => {},
        onOtherVersion: (other) => {
          others.push(other);
        },
        onDeadEnd: (deadEnd) => {
          deadEnds.push(deadEnd);
        },
      }),
    {
      wrapper: ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    },
  );
  return {
    autosave: () => result.current,
    said,
    deadEnds,
    others,
    type: (narrative: string) => {
      content.narrative = narrative;
      act(() => {
        result.current.changed();
      });
    },
  };
}

/** What `renderAutosave()` gives. */
type Rendered = ReturnType<typeof renderAutosave>;

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** How many times the antiforgery token has been asked for. */
function tokenRequests(): number {
  return fetchMock.mock.calls.filter(([path]) => path === antiforgeryPath).length;
}

/** A sign-in answered, as the signed-out-in-place view's, after which `api()` holds no antiforgery token. */
async function signIn() {
  await api(signInPath, { method: 'POST', body: {} });
}

/** Makes the page hidden or visible, with the event that the browser fires. */
function showPage(state: DocumentVisibilityState) {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(state);
  act(() => {
    document.dispatchEvent(new Event('visibilitychange'));
  });
}

/** The browser going offline or online, with the event that it fires. */
function goOnline(online: boolean) {
  act(() => {
    window.dispatchEvent(new Event(online ? 'online' : 'offline'));
  });
}

const pending = Symbol('pending');

/** Reads what `promise` gave, once the clock has moved on; `pending` until it settles. */
function outcome<T>(promise: Promise<T>): () => T | typeof pending {
  let value: T | typeof pending = pending;
  void promise.then((given) => {
    value = given;
  });
  return () => value;
}

/**
 * A text whose save, the second of a reopened draft, is a body of `bytes` bytes as UTF-8, its characters under the
 * text's limit: "€" is one character and three bytes.
 */
function textOf(bytes: number): string {
  const empty = {
    clientId: crypto.randomUUID(),
    seq: 2,
    narrative: '',
    tickedGoalIds: [],
    pickedGroupIds: [],
    tickedCommonItemIds: [],
    isFlagged: false,
    baseVersion: 0,
  };
  const rest = bytes - new TextEncoder().encode(JSON.stringify({ ...empty, flagReason: null })).length;
  return '€'.repeat(Math.floor(rest / 3)) + 'x'.repeat(rest % 3);
}

beforeEach(() => {
  vi.useFakeTimers();
  sentSaves = [];
  answerSaves(saved());
  fetchMock.mockImplementation((path, init) => {
    if (path === antiforgeryPath) {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }));
    }

    if (path === signInPath) {
      return Promise.resolve(new Response('{}', { headers: { 'Content-Type': 'application/json' } }));
    }

    if (init?.method === 'GET') {
      return draftRead();
    }

    if (init?.method !== 'PUT') {
      throw new Error(`The test gave no answer for ${init?.method ?? 'GET'} ${path as string}.`);
    }

    const json = init.body as string;
    sentSaves.push({
      at: Date.now(),
      body: JSON.parse(json) as Record<string, unknown>,
      bytes: new TextEncoder().encode(json).length,
      keepalive: init.keepalive,
    });
    const answer = saveAnswers.length > 1 ? saveAnswers.shift()! : saveAnswers[0];
    return answer();
  });
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  onlineManager.setOnline(true);
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('A save', () => {
  const picks = { tickedGoalIds: ['g1'], pickedGroupIds: ['p1', 'p2'], tickedCommonItemIds: ['c1'] };

  it('carries the picks and the ticks of common items, as it carries the goals, when the text is saved', async () => {
    const { type } = renderAutosave(template, picks);
    type('Jane');
    await wait(5_000);

    expect(sentSaves).toHaveLength(1);
    expect(sentSaves[0].body).toMatchObject(picks);
  });

  it('carries them as the page is hidden too', async () => {
    const { type } = renderAutosave(reopened, picks);
    type('Jane');
    showPage('hidden');
    await wait(0);

    expect(sentSaves[0]).toMatchObject({ keepalive: true, body: picks });
  });
});

describe('Typing steadily', () => {
  // design.md §9.9 allows 300 requests a minute for each user, autosave's included.
  it("for five minutes sends at most 30 saves a minute, far under the server's 300", async () => {
    const { type } = renderAutosave();
    const start = Date.now();

    for (let second = 1; second <= 300; second++) {
      type('x'.repeat(second));
      await wait(1_000);
    }

    for (let minute = 0; minute < 5; minute++) {
      const saves = sentSaves.filter(({ at }) => Math.floor((at - start) / 60_000) === minute);
      expect(saves.length).toBeGreaterThanOrEqual(1);
      expect(saves.length).toBeLessThanOrEqual(30);
    }
  });
});

describe('A pending edit', () => {
  /** A read of the draft that answers `draft`, as the server does. */
  function serveDraft(draft: Draft) {
    const body = JSON.stringify(draft);
    draftRead = () => Promise.resolve(new Response(body, { headers: { 'Content-Type': 'application/json' } }));
  }

  /** Saves "Jane", then meets the conflict of a newer version with "Jane made toast."; later saves are answered. */
  async function meetTheConflict() {
    answerSaves(saved(), problem(409, 'note.version_conflict'), saved('2026-09-30T23:44:00Z'));
    const rendered = renderAutosave(reopenedEdit);
    for (const text of ['Jane', 'Jane made toast.']) {
      rendered.type(text);
      act(() => {
        rendered.autosave().flush();
      });
      await wait(0);
    }

    return rendered;
  }

  it('sends its base version in every save, a page-hidden one too, and a draft sends 0', async () => {
    const pendingEdit = renderAutosave(reopenedEdit);
    pendingEdit.type('Jane');
    await wait(2_000);
    pendingEdit.type('Jane made toast.');
    showPage('hidden');
    await wait(0);
    showPage('visible');
    const draft = renderAutosave(reopened);
    draft.type('Jane');
    await wait(2_000);

    expect(sentSaves.map(({ body }) => body.baseVersion)).toEqual([2, 2, 0]);
  });

  it('opens a reopened pending edit as saved, never as a draft, and a new one with nothing said', () => {
    expect(renderAutosave(reopenedEdit).said).toEqual(['saved']);
    expect(renderAutosave(edit).said).toEqual(['none']);
    expect(renderAutosave(reopened).said).toEqual(['draft']);
  });

  it('stops at a 403 note.not_editable, a dead end, with "Not saved" and no more saves', async () => {
    answerSaves(problem(403, 'note.not_editable'));
    const { type, said, deadEnds } = renderAutosave(reopenedEdit);
    type('Jane');
    await wait(2_000);
    type('Jane made toast.');
    await wait(60_000);

    expect(sentSaves).toHaveLength(1);
    expect(deadEnds).toEqual([{ code: 'note.not_editable' }]);
    expect(said.at(-1)).toBe('notSaved');
  });

  it('stops at a 409 note.version_conflict until startAgain(), which sends at once with the new base', async () => {
    const { autosave, type, said, deadEnds } = await meetTheConflict();
    expect(deadEnds).toEqual([{ code: 'note.version_conflict' }]);
    expect(said.at(-1)).toBe('notSaved');
    type('Jane made toast. Then a walk.');
    await wait(30_000);
    expect(sentSaves).toHaveLength(2);

    act(() => {
      autosave().startAgain(3);
    });
    expect(said.at(-1)).toBe('saving');
    await wait(0);

    expect(sentSaves).toHaveLength(3);
    expect(sentSaves[2].body).toMatchObject({
      baseVersion: 3,
      clientId: sentSaves[0].body.clientId,
      seq: 3,
      narrative: 'Jane made toast. Then a walk.',
    });
    expect(said.at(-1)).toBe('saved');

    type('Jane made toast. Then a walk to the shops.');
    await wait(2_000);
    expect(sentSaves[3].body).toMatchObject({ baseVersion: 3, seq: 4 });
  });

  it('sends what the form holds on startAgain() even with no change made since the conflict', async () => {
    const { autosave } = await meetTheConflict();

    act(() => {
      autosave().startAgain(4);
    });
    await wait(0);

    expect(sentSaves[2].body).toMatchObject({ baseVersion: 4, seq: 3, narrative: 'Jane made toast.' });
  });

  describe('Load the other version', () => {
    /** Saves "Jane", then meets the takeover with "Jane made toast.", on a page opened as `opened`. */
    async function loseTheNote(opened: Draft) {
      answerSaves(saved(), problem(409, 'draft.taken_over'), saved());
      const rendered = renderAutosave(opened);
      for (const text of ['Jane', 'Jane made toast.']) {
        rendered.type(text);
        act(() => {
          rendered.autosave().flush();
        });
        await wait(0);
      }

      return rendered;
    }

    it('is the version-conflict dead end on a draft page whose read is a pending edit', async () => {
      const { autosave, deadEnds, others } = await loseTheNote(reopened);

      serveDraft(reopenedEdit);
      const loaded = outcome(autosave().load());
      await wait(0);

      expect(loaded()).toBe('deadEnd');
      expect(deadEnds).toEqual([{ code: 'note.version_conflict' }]);
      expect(others).toEqual([]);
    });

    it('adopts the base of the answer on an edit page, and the saves after it send it', async () => {
      const { autosave, type, said, others } = await loseTheNote(reopenedEdit);

      serveDraft({ ...reopenedEdit, baseVersion: 3, narrative: 'Sam made tea.' });
      const loaded = outcome(autosave().load());
      await wait(0);
      type('Sam made tea. Then a nap.');
      await wait(2_000);

      expect(loaded()).toBe('shown');
      expect(others.map((other) => other.baseVersion)).toEqual([3]);
      expect(said.at(-1)).toBe('saved');
      expect(sentSaves.at(-1)?.body).toMatchObject({ baseVersion: 3, seq: 1 });
    });
  });
});

describe('A save that fails', () => {
  it('goes again after 1, 2, 4 and 8 s, then every 10 s, each attempt under the next number', async () => {
    answerSaves(noConnection);
    const { autosave, type } = renderAutosave();
    type('Jane');
    act(() => {
      autosave().flush();
    });

    await wait(44_999);

    expect(gaps()).toEqual([1_000, 2_000, 4_000, 8_000, 10_000, 10_000]);
    expect(sentSaves.map(({ body }) => body.seq)).toEqual([1, 2, 3, 4, 5, 6, 7]);
    expect(new Set(sentSaves.map(({ body }) => body.clientId)).size).toBe(1);
  });

  it.each([408, 429, 500, 502, 503, 504])('goes again after 1 s when it fails with a %i', async (status) => {
    answerSaves(problem(status), saved());
    const { autosave, type, said } = renderAutosave();
    type('Jane');
    act(() => {
      autosave().flush();
    });

    await wait(30_000);

    expect(gaps()).toEqual([1_000]);
    expect(said).toEqual(['none', 'saving', 'saved']);
  });

  it.each([429, 503])(
    "goes again after a %i's Retry-After of 3 s, in place of the back-off's 1 s, \"Not saved\" meanwhile",
    async (status) => {
      answerSaves(problem(status, undefined, { 'Retry-After': '3' }), saved());
      const { autosave, type, said } = renderAutosave();
      type('Jane');
      act(() => {
        autosave().flush();
      });

      await wait(2_999);
      expect(sentSaves).toHaveLength(1);
      expect(said).toEqual(['none', 'saving', 'failed']);

      await wait(1);
      expect(gaps()).toEqual([3_000]);
      expect(said).toEqual(['none', 'saving', 'failed', 'saved']);
    },
  );

  it.each([401, 403, 409, 412, 422])('is sent once, never again, when refused with a %i', async (status) => {
    answerSaves(problem(status));
    const { autosave, type, said } = renderAutosave();
    type('Jane');
    act(() => {
      autosave().flush();
    });

    await wait(60_000);

    expect(sentSaves).toHaveLength(1);
    expect(said).toEqual(['none', 'saving']);
  });

  it('goes again with the text typed meanwhile, which a blur before the retry does not send by itself', async () => {
    answerSaves(noConnection, saved());
    const { autosave, type, said } = renderAutosave();
    type('Jane');
    act(() => {
      autosave().flush();
    });
    await wait(500);

    type('Jane made toast.');
    act(() => {
      autosave().flush();
    });
    await wait(499);
    expect(sentSaves).toHaveLength(1);

    await wait(1);
    await wait(30_000);
    expect(sentTexts()).toEqual([
      [1, 'Jane'],
      [2, 'Jane made toast.'],
    ]);
    expect(gaps()).toEqual([1_000]);
    expect(said).toEqual(['none', 'saving', 'saved']);
  });

  it.each([
    {
      act: 'a change',
      text: 'Jane made toast.',
      resume: ({ type }: Rendered) => {
        type('Jane made toast.');
      },
    },
    {
      act: 'a blur',
      text: 'Jane',
      resume: ({ autosave }: Rendered) => {
        act(() => {
          autosave().flush();
        });
      },
    },
  ])(
    'stops 28 minutes after the person last acted on the form, "Not saved" kept, and goes on at $act',
    async ({ text, resume }) => {
      answerSaves(problem(500));
      const rendered = renderAutosave();
      const start = Date.now();
      rendered.type('Jane');
      act(() => {
        rendered.autosave().flush();
      });

      // Every 10 s from the fifth retry on, the last of them sent less than 28 minutes after the change.
      await wait(60 * 60_000);
      const sent = sentSaves.length;
      expect(sentSaves.at(-1)!.at - start).toBeLessThan(28 * 60_000);
      expect(sentSaves.at(-1)!.at - start).toBeGreaterThanOrEqual(28 * 60_000 - 10_000);
      expect(rendered.said.at(-1)).toBe('failed');
      expect(rendered.autosave().unsaved()).toBe(true);

      resume(rendered);
      await wait(2_000);
      expect(sentSaves).toHaveLength(sent + 1);
      expect(sentSaves.at(-1)!.body.narrative).toBe(text);
      await wait(10_000);
      expect(sentSaves).toHaveLength(sent + 2);
    },
  );

  it('waits while the browser is offline, "Not saved" at once, then goes with the newest text online', async () => {
    const { type, said } = renderAutosave();
    type('Jane');

    goOnline(false);
    expect(said).toEqual(['none', 'saving', 'failed']);
    await wait(30_000);
    type('Jane made toast.');
    await wait(30_000);
    expect(sentSaves).toEqual([]);

    goOnline(true);
    await wait(0);

    expect(sentTexts()).toEqual([[1, 'Jane made toast.']]);
    expect(said).toEqual(['none', 'saving', 'failed', 'saved']);
  });
});

describe('The page hidden and shown again', () => {
  it.each([
    { bytes: 59_999, keepalive: true },
    { bytes: 60_000, keepalive: false },
  ])(
    'sends the newest state at once as the page is hidden, beside the save out, keepalive $keepalive at $bytes bytes',
    async ({ bytes, keepalive }) => {
      const first = held(saved('2026-09-30T23:42:00Z'));
      answerSaves(first.answer, saved('2026-09-30T23:43:00Z'));
      const { autosave, type, said } = renderAutosave(reopened);
      type('Jane');
      act(() => {
        autosave().flush();
      });
      await wait(0);
      const text = textOf(bytes);
      type(text);

      showPage('hidden');
      await wait(0);

      expect(sentSaves.map((save) => [save.body.seq, save.keepalive])).toEqual([
        [1, undefined],
        [2, keepalive],
      ]);
      expect(sentSaves[1].body.narrative).toBe(text);
      expect(sentSaves[1].bytes).toBe(bytes);
      // Its answer counts as any save's.
      expect(said).toEqual(['draft', 'saving', 'saved']);

      // The older save's answer, after it, leaves "Saved" at the newer time.
      first.release();
      await wait(30_000);
      expect(sentSaves).toHaveLength(2);
      expect(said).toEqual(['draft', 'saving', 'saved']);
    },
  );

  it('sends the newest state at once as the browser comes online while the page is hidden', async () => {
    answerSaves(noConnection);
    const { type, said } = renderAutosave();
    type('Jane');
    goOnline(false);
    showPage('hidden');
    await wait(30_000);
    expect(sentSaves.map(({ keepalive }) => keepalive)).toEqual([true]);

    answerSaves(saved());
    goOnline(true);
    await wait(0);

    expect(sentSaves.map((save) => [save.body.seq, save.body.narrative, save.keepalive])).toEqual([
      [1, 'Jane', true],
      [2, 'Jane', true],
    ]);
    expect(said).toEqual(['none', 'saving', 'failed', 'saved']);
  });

  it('fetches the antiforgery token as the form opens, so that the page-hidden save is one request', async () => {
    // No token held, as on the first page load or after a sign-in.
    await signIn();
    const beforeOpening = tokenRequests();
    const { type } = renderAutosave(reopened);
    await wait(0);
    expect(tokenRequests()).toBe(beforeOpening + 1);

    type('Jane');
    showPage('hidden');
    await wait(0);

    expect(tokenRequests()).toBe(beforeOpening + 1);
    expect(sentSaves.map(({ body, keepalive }) => [body.seq, keepalive])).toEqual([[1, true]]);
  });

  it('sends nothing as the page is hidden with no change unsaved', async () => {
    const { autosave, type } = renderAutosave();
    showPage('hidden');
    showPage('visible');
    await wait(0);
    expect(sentSaves).toEqual([]);

    type('Jane');
    act(() => {
      autosave().flush();
    });
    await wait(0);
    showPage('hidden');
    await wait(0);

    expect(sentSaves).toHaveLength(1);
  });

  it('sends an unsaved change at once as the page is shown again, cutting the wait for a retry short', async () => {
    answerSaves(noConnection, noConnection, noConnection, saved());
    const { autosave, type, said } = renderAutosave();
    type('Jane');
    act(() => {
      autosave().flush();
    });
    await wait(1_000);
    showPage('hidden');
    await wait(500);

    showPage('visible');
    await wait(0);

    expect(sentSaves.map(({ at, keepalive }) => [at - sentSaves[0].at, keepalive])).toEqual([
      [0, undefined],
      [1_000, undefined],
      [1_000, true],
      [1_500, undefined],
    ]);
    expect(said.at(-1)).toBe('saved');
    await wait(30_000);
    expect(sentSaves).toHaveLength(4);
  });

  it('sends nothing from stop() to resume(), hidden or shown, then the change made meanwhile at once', async () => {
    const { autosave, type } = renderAutosave();
    type('Jane');
    await act(() => autosave().stop());

    showPage('hidden');
    showPage('visible');
    await wait(30_000);
    expect(sentSaves).toEqual([]);

    act(() => {
      autosave().resume();
    });
    await wait(0);
    expect(sentTexts()).toEqual([[1, 'Jane']]);
  });

  it('settles stop() only once a page-hidden save out has been answered', async () => {
    const hidden = held(saved());
    answerSaves(hidden.answer);
    const { autosave, type } = renderAutosave();
    type('Jane');
    showPage('hidden');
    await wait(0);
    expect(sentSaves.map(({ keepalive }) => keepalive)).toEqual([true]);

    const stopped = outcome(autosave().stop());
    await wait(30_000);
    expect(stopped()).toBe(pending);

    hidden.release();
    await wait(0);
    expect(stopped()).toBe(true);
    expect(sentSaves).toHaveLength(1);
  });

  // So that Discard sends nothing once the note at its address may no longer be this draft (T12's Notes).
  it('gives false from stop() when the save out meets a dead end, and true for one that fails', async () => {
    const conflict = held(problem(409, 'note.version_conflict'));
    answerSaves(conflict.answer);
    const ended = renderAutosave();
    ended.type('Jane');
    await wait(2_000);
    const stoppedAtTheDeadEnd = outcome(ended.autosave().stop());

    conflict.release();
    await wait(0);
    expect(stoppedAtTheDeadEnd()).toBe(false);
    expect(ended.said.at(-1)).toBe('notSaved');

    answerSaves(noConnection);
    const failing = renderAutosave();
    failing.type('Jane');
    await wait(2_000);
    const stoppedAfterAFailure = outcome(failing.autosave().stop());
    await wait(30_000);
    expect(stoppedAfterAFailure()).toBe(true);
    expect(sentSaves).toHaveLength(2);
  });

  it('sends nothing after end(), hidden or shown', async () => {
    const { autosave, type } = renderAutosave();
    type('Jane');
    act(() => {
      autosave().end({ code: 'note.version_conflict' });
    });

    showPage('hidden');
    showPage('visible');
    await wait(30_000);

    expect(sentSaves).toEqual([]);
  });
});

describe('flushNow() and checkSave()', () => {
  it('flushNow() gives true at once with nothing unsaved, else once the change it sends now is saved', async () => {
    const save = held(saved());
    answerSaves(save.answer);
    const { autosave, type } = renderAutosave();
    const unchanged = outcome(autosave().flushNow());
    await wait(0);
    expect(unchanged()).toBe(true);
    expect(sentSaves).toEqual([]);

    type('Jane');
    const flushed = outcome(autosave().flushNow());
    await wait(0);
    expect(sentSaves).toHaveLength(1);
    expect(flushed()).toBe(pending);

    save.release();
    await wait(0);
    expect(flushed()).toBe(true);
  });

  it('flushNow() gives true after a dead end, sending nothing', async () => {
    const { autosave, type } = renderAutosave();
    type('Jane');
    act(() => {
      autosave().end({ code: 'note.participant_archived' });
    });

    const flushed = outcome(autosave().flushNow());
    await wait(0);

    expect(flushed()).toBe(true);
    expect(sentSaves).toEqual([]);
  });

  it('flushNow() and checkSave() give false and nothing as "Not saved" shows, while the retries go on', async () => {
    answerSaves(noConnection);
    const { autosave, type, said } = renderAutosave();
    type('Jane');

    const flushed = outcome(autosave().flushNow());
    await wait(0);
    expect(flushed()).toBe(pending);
    await wait(1_000);
    expect(said.at(-1)).toBe('failed');
    expect(flushed()).toBe(false);

    const checked = outcome(autosave().checkSave());
    await wait(0);
    expect(checked()).toBeUndefined();
    expect(sentSaves).toHaveLength(2);

    answerSaves(saved());
    await wait(2_000);
    expect(sentSaves).toHaveLength(3);
    expect(said.at(-1)).toBe('saved');
  });

  it('checkSave() and flushNow() give nothing and false at once on a Retry-After longer than 1 s', async () => {
    answerSaves(problem(503, undefined, { 'Retry-After': '30' }), saved());
    const { autosave, type, said } = renderAutosave();
    type('Jane');

    const checked = outcome(autosave().checkSave());
    await wait(0);
    expect(checked()).toBeUndefined();
    expect(said.at(-1)).toBe('failed');

    type('Jane made toast.');
    const flushed = outcome(autosave().flushNow());
    await wait(0);
    expect(flushed()).toBe(false);

    await wait(29_999);
    expect(sentSaves).toHaveLength(1);
    await wait(1);
    expect(sentTexts()).toEqual([
      [1, 'Jane'],
      [2, 'Jane made toast.'],
    ]);
    expect(said.at(-1)).toBe('saved');
  });

  it.each([403, 422])('flushNow() gives false, and checkSave() nothing, on a %i', async (status) => {
    answerSaves(problem(status));
    const { autosave, type } = renderAutosave();
    type('Jane');

    const flushed = outcome(autosave().flushNow());
    await wait(0);
    expect(flushed()).toBe(false);

    const checked = outcome(autosave().checkSave());
    await wait(0);
    expect(checked()).toBeUndefined();
    expect(sentSaves).toHaveLength(2);
    await wait(60_000);
    expect(sentSaves).toHaveLength(2);
  });
});

// What a leave asks before it goes (autosave-status.md *Behaviour* 7).
describe('unsaved()', () => {
  it('is false with nothing changed, then true from a change until a save carrying it is answered', async () => {
    const save = held(saved());
    answerSaves(save.answer);
    const { autosave, type } = renderAutosave();
    expect(autosave().unsaved()).toBe(false);

    type('Jane');
    expect(autosave().unsaved()).toBe(true);
    await wait(2_000);
    expect(sentSaves).toHaveLength(1);
    expect(autosave().unsaved()).toBe(true);

    save.release();
    await wait(0);
    expect(autosave().unsaved()).toBe(false);
  });

  it('is false after a dead end that a save met, though its change is never saved', async () => {
    answerSaves(problem(422, 'note.participant_archived'));
    const { autosave, type } = renderAutosave();
    type('Jane');
    await wait(2_000);
    type('Jane made toast.');

    expect(sentSaves).toHaveLength(1);
    expect(autosave().unsaved()).toBe(false);
  });

  it('is false after a dead end that Submit met, with a change unsaved', async () => {
    const { autosave, type } = renderAutosave();
    type('Jane');
    act(() => {
      autosave().end({ code: 'note.version_conflict' });
    });
    await wait(30_000);

    expect(sentSaves).toEqual([]);
    expect(autosave().unsaved()).toBe(false);
  });
});

describe('Taken over by another page', () => {
  /** A save refused as another page has saved the note since this page last did (design.md §5.6). */
  const takenOver = problem(409, 'draft.taken_over');

  /** Saves "Jane", then "Jane made toast.", which meets the takeover; the saves after it are answered at 9:44 am. */
  async function loseTheNote() {
    answerSaves(saved(), takenOver, saved('2026-09-30T23:44:00Z'));
    const rendered = renderAutosave(reopened);
    for (const text of ['Jane', 'Jane made toast.']) {
      rendered.type(text);
      act(() => {
        rendered.autosave().flush();
      });
      await wait(0);
    }

    return rendered;
  }

  it('pauses at a 409 draft.taken_over with "Not saved", keeping each change made after it, sending none', async () => {
    const { autosave, type, said } = await loseTheNote();
    expect(said).toEqual(['draft', 'saving', 'saved', 'saving', 'takenOver']);

    type('Jane made toast. Then a walk.');
    act(() => {
      autosave().flush();
      autosave().resume();
    });
    showPage('hidden');
    showPage('visible');
    goOnline(false);
    goOnline(true);
    await wait(60_000);

    expect(sentSaves).toHaveLength(2);
    expect(said).toEqual(['draft', 'saving', 'saved', 'saving', 'takenOver']);
  });

  it('gives false from flushNow(), and nothing from checkSave() meeting it or after it, sending no more', async () => {
    answerSaves(saved(), takenOver);
    const { autosave, type } = renderAutosave(reopened);
    type('Jane');
    act(() => {
      autosave().flush();
    });
    await wait(0);

    const checked = outcome(autosave().checkSave());
    await wait(0);
    expect(checked()).toBeUndefined();

    const flushed = outcome(autosave().flushNow());
    const checkedAgain = outcome(autosave().checkSave());
    await wait(30_000);
    expect(flushed()).toBe(false);
    expect(checkedAgain()).toBeUndefined();
    expect(sentSaves).toHaveLength(2);
  });

  it('sends what the form holds at once on keep(), under a new clientId numbered from 1, then "Saved"', async () => {
    const { autosave, type, said } = await loseTheNote();
    type('Jane made toast. Then a walk.');

    act(() => {
      autosave().keep();
    });
    await wait(0);
    type('Jane made toast. Then a walk to the shops.');
    act(() => {
      autosave().flush();
    });
    await wait(0);

    const firstId = sentSaves[0].body.clientId;
    expect(sentSaves.map(({ body }) => [body.clientId === firstId, body.seq, body.narrative])).toEqual([
      [true, 1, 'Jane'],
      [true, 2, 'Jane made toast.'],
      [false, 1, 'Jane made toast. Then a walk.'],
      [false, 2, 'Jane made toast. Then a walk to the shops.'],
    ]);
    expect(sentSaves[2].body.clientId).toBe(sentSaves[3].body.clientId);
    expect(said.slice(5)).toEqual(['saving', 'saved', 'saving', 'saved']);
  });

  it('is unsaved() while the banner waits, nothing changed since, until the save of keep() is answered', async () => {
    answerSaves(saved(), takenOver, saved('2026-09-30T23:44:00Z'));
    const { autosave, type } = renderAutosave(reopened);
    type('Jane');
    act(() => {
      autosave().flush();
    });
    await wait(0);
    void autosave().checkSave();
    await wait(0);
    expect(sentSaves).toHaveLength(2);
    expect(autosave().unsaved()).toBe(true);

    act(() => {
      autosave().keep();
    });
    expect(autosave().unsaved()).toBe(true);
    await wait(0);
    expect(autosave().unsaved()).toBe(false);
  });

  it('sends on keep() with no change made since the takeover, as the server holds the other version', async () => {
    const { autosave } = await loseTheNote();

    act(() => {
      autosave().keep();
    });
    await wait(0);

    expect(sentTexts()).toEqual([
      [1, 'Jane'],
      [2, 'Jane made toast.'],
      [1, 'Jane made toast.'],
    ]);
  });

  /**
   * The server's answers for a draft that another page saved last, as its save 3, by design.md §5.6's rules: from
   * another `clientId`, `seq` 1 takes the working copy over and any other number is `409 draft.taken_over`.
   */
  function heldByAnotherPage(): Answer {
    const last = { clientId: crypto.randomUUID(), seq: 3 };
    return () => {
      const { clientId, seq } = sentSaves.at(-1)!.body as { clientId: string; seq: number };
      if (clientId === last.clientId) {
        last.seq = Math.max(last.seq, seq);
      } else if (seq === 1) {
        Object.assign(last, { clientId, seq });
      } else {
        return takenOver();
      }

      return saved('2026-09-30T23:44:00Z')();
    };
  }

  it("resends a reopened draft as a new clientId's 1, with no banner, when its first save was lost", async () => {
    answerSaves(noConnection, heldByAnotherPage());
    const { autosave, type, said } = renderAutosave(reopened);
    type('Jane');
    act(() => {
      autosave().flush();
    });
    await wait(1_000);

    const lostId = sentSaves[0].body.clientId;
    expect(sentSaves.map(({ body }) => [body.clientId === lostId, body.seq, body.narrative])).toEqual([
      [true, 1, 'Jane'],
      [true, 2, 'Jane'],
      [false, 1, 'Jane'],
    ]);
    expect(said).toEqual(['draft', 'saving', 'saved']);

    // The page's next save goes on under the new clientId, which the server now holds as the last to save.
    type('Jane made toast.');
    act(() => {
      autosave().flush();
    });
    await wait(0);
    expect(sentSaves[3].body).toMatchObject({ clientId: sentSaves[2].body.clientId, seq: 2 });
    expect(said.at(-1)).toBe('saved');
  });

  it('sends it again so too after the page-hidden save used up the first number offline', async () => {
    answerSaves(noConnection, heldByAnotherPage());
    const { type, said } = renderAutosave(reopened);
    type('Jane');
    goOnline(false);
    showPage('hidden');
    await wait(0);
    showPage('visible');
    await wait(30_000);

    goOnline(true);
    await wait(0);

    expect(sentSaves.map(({ body, keepalive }) => [body.seq, keepalive])).toEqual([
      [1, true],
      [2, undefined],
      [1, undefined],
    ]);
    expect(sentSaves[2].body.clientId).not.toBe(sentSaves[0].body.clientId);
    expect(said).toEqual(['draft', 'saving', 'failed', 'saved']);
  });
});
