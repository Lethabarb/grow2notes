import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type * as ApiModule from './api.ts';
import type * as MeModule from './me.ts';
import type * as QueryClientModule from './queryClient.ts';
import type * as SessionModule from './session.ts';

// Fresh copies for each test, as the wrapper keeps the antiforgery token from one request to the next.
let api: typeof ApiModule.api;
let refreshMe: typeof MeModule.refreshMe;
let createQueryClient: typeof QueryClientModule.createQueryClient;
let startClock: typeof SessionModule.startClock;
let holdWhile: typeof SessionModule.holdWhile;
let subscribe: typeof SessionModule.subscribe;
let getPhase: typeof SessionModule.getPhase;
let untilSessionSurelyEnded: typeof SessionModule.untilSessionSurelyEnded;
let registerSaveFlush: typeof SessionModule.registerSaveFlush;
let flushSaves: typeof SessionModule.flushSaves;
type Phase = SessionModule.Phase;

const antiforgeryPath = '/api/auth/antiforgery';
const mePath = '/api/auth/me';

// When each test starts the clock, on a whole second.
const startTime = Date.parse('2026-10-12T09:00:00+11:00');

const fetchMock = vi.fn<typeof fetch>();

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = () => Promise<Response>;

// How the server meets each request but the antiforgery token's, which a test sets before it sends one.
let server: Answer;

/** An answer with `status`, as the API gives it: JSON for a `200`, no body for a `204`, and a problem otherwise. */
function answer(status: number): Answer {
  return () => {
    if (status === 204) {
      return Promise.resolve(new Response(null, { status }));
    }

    const [body, type] =
      status === 200
        ? ['{}', 'application/json']
        : [JSON.stringify({ type: 'about:blank', title: 'Problem', status }), 'application/problem+json'];
    return Promise.resolve(new Response(body, { status, headers: { 'Content-Type': type } }));
  };
}

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** `then`, given `ms` after the request is sent. */
function after(ms: number, then: Answer): Answer {
  return () =>
    new Promise<void>((resolve) => {
      setTimeout(resolve, ms);
    }).then(then);
}

/** `then`, held back until `release()` is called, and given at once from then on. */
function heldBack(then: Answer): { answer: Answer; release: () => void } {
  let release = () => {};
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { answer: () => released.then(then), release };
}

/** A promise that the test settles, as it likes. */
function pending() {
  let resolve = () => {};
  let reject = () => {};
  const promise = new Promise<void>((resolvePromise, rejectPromise) => {
    resolve = () => resolvePromise();
    reject = () => rejectPromise(new Error('Failed'));
  });
  return { promise, resolve, reject };
}

/** `clock` as a time since the start, in minutes and seconds, as '28:00', or with hours first, as '12:01:00'. */
function since(clock: string): number {
  return clock.split(':').reduce((total, part) => total * 60 + Number(part), 0) * 1000;
}

/** Moves the fake clock on to `clock` after the start, running each timer and promise that falls due by then. */
async function at(clock: string) {
  const ms = startTime + since(clock) - Date.now();
  if (ms < 0) {
    throw new Error(`The clock is past ${clock} already.`);
  }

  await vi.advanceTimersByTimeAsync(ms);
}

/** Runs the promises and timers that are due now. */
async function settle() {
  await vi.advanceTimersByTimeAsync(0);
}

const stops: (() => void)[] = [];

/** Starts the clock, which is stopped after the test. */
function start(checkSession: () => Promise<unknown> = () => Promise.resolve()) {
  stops.push(startClock(checkSession));
}

const unsubscribers: (() => void)[] = [];

/** Each phase that the clock tells its subscribers of from now on, in order. */
function watch(): Phase[] {
  const told: Phase[] = [];
  unsubscribers.push(
    subscribe(() => {
      told.push(getPhase());
    }),
  );
  return told;
}

/** The page becomes `state`, as the browser says so. */
function becomes(state: DocumentVisibilityState) {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(state);
  document.dispatchEvent(new Event('visibilitychange'));
}

/** How many requests have gone to `GET /api/auth/me`. */
function meRequests() {
  return fetchMock.mock.calls.filter(([path]) => path === mePath).length;
}

