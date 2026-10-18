import { createHash } from 'node:crypto';
import { setTimeout as wait } from 'node:timers/promises';
import type { Page, Route } from '@playwright/test';
import type { Me } from './me.ts';
import type { AdminParticipant } from './participants.ts';

/** A goal on the note form: its ID and its wording, as the manager's list or a draft's snapshot holds it. */
export type NoteGoal = { id: string; text: string };

/** Who started another user's draft, as `409 note.in_progress_by_other` names them (design.md §6.9). */
export type StartedBy = { authorDisplayName: string; startedAtUtc: string };

/** A `PUT {base}/draft` that the page sent: its JSON body (design.md §6.3), as far as T08's form fills it. */
export type DraftSave = {
  clientId: string;
  seq: number;
  listsVersion?: string;
  narrative: string;
  tickedGoalIds: string[];
};

/** A working copy as the store keeps it: whose it is, its snapshot of the goals, its ticks and text. */
type HeldDraft = StartedBy & {
  authorUserId: string;
  snapshot: NoteGoal[];
  ticked: string[];
  narrative: string;
  savedAtUtc: string;
};

/** Alex P., another worker, who started a note at 9:14 am in Melbourne on 1 October 2026 (note-form.md's sketch). */
export const alexStarted: StartedBy = { authorDisplayName: 'Alex P.', startedAtUtc: '2026-09-30T23:14:00Z' };

/** 06:12 UTC on 1 October 2026, which the page shows as "Saved 4:12 pm" on that day in Melbourne. */
const savedAtFourTwelve = '2026-10-01T06:12:00.000Z';

// A participant's header, and their note for a day's working copy, which the form reads and autosave puts.
const headerPath = /^\/api\/participants\/([^/]+)$/;
const draftPath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)\/draft$/;

/** An RFC 9457 problem with the API's stable `code` and any extensions (design.md §6.9). */
const problem = (status: number, code: string, extensions: Record<string, unknown> = {}) => ({
  status,
  contentType: 'application/problem+json',
  body: JSON.stringify({ type: 'about:blank', title: code, status, code, ...extensions }),
});

/** A goal as the API gives it, its ID and wording alone, from a goal with more, such as goals.ts's. */
const goalOf = ({ id, text }: NoteGoal): NoteGoal => ({ id, text });

/** The lists' version, which changes with any goal's ID, wording or place, as the API's does (design.md §5.7). */
const versionOf = (goals: readonly NoteGoal[]) =>
  createHash('sha256')
    .update(JSON.stringify(goals.map(({ id, text }) => [id, text])))
    .digest('base64');

/** The day before `date`, both written `yyyy-MM-dd`. */
const dayBefore = (date: string) =>
  new Date(Date.parse(`${date}T00:00:00Z`) - 24 * 60 * 60 * 1000).toISOString().slice(0, 10);

type StoreOptions = {
  /** The server's date in Melbourne, such as `me.today`, which decides which first saves start a note. */
  today: string;
  /** The made-up participants whose notes can be opened, with their status as the header gives it. */
  participants: readonly AdminParticipant[];
  /** Each participant's active goals, by participant ID, in the manager's order; one not named has none. */
  goals?: Readonly<Record<string, readonly NoteGoal[]>>;
  /** The organisation's guide prompts, `""` for none. */
  prompts?: string;
  /** The time that each save answers with, by default 4:12 pm in Melbourne on 1 October 2026. */
  savedAtUtc?: string;
};

/**
 * Participants' notes as the API keeps them (design.md §5.5 to §5.7), in the browser, so that no note request reaches
 * the app, where no made-up person has a session: a participant's header; the note form, a template, the caller's own
 * draft or another user's `409`, or `404` for a date after today and, to a worker, before yesterday (design.md A65);
 * and autosave's `PUT`, which creates the note with the creating save's checks, or updates the caller's own draft.
 * `serve` answers one page's requests as one person's.
 */