beforeEach(async () => {
  vi.resetModules();
  ({ api } = await import('./api.ts'));
  ({ refreshMe } = await import('./me.ts'));
  ({ createQueryClient } = await import('./queryClient.ts'));
  ({ startClock, holdWhile, subscribe, getPhase, untilSessionSurelyEnded, registerSaveFlush, flushSaves } =
    await import('./session.ts'));
  vi.useFakeTimers();
  vi.setSystemTime(startTime);
  fetchMock.mockImplementation((path) =>
    path === antiforgeryPath
      ? Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }))
      : server(),
  );
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  for (const stop of stops.splice(0)) {
    stop();
  }
  for (const unsubscribe of unsubscribers.splice(0)) {
    unsubscribe();
  }
  fetchMock.mockReset();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the session clock', () => {
  it('warns at 28:00 from an accepted request, steps at 29:00, 29:20 and 29:40, and expires at 30:00', async () => {
    start();
    const told = watch();
    server = answer(200);
    await api('/api/today');

    for (const [clock, phase] of [
      ['27:59', 'active'],
      ['28:00', 'warn-2m'],
      ['28:59', 'warn-2m'],
      ['29:00', 'warn-1m'],
      ['29:19', 'warn-1m'],
      ['29:20', 'warn-40s'],
      ['29:39', 'warn-40s'],
      ['29:40', 'warn-20s'],
      ['29:59', 'warn-20s'],
      ['30:00', 'expired'],
      ['45:00', 'expired'],
    ]) {
      await at(clock);
      expect(getPhase(), `at ${clock}`).toBe(phase);
    }

    expect(told).toEqual(['warn-2m', 'warn-1m', 'warn-40s', 'warn-20s', 'expired']);
    // Nothing at any mark: the one request is the test's own.
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(vi.getTimerCount()).toBe(0);
  });

  it("counts a request from its send time, not its answer's", async () => {
    start();
    const told = watch();
    await at('10:00');
    server = after(9_000, answer(200));
    const request = api('/api/today');
    await at('10:09');
    await request;

    await at('37:59');
    expect(getPhase()).toBe('active');
    await at('38:00');
    expect(getPhase()).toBe('warn-2m');
    expect(told).toEqual(['warn-2m']);
  });

  // The server starts its 30 minutes as it takes the request, at some moment between the send and the answer, so the
  // session has surely ended 30 minutes after the answer came, with 5 seconds more for the clocks' drift (A61).
  it('has the session surely ended 30:05 after the latest accepted answer came, or at once on a 401', async () => {
    start();
    await at('10:00');
    server = after(9_000, answer(200));
    const request = api('/api/today');
    await at('10:09');
    await request;

    await at('40:00');
    expect(getPhase()).toBe('expired');
    expect(untilSessionSurelyEnded()).toBe(14_000);
    await at('40:13');
    expect(untilSessionSurelyEnded()).toBe(1_000);
    await at('40:14');
    expect(untilSessionSurelyEnded()).toBe(0);
    await at('50:00');
    expect(untilSessionSurelyEnded()).toBe(0);

    server = answer(200);
    await api('/api/today');
    expect(untilSessionSurelyEnded()).toBe(since('30:05'));
    server = answer(401);
    await api(mePath).catch(() => {});
    expect(untilSessionSurelyEnded()).toBe(0);
  });

  // A clock in a warning phase, one expired at 30:00, and one that a 401 ended, each with when it warns once a request
  // that it accepts has started it afresh.
  const clocks = [
    { clock: 'a warning', phase: 'warn-1m', reach: () => at('29:00'), warns: '57:00' },
    { clock: 'an expired', phase: 'expired', reach: () => at('31:00'), warns: '59:00' },
    {
      clock: 'an ended',
      phase: 'expired',
      reach: async () => {
        await at('10:00');
        server = answer(401);
        await api(mePath).catch(() => {});
      },
      warns: '38:00',
    },
  ] as const;

  it.each(
    ([
      { status: 200, method: 'GET', path: '/api/today' },
      { status: 204, method: 'POST', path: '/api/auth/ping' },
      { status: 404, method: 'GET', path: '/api/participants/2f9a' },
      { status: 503, method: 'GET', path: '/api/today' },
    ] as const).flatMap((request) => clocks.map((clock) => ({ ...request, ...clock }))),
  )('starts $clock clock afresh on $status to $method $path', async ({ status, method, path, phase, reach, warns }) => {
    start();
    await reach();
    expect(getPhase()).toBe(phase);
    const told = watch();

    server = answer(status);
    await api(path, { method }).catch(() => {});

    expect(getPhase()).toBe('active');
    expect(told).toEqual(['active']);
    await at(warns);
    expect(told).toEqual(['active', 'warn-2m']);
  });

  it.each(
    ([
      { failure: 'no answer', method: 'GET', path: '/api/today', fails: noConnection },
      { failure: "a failed sign-in's 401", method: 'POST', path: '/api/auth/login/totp', fails: answer(401) },
      { failure: "logout's 401", method: 'POST', path: '/api/auth/logout', fails: answer(401) },
    ] as const).flatMap((request) => clocks.map((clock) => ({ ...request, ...clock }))),
  )('leaves $clock clock as it was on $failure', async ({ method, path, fails, phase, reach }) => {
    start();
    // A change first, so that the antiforgery token, whose request is accepted too, is held before the failure.
    server = answer(204);
    await api('/api/auth/ping', { method: 'POST' });
    await reach();
    const told = watch();
    const tokenRequests = fetchMock.mock.calls.filter(([sent]) => sent === antiforgeryPath).length;

    server = fails;
    await expect(api(path, { method, body: method === 'POST' ? {} : undefined })).rejects.toThrow();

    expect(fetchMock.mock.calls.filter(([sent]) => sent === antiforgeryPath)).toHaveLength(tokenRequests);
    expect(getPhase()).toBe(phase);
    expect(told).toEqual([]);
  });

  it('goes on with the countdown after a request with no answer', async () => {
    start();
    const told = watch();
    await at('29:00');

    server = noConnection;
    await expect(api('/api/today')).rejects.toThrow();
    await at('29:20');

    expect(told).toEqual(['warn-2m', 'warn-1m', 'warn-40s']);
  });

  it("expires at once on /api/auth/me's 401, with no warning first", async () => {
    start();
    const told = watch();
    await at('10:00');

    server = answer(401);
    await expect(api(mePath)).rejects.toThrow();

    expect(getPhase()).toBe('expired');
    expect(told).toEqual(['expired']);
    expect(vi.getTimerCount()).toBe(0);
  });

  describe('while a request is out', () => {
    it('holds the phase, so that one sent at 27:59 and answered at 28:05 brings no warning', async () => {
      start();
      const told = watch();
      await at('27:59');
      server = after(6_000, answer(200));
      const request = api('/api/today');

      await at('28:00');
      expect(getPhase()).toBe('active');
      await at('28:05');
      await request;

      expect(getPhase()).toBe('active');
      expect(told).toEqual([]);
      // 28 minutes from its send time.
      await at('55:59');
      expect(told).toEqual(['warn-2m']);
    });

    it('brings the warning as one that fails at 28:05 does', async () => {
      start();
      const told = watch();
      await at('27:59');
      server = after(6_000, noConnection);
      const request = api('/api/today').catch(() => {});

      await at('28:04');
      expect(getPhase()).toBe('active');
      await at('28:05');
      await request;

      expect(getPhase()).toBe('warn-2m');
      expect(told).toEqual(['warn-2m']);
    });

    it('keeps the newest send time when an earlier request is answered after a later one', async () => {
      start();
      const told = watch();
      await at('10:00');
      server = after(9_000, answer(200));
      const earlier = api('/api/today');
      await at('10:05');
      server = after(1_000, answer(200));
      const later = api('/api/today');

      await at('10:09');
      await Promise.all([earlier, later]);

      // 28 minutes from the later send time, which the earlier request's answer, coming last, leaves as it is.
      await at('38:04');
      expect(getPhase()).toBe('active');
      await at('38:05');
      expect(getPhase()).toBe('warn-2m');
      expect(told).toEqual(['warn-2m']);
    });

    it('counts its holds, and decides once the last has settled', async () => {
      start();
      const told = watch();
      await at('27:00');
      const first = pending();
      const second = pending();
      holdWhile(first.promise);
      holdWhile(second.promise);

      await at('28:30');
      first.resolve();
      await settle();
      expect(getPhase()).toBe('active');

      await at('28:40');
      second.reject();
      await settle();
      expect(getPhase()).toBe('warn-2m');
      expect(told).toEqual(['warn-2m']);
    });
  });

  it('changes nothing while the page is hidden, for a request that settles then too', async () => {
    start();
    const told = watch();
    await at('27:00');
    becomes('hidden');

    await at('28:10');
    server = noConnection;
    await expect(api('/api/today')).rejects.toThrow();
    await at('31:00');

    expect(getPhase()).toBe('active');
    expect(told).toEqual([]);
    expect(vi.getTimerCount()).toBe(0);
  });

  describe('as the page becomes visible again', () => {
    const absences = [
      { absence: 'hidden at 27:00, visible at 28:30', hidden: '27:00', visible: '28:30', byTime: 'warn-2m' },
      { absence: 'hidden for 31 minutes', hidden: '00:00', visible: '31:00', byTime: 'expired' },
    ] as const;

    it.each(
      absences.flatMap((absence) => [
        { ...absence, outcome: 'a 200', answers: answer(200), phase: 'active', told: [] },
        { ...absence, outcome: 'a 401', answers: answer(401), phase: 'expired', told: ['expired'] },
      ]),
    )('holds the phase on one GET /api/auth/me, $absence, until $outcome decides', async ({
      hidden,
      visible,
      answers,
      phase,
      told: phasesTold,
    }) => {
      const client = createQueryClient();
      start(() => refreshMe(client));
      const told = watch();
      await at(hidden);
      becomes('hidden');
      // Nothing moves while the page is hidden.
      expect(vi.getTimerCount()).toBe(0);
      await at(visible);
      expect(getPhase()).toBe('active');

      const held = heldBack(answers);
      server = held.answer;
      becomes('visible');
      await vi.advanceTimersByTimeAsync(5_000);

      expect(meRequests()).toBe(1);
      expect(getPhase()).toBe('active');
      expect(told).toEqual([]);

      held.release();
      await settle();

      expect(meRequests()).toBe(1);
      expect(getPhase()).toBe(phase);
      expect(told).toEqual(phasesTold);
    });

    it.each(absences)("gives the time's phase, $absence, with no connection after its silent retry", async ({
      hidden,
      visible,
      byTime,
    }) => {
      const client = createQueryClient();
      start(() => refreshMe(client));
      const told = watch();
      await at(hidden);
      becomes('hidden');
      await at(visible);

      const held = heldBack(noConnection);
      server = held.answer;
      becomes('visible');
      await vi.advanceTimersByTimeAsync(5_000);
      held.release();
      await settle();

      // The query client's silent retry is still to come, a second later.
      expect(meRequests()).toBe(1);
      expect(getPhase()).toBe('active');
      await vi.advanceTimersByTimeAsync(1_000);

      expect(meRequests()).toBe(2);
      expect(getPhase()).toBe(byTime);
      expect(told).toEqual([byTime]);
    });
  });

  it('never warns with a request accepted every 20 minutes for 12 hours, and expires on the 401 then', async () => {
    start();
    const told = watch();
    server = answer(200);
    for (let minutes = 0; minutes <= 12 * 60; minutes += 20) {
      await at(`${minutes}:00`);
      await api(mePath);
    }

    expect(told).toEqual([]);

    // Past the 12-hour limit, which only the server knows of (design.md §8.4).
    await at('12:01:00');
    server = answer(401);
    await expect(api(mePath)).rejects.toThrow();

    expect(told).toEqual(['expired']);
  });

  it('leaves no timer once stopped, changes for no request or hold, and starts afresh, active', async () => {
    const checkSession = vi.fn(() => Promise.resolve());
    const stop = startClock(checkSession);
    const told = watch();
    await at('28:10');
    const hold = pending();
    holdWhile(hold.promise);

    await at('28:20');
    stop();

    expect(getPhase()).toBe('active');
    expect(told).toEqual(['warn-2m', 'active']);
    expect(vi.getTimerCount()).toBe(0);

    becomes('visible');
    server = answer(200);
    await at('29:00');
    await api('/api/today');
    hold.resolve();
    await settle();

    expect(checkSession).not.toHaveBeenCalled();
    expect(getPhase()).toBe('active');
    expect(told).toEqual(['warn-2m', 'active']);
    expect(vi.getTimerCount()).toBe(0);

    await at('40:00');
    start();
    expect(getPhase()).toBe('active');
    await at('67:59');
    expect(getPhase()).toBe('active');
    await at('68:00');
    expect(told).toEqual(['warn-2m', 'active', 'warn-2m']);
  });
});

// A mounted note form's autosave, which the clock asks at 28:00 to send a change waiting, and Sign out before its
// logout (session-timeout.md *The session clock* step 3; app-shell.md *Press Sign out* step 2; T10's Notes).
describe("a note form's change waiting", () => {
  const draftPath = '/api/participants/3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95/notes/2026-10-01/draft';

  /**
   * Registers a stand-in for a mounted note form's autosave, as the form registers its own: `change()` makes a change,
   * which its flush sends as a `PUT` through `api()`, as autosave does, giving `true` once it is answered and `false`
   * once it fails. Gives the flush, to count its calls, and `unmount()`, which removes it as the form goes.
   */
  function mountForm() {
    let made = 0;
    let saved = 0;
    const flushNow = vi.fn(async () => {
      const carried = made;
      try {
        await api(draftPath, { method: 'PUT', body: { seq: carried } });
      } catch {
        return false;
      }

      saved = Math.max(saved, carried);
      return true;
    });
    const unmount = registerSaveFlush({ unsaved: () => saved < made, flushNow });
    stops.push(unmount);
    return {
      flushNow,
      change: () => {
        made += 1;
      },
      unmount,
    };
  }

  /** Holds the antiforgery token, as after the form's first save, so that a later change asks for none. */
  async function holdToken() {
    server = answer(200);
    await api(draftPath, { method: 'PUT', body: {} });
  }

  it('sends a change made at 27:59 at 28:00, holding the phase, and opens no warning once it is accepted', async () => {
    start();
    const told = watch();
    await holdToken();
    const form = mountForm();
    await at('27:59');
    form.change();
    server = after(3_000, answer(200));

    await at('28:00');
    expect(form.flushNow).toHaveBeenCalledOnce();
    await at('28:02');
    expect(getPhase()).toBe('active');
    await at('28:03');

    expect(getPhase()).toBe('active');
    expect(told).toEqual([]);
    // 28 minutes from the save's send time, with nothing unsaved by then.
    await at('55:59');
    expect(told).toEqual([]);
    await at('56:00');
    expect(told).toEqual(['warn-2m']);
    expect(form.flushNow).toHaveBeenCalledOnce();
  });

  it('lets the warning open as a save failing at 28:00 settles, with no second flush in that idle period', async () => {
    start();
    const told = watch();
    await holdToken();
    const form = mountForm();
    form.change();
    server = after(1_000, noConnection);

    await at('28:00');
    expect(form.flushNow).toHaveBeenCalledOnce();
    expect(getPhase()).toBe('active');
    await at('28:01');

    expect(getPhase()).toBe('warn-2m');
    expect(told).toEqual(['warn-2m']);
    await at('29:00');
    expect(told).toEqual(['warn-2m', 'warn-1m']);
    expect(form.flushNow).toHaveBeenCalledOnce();
  });

  it('asks again at the 28:00 mark of the next idle period, after Stay signed in', async () => {
    start();
    const told = watch();
    await holdToken();
    const form = mountForm();
    form.change();
    server = noConnection;
    await at('28:00');
    expect(told).toEqual(['warn-2m']);

    await at('28:30');
    server = answer(204);
    await api('/api/auth/ping', { method: 'POST' });
    expect(told).toEqual(['warn-2m', 'active']);
    server = answer(200);
    await at('56:30');

    expect(form.flushNow).toHaveBeenCalledTimes(2);
    expect(getPhase()).toBe('active');
    expect(told).toEqual(['warn-2m', 'active']);
  });

  it('calls nothing with nothing unsaved, and warns at 28:00', async () => {
    start();
    const told = watch();
    const form = mountForm();

    await at('28:00');

    expect(form.flushNow).not.toHaveBeenCalled();
    expect(told).toEqual(['warn-2m']);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('calls nothing once the form has gone, or once a 401 has ended the session', async () => {
    start();
    const told = watch();
    const gone = mountForm();
    gone.change();
    gone.unmount();
    await at('28:00');
    expect(told).toEqual(['warn-2m']);

    server = answer(200);
    await api('/api/today');
    const form = mountForm();
    form.change();
    await at('40:00');
    server = answer(401);
    await api(mePath).catch(() => {});

    expect(told).toEqual(['warn-2m', 'active', 'expired']);
    expect(gone.flushNow).not.toHaveBeenCalled();
    expect(form.flushNow).not.toHaveBeenCalled();
  });

  describe('for Sign out', () => {
    it('gives true at once with no form mounted, or nothing unsaved, sending nothing', async () => {
      await expect(flushSaves()).resolves.toBe(true);
      const form = mountForm();
      await expect(flushSaves()).resolves.toBe(true);

      expect(form.flushNow).not.toHaveBeenCalled();
      expect(fetchMock).not.toHaveBeenCalled();
    });

    it('gives true once every change is saved, one made while its save was out included', async () => {
      const form = mountForm();
      form.change();
      server = after(1_000, answer(200));
      let flushed: boolean | undefined;
      void flushSaves().then((result) => {
        flushed = result;
      });

      await vi.advanceTimersByTimeAsync(500);
      form.change();
      await vi.advanceTimersByTimeAsync(500);
      expect(flushed).toBeUndefined();
      await vi.advanceTimersByTimeAsync(1_000);

      expect(flushed).toBe(true);
      expect(form.flushNow).toHaveBeenCalledTimes(2);
    });

    it('gives false as soon as a flush does, asking no more', async () => {
      const form = mountForm();
      form.change();
      server = noConnection;

      await expect(flushSaves()).resolves.toBe(false);

      expect(form.flushNow).toHaveBeenCalledOnce();
    });
  });
});