export function notesStore({
  today: firstToday,
  participants,
  goals: firstGoals = {},
  prompts = '',
  savedAtUtc = savedAtFourTwelve,
}: StoreOptions) {
  let today = firstToday;
  const headers = new Map(participants.map(({ id, givenName, familyName, status }) => [
    id,
    { id, givenName, familyName, status },
  ]));
  const goals = new Map(participants.map(({ id }) => [id, (firstGoals[id] ?? []).map(goalOf)]));
  const drafts = new Map<string, HeldDraft>();
  const saves: DraftSave[] = [];

  /** The note form for `me`, as `GET {base}/draft` answers it. */
  function form(me: Me, participantId: string, noteDate: string) {
    const listed = goals.get(participantId) ?? [];
    const draft = drafts.get(`${participantId}/${noteDate}`);
    if (draft !== undefined) {
      if (draft.authorUserId !== me.userId) {
        const { authorDisplayName, startedAtUtc } = draft;
        return problem(409, 'note.in_progress_by_other', { authorDisplayName, startedAtUtc });
      }

      const json = {
        baseVersion: 0,
        isSaved: true,
        guidePrompts: prompts,
        goals: draft.snapshot.map((goal) => ({ ...goal, isTicked: draft.ticked.includes(goal.id) })),
        narrative: draft.narrative,
        savedAtUtc: draft.savedAtUtc,
      };
      return { json };
    }

    if (noteDate > today || (noteDate < dayBefore(today) && me.role !== 'Manager')) {
      return { status: 404 };
    }

    const json = {
      baseVersion: 0,
      isSaved: false,
      listsVersion: versionOf(listed),
      guidePrompts: prompts,
      goals: listed.map((goal) => ({ ...goal, isTicked: false })),
      narrative: '',
    };
    return { json };
  }

  /** A save of `me`'s working copy, as `PUT {base}/draft` answers it: the creating save's checks in their order. */
  function save(me: Me, participantId: string, noteDate: string, sent: DraftSave) {
    saves.push(sent);
    const key = `${participantId}/${noteDate}`;
    const draft = drafts.get(key);
    const ticked = [...new Set(sent.tickedGoalIds)];
    if (draft !== undefined) {
      if (draft.authorUserId !== me.userId) {
        const { authorDisplayName, startedAtUtc } = draft;
        return problem(409, 'note.in_progress_by_other', { authorDisplayName, startedAtUtc });
      }

      if (!ticked.every((id) => draft.snapshot.some((goal) => goal.id === id))) {
        return problem(422, 'validation.failed', { errors: { tickedGoalIds: ['Unknown goal.'] } });
      }

      Object.assign(draft, { ticked, narrative: sent.narrative, savedAtUtc });
      return { status: 200, json: { savedAtUtc } };
    }

    const listed = goals.get(participantId) ?? [];
    if (noteDate > today) {
      return problem(422, 'note.future_date');
    }

    if (noteDate < today && me.role !== 'Manager') {
      return problem(422, 'note.past_date_manager_only');
    }

    if (headers.get(participantId)?.status === 'Archived') {
      return problem(422, 'note.participant_archived');
    }

    if (sent.listsVersion !== versionOf(listed)) {
      return problem(409, 'note.lists_changed');
    }

    if (!ticked.every((id) => listed.some((goal) => goal.id === id))) {
      return problem(422, 'validation.failed', { errors: { tickedGoalIds: ['Unknown goal.'] } });
    }

    drafts.set(key, {
      authorUserId: me.userId,
      authorDisplayName: me.displayName,
      startedAtUtc: savedAtUtc,
      snapshot: [...listed],
      ticked,
      narrative: sent.narrative,
      savedAtUtc,
    });
    return { status: 201, json: { savedAtUtc } };
  }

  return {
    /**
     * Answers `page`'s requests for a participant's header and their notes' working copies from now on as `me`'s, each
     * `latencyMs` after it is sent, as on a slow network. A later call overrides it, as Playwright tries the latest
     * route first.
     */
    serve: (page: Page, me: Me, { latencyMs = 0 } = {}) =>
      page.route(
        ({ pathname }) => headerPath.test(pathname) || draftPath.test(pathname),
        async (route: Route) => {
          const request = route.request();
          const { pathname } = new URL(request.url());
          await wait(latencyMs);
          const [, headerId] = headerPath.exec(pathname) ?? [];
          if (headerId !== undefined) {
            const header = headers.get(headerId);
            await route.fulfill(header === undefined ? { status: 404 } : { json: header });
            return;
          }

          const [, participantId = '', noteDate = ''] = draftPath.exec(pathname) ?? [];
          if (!headers.has(participantId)) {
            await route.fulfill({ status: 404 });
            return;
          }

          await route.fulfill(
            request.method() === 'PUT'
              ? save(me, participantId, noteDate, request.postDataJSON() as DraftSave)
              : form(me, participantId, noteDate),
          );
        },
      ),
    /** Every save that the pages sent, in order, whatever it was answered. */
    saves,
    /** The working copy of a participant's note for a day, as the last save left it, if there is one. */
    draft: (participantId: string, noteDate: string) => drafts.get(`${participantId}/${noteDate}`),
    /** Moves the server's date on, as midnight in Melbourne does. */
    setToday: (date: string) => {
      today = date;
    },
    /** Gives a participant a new list of active goals, as a manager's add, reword, archive or move does. */
    setGoals: (participantId: string, list: readonly NoteGoal[]) => {
      goals.set(participantId, list.map(goalOf));
    },
    /** Archives a participant, as a manager does. */
    archive: (participantId: string) => {
      const header = headers.get(participantId);
      if (header !== undefined) {
        header.status = 'Archived';
      }
    },
    /** Starts a participant's note for a day as another user, whose draft it then is, with an empty box. */
    startElsewhere: (participantId: string, noteDate: string, startedBy: StartedBy) => {
      drafts.set(`${participantId}/${noteDate}`, {
        ...startedBy,
        authorUserId: 'another user',
        snapshot: [...(goals.get(participantId) ?? [])],
        ticked: [],
        narrative: '',
        savedAtUtc: startedBy.startedAtUtc,
      });
    },
  };
}

/** What `notesStore` gives. */
export type NotesStore = ReturnType<typeof notesStore>;

/**
 * Aborts each `GET` of a participant's header and of their note's working copy that the page sends from now on, in the
 * browser, as a phone with no signal would. The form's failure then shows about 1 s after it opens, after the query
 * client's silent retry. A later `serve` of a store overrides it.
 */
export async function failNoteReads(page: Page): Promise<void> {
  await page.route(
    ({ pathname }) => headerPath.test(pathname) || draftPath.test(pathname),
    (route) => (route.request().method() === 'GET' ? route.abort('internetdisconnected') : route.fallback()),
  );
}
