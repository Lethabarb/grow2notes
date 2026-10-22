import { createHash, randomUUID } from 'node:crypto';
import { setTimeout as wait } from 'node:timers/promises';
import type { Page, Request, Route } from '@playwright/test';
import type { Me } from './me.ts';
import type { AdminParticipant } from './participants.ts';
import type { ToReviewRow } from './reviews.ts';
import type { DraftsOf, MyDraft, NoteOf } from './today.ts';

/** A goal on the note form: its ID and its wording, as the manager's list or a draft's snapshot holds it. */
export type NoteGoal = { id: string; text: string };

/**
 * A group of common items on the note form: its ID, its name and its items, as the managers' list or a draft's
 * snapshot holds it. Every note is the one with `isEveryNote`.
 */
export type NoteGroup = { id: string; name: string; isEveryNote?: boolean; items: readonly NoteGoal[] };

/** Who started another user's draft, as `409 note.in_progress_by_other` names them (design.md §6.9). */
export type StartedBy = { authorDisplayName: string; startedAtUtc: string };

/** A `PUT {base}/draft` that the page sent: its JSON body (design.md §6.3), as far as the form fills it. */
export type DraftSave = {
  clientId: string;
  seq: number;
  listsVersion?: string;
  narrative: string;
  tickedGoalIds: string[];
  pickedGroupIds: string[];
  tickedCommonItemIds: string[];
  isFlagged: boolean;
  flagReason: string | null;
};

/** A `POST {base}/versions` that the page sent (design.md §6.3): its JSON body, as far as Submit fills it. */
export type VersionBody = {
  baseVersion: number;
  narrative: string;
  tickedGoalIds: string[];
  pickedGroupIds: string[];
  tickedCommonItemIds: string[];
  isFlagged: boolean;
  flagReason: string | null;
};

/** A Submit that the page sent: its `Idempotency-Key` and its body. */
export type VersionPost = { idempotencyKey: string | undefined; body: VersionBody };

/** A note's flag status, as the API names it (design.md §5.3), which Submit's answer and a manager's history carry. */
export type FlagStatus = 'None' | 'ToReview' | 'Reviewed';

/** A submitted note to put in the store, as `addSubmitted` takes it: who wrote it, when, and its version 1. */
export type SubmittedNote = {
  authorDisplayName: string;
  /** The author's user ID, someone else's by default. */
  authorUserId?: string;
  firstSubmittedAtUtc: string;
  narrative?: string;
  /** Every goal of its snapshot, in order, with its tick. */
  goals?: { text: string; isTicked: boolean }[];
  /** The reason of a flagged version; an unflagged one has none. */
  flagReason?: string;
  /**
   * The reason of version 1, flagged, when a version 2 has been saved over it, as T14's Save changes will save one,
   * which removed the flag or changed its reason: the note stays in To review on version 1's reason and time (A15),
   * and reads as version 2, with `flagReason`, if any, as its own.
   */
  firstFlagReason?: string;
  /** Set by hand, as T16 will set it; by default `ToReview` for a note with a flagged version, as Submit leaves it. */
  flagStatus?: FlagStatus;
};

/** The version in a note's `FlaggedVersionNumber` (design.md §5.3): its reason and when it was saved. */
type FlaggedVersion = { flagReason: string; createdAtUtc: string };

/**
 * A working copy as the store keeps it: whose it is, its snapshot of the goals, its ticks, text and flag, and the page
 * that saved it last, by its `clientId`, with the number of that page's last save (design.md §5.6).
 */
type HeldDraft = StartedBy & {
  authorUserId: string;
  snapshot: NoteGoal[];
  ticked: string[];
  groups: NoteGroup[];
  picked: string[];
  tickedItems: string[];
  narrative: string;
  isFlagged: boolean;
  flagReason: string | null;
  savedAtUtc: string;
  lastClientId: string;
  lastSeq: number;
};

/** A discarded draft as the store keeps it, hidden from every list and screen, its content kept (A29). */
type DiscardedDraft = HeldDraft & { discardedByUserId: string };

/** What another page of a draft's author holds as it saves the draft, as `saveElsewhere` takes it. */
export type SavedElsewhere = {
  narrative?: string;
  tickedGoalIds?: string[];
  pickedGroupIds?: string[];
  tickedCommonItemIds?: string[];
  isFlagged?: boolean;
  flagReason?: string;
  /** The time of its save, by default the store's. */
  savedAtUtc?: string;
};

/**
 * A submitted note as the store keeps it (design.md §5.3): its header, with its flag status and the version that
 * flagged it, if any, and its current version, the one that it reads as.
 */
type HeldSubmitted = StartedBy & {
  authorUserId: string;
  firstSubmittedAtUtc: string;
  idempotencyKey: string;
  currentVersionNumber: number;
  narrative: string;
  goals: { text: string; isTicked: boolean }[];
  commonItemGroups: ReturnType<typeof readGroupsOf>;
  isFlagged: boolean;
  flagReason: string | null;
  flagStatus: FlagStatus;
  flaggedVersion: FlaggedVersion | null;
};

/**
 * What Submit leaves on a note whose version 1 has `flagReason`, or none (design.md §5.8 step 5): To review, with that
 * version as the flagged one, when it is flagged, and else no flag.
 */
const flagOf = (
  flagReason: string | null,
  createdAtUtc: string,
): Pick<HeldSubmitted, 'flagStatus' | 'flaggedVersion'> =>
  flagReason === null
    ? { flagStatus: 'None', flaggedVersion: null }
    : { flagStatus: 'ToReview', flaggedVersion: { flagReason, createdAtUtc } };

/** Alex P., another worker, who started a note at 9:14 am in Melbourne on 1 October 2026 (note-form.md's sketch). */
export const alexStarted: StartedBy = { authorDisplayName: 'Alex P.', startedAtUtc: '2026-09-30T23:14:00Z' };

/** 06:12 UTC on 1 October 2026, which the page shows as "Saved 4:12 pm" on that day in Melbourne. */
const savedAtFourTwelve = '2026-10-01T06:12:00.000Z';

// A participant's header; their history; their note for a day, which the page reads first; its working copy, which
// the form reads and autosave puts; its versions, which Submit posts to; and its discard.
const headerPath = /^\/api\/participants\/([^/]+)$/;
const historyPath = /^\/api\/participants\/([^/]+)\/notes$/;
const notePath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)$/;
const draftPath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)\/draft$/;
const versionsPath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)\/versions$/;
const discardPath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)\/discard$/;
const notePaths = [headerPath, historyPath, notePath, draftPath, versionsPath, discardPath];

/** A page of a history, as design.md §6.1 *Lists* has it. */
const pageSize = 30;

/** An RFC 9457 problem with the API's stable `code` and any extensions (design.md §6.9). */
const problem = (status: number, code: string, extensions: Record<string, unknown> = {}) => ({
  status,
  contentType: 'application/problem+json',
  body: JSON.stringify({ type: 'about:blank', title: code, status, code, ...extensions }),
});

/** A `403` problem with no `code`, as Discard draft refuses a worker who did not start the draft (design.md §6.3). */
const forbidden = {
  status: 403,
  contentType: 'application/problem+json',
  body: JSON.stringify({ type: 'about:blank', title: 'Forbidden', status: 403 }),
};

/** The validation filter's `422`, naming the field that it refuses. */
const refused = (field: string) => problem(422, 'validation.failed', { errors: { [field]: ['Refused.'] } });

/** Blank as .NET's `string.IsNullOrWhiteSpace` counts it, U+0085 included, which `trim()` keeps. */
const isBlank = (text: string | null) => text === null || /^[\s\u0085]*$/.test(text);

/** A GUID, as the API binds the `Idempotency-Key` header. */
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** A goal as the API gives it, its ID and wording alone, from a goal with more, such as goals.ts's. */
const goalOf = ({ id, text }: NoteGoal): NoteGoal => ({ id, text });

/** The lists' version, which changes with any goal or group, or item of one, as the API's does (design.md §5.7). */
const versionOf = (goals: readonly NoteGoal[], groups: readonly NoteGroup[]) =>
  createHash('sha256')
    .update(
      JSON.stringify({
        goals: goals.map(({ id, text }) => [id, text]),
        groups: groups.map(({ id, name, items }) => ({ id, name, items: items.map((item) => [item.id, item.text]) })),
      }),
    )
    .digest('base64');

/** The picks and ticks of common items that a save or Submit sends. */
type SentTicks = Pick<DraftSave, 'pickedGroupIds' | 'tickedCommonItemIds'>;

/** The groups of a snapshot that the note shows: Every note and the picked groups, when they have items. */
const shownOf = (groups: readonly NoteGroup[], picked: readonly string[]) =>
  groups.filter((group) => (group.isEveryNote === true || picked.includes(group.id)) && group.items.length > 0);

/**
 * The `422` that the API answers picks and ticks that a snapshot's `groups` do not allow, in its order: a missing
 * list, a pick of Every note, of a group with no items or of one outside the snapshot, and a tick of an item that is
 * not in Every note or a picked group; or `null` for those that it allows.
 */
function refusedTicks(groups: readonly NoteGroup[], { pickedGroupIds, tickedCommonItemIds }: SentTicks) {
  if (!Array.isArray(pickedGroupIds)) {
    return refused('pickedGroupIds');
  }

  if (!Array.isArray(tickedCommonItemIds)) {
    return refused('tickedCommonItemIds');
  }

  const offered = groups.filter((group) => group.isEveryNote !== true && group.items.length > 0);
  if (!pickedGroupIds.every((id) => offered.some((group) => group.id === id))) {
    return refused('pickedGroupIds');
  }

  const shown = shownOf(groups, pickedGroupIds).flatMap((group) => group.items);
  return tickedCommonItemIds.every((id) => shown.some((item) => item.id === id))
    ? null
    : refused('tickedCommonItemIds');
}

/** The groups as `GET {base}/draft` gives them: all of the snapshot, with its picks and ticks. */
const formGroupsOf = (groups: readonly NoteGroup[], picked: readonly string[], ticked: readonly string[]) =>
  groups.map(({ id, name, isEveryNote = false, items }) => ({
    id,
    name,
    isEveryNote,
    isPicked: picked.includes(id),
    items: items.map((item) => ({ ...item, isTicked: ticked.includes(item.id) })),
  }));

/** The groups as `GET {base}` gives them for a note: those shown, with every item and its tick, and no IDs. */
const readGroupsOf = (groups: readonly NoteGroup[], picked: readonly string[], ticked: readonly string[]) =>
  shownOf(groups, picked).map(({ name, isEveryNote = false, items }) => ({
    name,
    isEveryNote,
    items: items.map(({ id, text }) => ({ text, isTicked: ticked.includes(id) })),
  }));

/** The date `days` after `date`, both written `yyyy-MM-dd`; before it for a negative number. */
export const daysAfter = (date: string, days: number) =>
  new Date(Date.parse(`${date}T00:00:00Z`) + days * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);

type StoreOptions = {
  /** The server's date in Melbourne, such as `me.today`, which decides which first saves start a note. */
  today: string;
  /** The made-up participants whose notes can be opened, with their status as the header gives it. */
  participants: readonly AdminParticipant[];
  /** Each participant's active goals, by participant ID, in the manager's order; one not named has none. */
  goals?: Readonly<Record<string, readonly NoteGoal[]>>;
  /** The organisation's active common item groups, Every note first, in the managers' order; by default none. */
  commonItemGroups?: readonly NoteGroup[];
  /** The organisation's guide prompts, `""` for none. */
  prompts?: string;
  /** The time that each save and each Submit answers with, by default 4:12 pm in Melbourne on 1 October 2026. */
  savedAtUtc?: string;
};

/**
 * Participants' notes as the API keeps them (design.md §5.5 to §5.8), in the browser, so that no note request reaches
 * the app, where no made-up person has a session: a participant's header; their history; the note, a submitted note's
 * current version, a draft's header, with its author's working copy to a manager, or `404` for none; the note form, a
 * template, the caller's own draft or another user's `409`, or `404` for a date after today, to a worker for one
 * before yesterday (design.md A65), and for a submitted note; autosave's `PUT`, which creates the note with the
 * creating save's checks, or updates the caller's own draft as design.md §5.6 numbers a page's saves, by `clientId`
 * and `seq`; Submit's `POST {base}/versions`, with its checks in the API's order, which puts a flagged version 1 in To
 * review; and Discard draft's `POST {base}/discard`, with its checks in the API's order, which hides the draft from
 * every list and screen and frees the day for a new note. `serve` answers one page's requests as one person's,
 * `myDrafts` gives a person's unfinished drafts and `toReview` the To review list.
 */
export function notesStore({
  today: firstToday,
  participants,
  goals: firstGoals = {},
  commonItemGroups = [],
  prompts = '',
  savedAtUtc = savedAtFourTwelve,
}: StoreOptions) {
  let today = firstToday;
  let loseNextSubmitAnswer = false;
  const headers = new Map(participants.map(({ id, givenName, familyName, status }) => [
    id,
    { id, givenName, familyName, status },
  ]));
  const goals = new Map(participants.map(({ id }) => [id, (firstGoals[id] ?? []).map(goalOf)]));
  const drafts = new Map<string, HeldDraft>();
  const submitted = new Map<string, HeldSubmitted>();
  const discarded = new Map<string, DiscardedDraft[]>();
  const saves: DraftSave[] = [];
  const submits: VersionPost[] = [];

  /**
   * The note at `{base}`, as `GET {base}` answers it to `me`: a submitted note's current version; a draft's header,
   * who started it and when, and, to a manager alone, its author's working copy in the same shape as a version, every
   * goal of its snapshot in order with its tick; or `404` for none, a discarded draft's day included.
   */
  function note(me: Me, participantId: string, noteDate: string) {
    const key = `${participantId}/${noteDate}`;
    const held = submitted.get(key);
    if (held !== undefined && headers.has(participantId)) {
      const { authorDisplayName, firstSubmittedAtUtc, narrative, isFlagged, flagReason, goals: ticks } = held;
      const author = { displayName: authorDisplayName };
      const current = { narrative, isFlagged, flagReason, goals: ticks, commonItemGroups: held.commonItemGroups };
      return { json: { status: 'Submitted', author, firstSubmittedAtUtc, current } };
    }

    const draft = drafts.get(key);
    if (draft === undefined || !headers.has(participantId)) {
      return { status: 404 };
    }

    const { authorDisplayName, startedAtUtc, snapshot, ticked, narrative, isFlagged, flagReason } = draft;
    const goalsTicked = snapshot.map(({ id, text }) => ({ text, isTicked: ticked.includes(id) }));
    const shown = readGroupsOf(draft.groups, draft.picked, draft.tickedItems);
    const content =
      me.role === 'Manager'
        ? { current: { narrative, isFlagged, flagReason, goals: goalsTicked, commonItemGroups: shown } }
        : {};
    return { json: { status: 'Draft', authorDisplayName, startedAtUtc, ...content } };
  }

  /**
   * Discard draft, as `POST {base}/discard` answers it, with its checks in the API's order: `404` for no participant
   * or a day with no note; `409 note.not_a_draft` for a submitted note or a day whose draft is discarded already; a
   * `403` problem with no `code` for a worker who did not start the draft; and otherwise `204`, the draft hidden, its
   * content kept, and the day free for a new note.
   */
  function discard(me: Me, participantId: string, noteDate: string) {
    const key = `${participantId}/${noteDate}`;
    const draft = drafts.get(key);
    if (!headers.has(participantId) || (draft === undefined && !submitted.has(key) && !discarded.has(key))) {
      return { status: 404 };
    }

    if (draft === undefined) {
      return problem(409, 'note.not_a_draft');
    }

    if (draft.authorUserId !== me.userId && me.role !== 'Manager') {
      return forbidden;
    }

    drafts.delete(key);
    discarded.set(key, [...(discarded.get(key) ?? []), { ...draft, discardedByUserId: me.userId }]);
    return { status: 204 };
  }

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
        commonItemGroups: formGroupsOf(draft.groups, draft.picked, draft.tickedItems),
        narrative: draft.narrative,
        isFlagged: draft.isFlagged,
        flagReason: draft.flagReason,
        savedAtUtc: draft.savedAtUtc,
      };
      return { json };
    }

    // A submitted note has no form until T14 adds pending edits.
    if (
      submitted.has(`${participantId}/${noteDate}`)
      || noteDate > today
      || (noteDate < daysAfter(today, -1) && me.role !== 'Manager')
    ) {
      return { status: 404 };
    }

    const json = {
      baseVersion: 0,
      isSaved: false,
      listsVersion: versionOf(listed, commonItemGroups),
      guidePrompts: prompts,
      goals: listed.map((goal) => ({ ...goal, isTicked: false })),
      commonItemGroups: formGroupsOf(commonItemGroups, [], []),
      narrative: '',
      isFlagged: false,
      flagReason: null,
    };
    return { json };
  }

  /**
   * A save of `me`'s working copy, as `PUT {base}/draft` answers it: the creating save's checks in their order, and a
   * later save numbered as §5.6 numbers a page's saves.
   */
  function save(me: Me, participantId: string, noteDate: string, sent: DraftSave) {
    saves.push(sent);
    const key = `${participantId}/${noteDate}`;
    const draft = drafts.get(key);
    const ticked = [...new Set(sent.tickedGoalIds)];
    // The reason only while flagged, as the API stores it (design.md §5.3).
    const flag = { isFlagged: sent.isFlagged, flagReason: sent.isFlagged ? sent.flagReason : null };
    if (submitted.has(key)) {
      // The draft's base, 0, is stale once its note is submitted, and the caller has no pending edit (§6.7).
      return problem(409, 'note.version_conflict');
    }

    if (draft !== undefined) {
      if (draft.authorUserId !== me.userId) {
        const { authorDisplayName, startedAtUtc } = draft;
        return problem(409, 'note.in_progress_by_other', { authorDisplayName, startedAtUtc });
      }

      if (draft.lastClientId === sent.clientId) {
        // The page's save sent again after its answer was lost, or one that a later save of the page overtook: the
        // time of the save that the working copy holds, and nothing changed.
        if (sent.seq <= draft.lastSeq) {
          return { status: 200, json: { savedAtUtc: draft.savedAtUtc } };
        }
      } else if (sent.seq !== 1) {
        // Another page has saved since this one last did, and only a page's first save takes the working copy over.
        return problem(409, 'draft.taken_over');
      }

      if (!ticked.every((id) => draft.snapshot.some((goal) => goal.id === id))) {
        return refused('tickedGoalIds');
      }

      const refusal = refusedTicks(draft.groups, sent);
      if (refusal !== null) {
        return refusal;
      }

      Object.assign(draft, {
        ticked,
        picked: [...new Set(sent.pickedGroupIds)],
        tickedItems: [...new Set(sent.tickedCommonItemIds)],
        narrative: sent.narrative,
        ...flag,
        savedAtUtc,
        lastClientId: sent.clientId,
        lastSeq: sent.seq,
      });
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

    if (sent.listsVersion !== versionOf(listed, commonItemGroups)) {
      return problem(409, 'note.lists_changed');
    }

    if (!ticked.every((id) => listed.some((goal) => goal.id === id))) {
      return refused('tickedGoalIds');
    }

    const refusal = refusedTicks(commonItemGroups, sent);
    if (refusal !== null) {
      return refusal;
    }

    drafts.set(key, {
      authorUserId: me.userId,
      authorDisplayName: me.displayName,
      startedAtUtc: savedAtUtc,
      snapshot: [...listed],
      ticked,
      groups: [...commonItemGroups],
      picked: [...new Set(sent.pickedGroupIds)],
      tickedItems: [...new Set(sent.tickedCommonItemIds)],
      narrative: sent.narrative,
      ...flag,
      savedAtUtc,
      lastClientId: sent.clientId,
      lastSeq: sent.seq,
    });
    return { status: 201, json: { savedAtUtc } };
  }

  /** Submit, as `POST {base}/versions` answers it, with its checks in the order of T09's Notes. */
  function submit(
    me: Me,
    participantId: string,
    noteDate: string,
    idempotencyKey: string | undefined,
    sent: VersionBody,
  ) {
    submits.push({ idempotencyKey, body: sent });
    if (idempotencyKey === undefined || !guid.test(idempotencyKey)) {
      return { status: 400 };
    }

    // The body's own checks, from the validation filter.
    if (sent.baseVersion !== 0) {
      return refused('baseVersion');
    }

    if (isBlank(sent.narrative) || sent.narrative.length > 20_000) {
      return refused('narrative');
    }

    if (sent.flagReason !== null && sent.flagReason.length > 200) {
      return refused('flagReason');
    }

    if (!Array.isArray(sent.tickedGoalIds)) {
      return refused('tickedGoalIds');
    }

    const key = `${participantId}/${noteDate}`;
    const draft = drafts.get(key);
    const held = submitted.get(key);
    if (!headers.has(participantId) || (draft === undefined && held === undefined)) {
      return { status: 404 };
    }

    if (draft !== undefined && draft.authorUserId !== me.userId) {
      const { authorDisplayName, startedAtUtc } = draft;
      return problem(409, 'note.in_progress_by_other', { authorDisplayName, startedAtUtc });
    }

    // The note's flag status as it is now, as the key finds the version, not the status its save set.
    if (held?.idempotencyKey === idempotencyKey) {
      const { firstSubmittedAtUtc, flagStatus } = held;
      return { status: 200, json: { versionNumber: 1, createdAtUtc: firstSubmittedAtUtc, flagStatus } };
    }

    if ([...submitted.values()].some((other) => other.idempotencyKey === idempotencyKey)) {
      return refused('idempotencyKey');
    }

    if (draft === undefined) {
      return problem(409, 'note.version_conflict');
    }

    if (sent.isFlagged && isBlank(sent.flagReason)) {
      return refused('flagReason');
    }

    const ticked = new Set(sent.tickedGoalIds);
    if (![...ticked].every((id) => draft.snapshot.some((goal) => goal.id === id))) {
      return refused('tickedGoalIds');
    }

    const refusal = refusedTicks(draft.groups, sent);
    if (refusal !== null) {
      return refusal;
    }

    const flagReason = sent.isFlagged ? sent.flagReason : null;
    const flag = flagOf(flagReason, savedAtUtc);
    submitted.set(key, {
      authorUserId: draft.authorUserId,
      authorDisplayName: draft.authorDisplayName,
      startedAtUtc: draft.startedAtUtc,
      firstSubmittedAtUtc: savedAtUtc,
      idempotencyKey,
      currentVersionNumber: 1,
      narrative: sent.narrative,
      goals: draft.snapshot.map(({ id, text }) => ({ text, isTicked: ticked.has(id) })),
      commonItemGroups: readGroupsOf(draft.groups, sent.pickedGroupIds, sent.tickedCommonItemIds),
      isFlagged: sent.isFlagged,
      flagReason,
      ...flag,
    });
    drafts.delete(key);
    return { status: 201, json: { versionNumber: 1, createdAtUtc: savedAtUtc, flagStatus: flag.flagStatus } };
  }

  /**
   * A participant's history for `me`, as `GET /api/participants/{participantId}/notes?before=` answers it: up to 30
   * live notes dated before `before`, tomorrow by default, newest first, with `hasMore`, and `flagStatus` on a
   * manager's rows alone.
   */
  function history(me: Me, participantId: string, before: string | null) {
    if (before !== null && !/^\d{4}-\d{2}-\d{2}$/.test(before)) {
      return { status: 400 };
    }

    const until = before ?? daysAfter(today, 1);
    const ofParticipant = (key: string) => key.startsWith(`${participantId}/`) && key.slice(-10) < until;
    const rows = [
      ...[...drafts].filter(([key]) => ofParticipant(key)).map(([key, held]) => ({
        noteDate: key.slice(-10),
        status: 'Draft',
        authorDisplayName: held.authorDisplayName,
        isFlagged: false,
        flagStatus: 'None',
      })),
      ...[...submitted].filter(([key]) => ofParticipant(key)).map(([key, held]) => ({
        noteDate: key.slice(-10),
        status: 'Submitted',
        authorDisplayName: held.authorDisplayName,
        isFlagged: held.isFlagged,
        flagStatus: held.flagStatus,
      })),
    ].sort((a, b) => b.noteDate.localeCompare(a.noteDate));
    const notes = rows
      .slice(0, pageSize)
      .map(({ flagStatus, ...row }) => (me.role === 'Manager' ? { ...row, flagStatus } : row));
    return { json: { notes, hasMore: rows.length > pageSize } };
  }

  /** The answer to one request of the page's, as `me`'s. */
  function answer(me: Me, method: string, url: URL, body: () => unknown, idempotencyKey: string | undefined) {
    const { pathname } = url;
    const [, headerId] = headerPath.exec(pathname) ?? [];
    if (headerId !== undefined) {
      const header = headers.get(headerId);
      return header === undefined ? { status: 404 } : { json: header };
    }

    const [, historyOf] = historyPath.exec(pathname) ?? [];
    if (historyOf !== undefined) {
      return headers.has(historyOf) ? history(me, historyOf, url.searchParams.get('before')) : { status: 404 };
    }

    const [, noteOf, noteDateOf = ''] = notePath.exec(pathname) ?? [];
    if (noteOf !== undefined) {
      return note(me, noteOf, noteDateOf);
    }

    const [, submittedOf, submittedDate = ''] = versionsPath.exec(pathname) ?? [];
    if (submittedOf !== undefined) {
      return submit(me, submittedOf, submittedDate, idempotencyKey, body() as VersionBody);
    }

    const [, discardedOf, discardedDate = ''] = discardPath.exec(pathname) ?? [];
    if (discardedOf !== undefined) {
      return discard(me, discardedOf, discardedDate);
    }

    const [, participantId = '', noteDate = ''] = draftPath.exec(pathname) ?? [];
    if (!headers.has(participantId)) {
      return { status: 404 };
    }

    return method === 'PUT'
      ? save(me, participantId, noteDate, body() as DraftSave)
      : form(me, participantId, noteDate);
  }

  return {
    /**
     * Answers `page`'s requests for a participant's header, history and notes from now on as `me`'s, each `latencyMs`
     * after it is sent, as on a slow network. A later call overrides it, as Playwright tries the latest route first.
     */
    serve: (page: Page, me: Me, { latencyMs = 0 } = {}) =>
      page.route(
        ({ pathname }) => notePaths.some((path) => path.test(pathname)),
        async (route: Route) => {
          const request = route.request();
          await wait(latencyMs);
          const url = new URL(request.url());
          const answered = answer(
            me,
            request.method(),
            url,
            () => request.postDataJSON(),
            request.headers()['idempotency-key'],
          );
          if (versionsPath.test(url.pathname) && loseNextSubmitAnswer) {
            // The version is kept, as the server kept it, but its answer never reaches the page.
            loseNextSubmitAnswer = false;
            await route.abort('internetdisconnected');
            return;
          }

          await route.fulfill(answered);
        },
      ),
    /** Every save that the pages sent, in order, whatever it was answered. */
    saves,
    /** Every Submit that the pages sent, in order, whatever it was answered. */
    submits,
    /** The working copy of a participant's note for a day, as the last save left it, if there is one. */
    draft: (participantId: string, noteDate: string) => drafts.get(`${participantId}/${noteDate}`),
    /** A participant's submitted note for a day, with its version, if there is one. */
    submitted: (participantId: string, noteDate: string) => submitted.get(`${participantId}/${noteDate}`),
    /**
     * The drafts of a participant's note for a day that were discarded, oldest first, each as its last save left it,
     * with who discarded it.
     */
    discarded: (participantId: string, noteDate: string) => discarded.get(`${participantId}/${noteDate}`) ?? [],
    /**
     * `me`'s unfinished drafts as `GET /api/me/drafts` lists them, for `serveMyDrafts`, as they stand when the list is
     * read: each of their own drafts, whatever its date and whether or not its participant is archived since, with the
     * participant's names and the time of its last save, in the order the store holds them, as the API promises none.
     * The store keeps no pending edits, which T14 builds, so a test gives their rows itself.
     */
    myDrafts: (me: Me): DraftsOf => () =>
      [...drafts]
        .filter(([, draft]) => draft.authorUserId === me.userId)
        .flatMap(([key, draft]): MyDraft[] => {
          const header = headers.get(key.slice(0, -11));
          if (header === undefined) {
            return [];
          }

          const { id: participantId, givenName, familyName } = header;
          const noteDate = key.slice(-10);
          return [{ participantId, givenName, familyName, noteDate, kind: 'draft', savedAtUtc: draft.savedAtUtc }];
        }),
    /** Each participant's note today as `me` sees it on Today, for `serveToday`, as it stands when the list is read. */
    todayNotes: (me: Me): NoteOf => (participantId) => {
      const key = `${participantId}/${today}`;
      const draft = drafts.get(key);
      if (draft !== undefined) {
        const { authorDisplayName, startedAtUtc } = draft;
        const isMine = draft.authorUserId === me.userId;
        const saved = isMine ? { savedAtUtc: draft.savedAtUtc } : {};
        return { status: 'Draft', authorDisplayName, isMine, startedAtUtc, ...saved, isFlagged: false };
      }

      const held = submitted.get(key);
      if (held === undefined) {
        return null;
      }

      const { authorDisplayName, startedAtUtc, firstSubmittedAtUtc, isFlagged } = held;
      const isMine = held.authorUserId === me.userId;
      return { status: 'Submitted', authorDisplayName, isMine, startedAtUtc, firstSubmittedAtUtc, isFlagged };
    },
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
    /**
     * Starts a participant's note for a day on another page, as another user, or as the user whose ID is given, such as
     * the person signed in, on the day before, whose draft it then is, with an empty box.
     */
    startElsewhere: (participantId: string, noteDate: string, startedBy: StartedBy, authorUserId = 'another user') => {
      drafts.set(`${participantId}/${noteDate}`, {
        ...startedBy,
        authorUserId,
        snapshot: [...(goals.get(participantId) ?? [])],
        ticked: [],
        groups: [...commonItemGroups],
        picked: [],
        tickedItems: [],
        narrative: '',
        isFlagged: false,
        flagReason: null,
        savedAtUtc: startedBy.startedAtUtc,
        lastClientId: randomUUID(),
        lastSeq: 1,
      });
    },
    /**
     * Saves the draft of a participant's note for a day as its author does from another tab or device, with what that
     * page holds, the rest kept: a page's first save, under a `clientId` of its own, which takes the working copy over,
     * so that the next save of a page that saved it before meets `409 draft.taken_over`.
     */
    saveElsewhere: (participantId: string, noteDate: string, elsewhere: SavedElsewhere) => {
      const key = `${participantId}/${noteDate}`;
      const draft = drafts.get(key);
      if (draft === undefined) {
        throw new Error(`No draft of ${key} is held.`);
      }

      const isFlagged = elsewhere.isFlagged ?? draft.isFlagged;
      Object.assign(draft, {
        ticked: elsewhere.tickedGoalIds ?? draft.ticked,
        picked: elsewhere.pickedGroupIds ?? draft.picked,
        tickedItems: elsewhere.tickedCommonItemIds ?? draft.tickedItems,
        narrative: elsewhere.narrative ?? draft.narrative,
        isFlagged,
        flagReason: isFlagged ? (elsewhere.flagReason ?? draft.flagReason) : null,
        savedAtUtc: elsewhere.savedAtUtc ?? savedAtUtc,
        lastClientId: randomUUID(),
        lastSeq: 1,
      });
    },
    /** Submits the draft of a participant's note for a day as its author does in another tab, with what it holds. */
    submitElsewhere: (participantId: string, noteDate: string) => {
      const key = `${participantId}/${noteDate}`;
      const draft = drafts.get(key);
      if (draft === undefined) {
        throw new Error(`No draft of ${key} is held.`);
      }

      submitted.set(key, {
        authorUserId: draft.authorUserId,
        authorDisplayName: draft.authorDisplayName,
        startedAtUtc: draft.startedAtUtc,
        firstSubmittedAtUtc: savedAtUtc,
        idempotencyKey: randomUUID(),
        currentVersionNumber: 1,
        narrative: draft.narrative,
        goals: draft.snapshot.map(({ id, text }) => ({ text, isTicked: draft.ticked.includes(id) })),
        commonItemGroups: readGroupsOf(draft.groups, draft.picked, draft.tickedItems),
        isFlagged: draft.isFlagged,
        flagReason: draft.flagReason,
        ...flagOf(draft.flagReason, savedAtUtc),
      });
      drafts.delete(key);
    },
    /**
     * Puts a submitted note of a participant's for a day in the store, as if its author had submitted it, and, with
     * `firstFlagReason`, then saved a version 2 over it.
     */
    addSubmitted: (participantId: string, noteDate: string, added: SubmittedNote) => {
      const { flagReason = null, firstFlagReason } = added;
      const flag = flagOf(firstFlagReason ?? flagReason, added.firstSubmittedAtUtc);
      const flagStatus = added.flagStatus ?? flag.flagStatus;
      if (flagStatus === 'ToReview' && flag.flaggedVersion === null) {
        throw new Error('A note in To review has a flagged version: give it a flagReason or a firstFlagReason.');
      }

      submitted.set(`${participantId}/${noteDate}`, {
        authorDisplayName: added.authorDisplayName,
        authorUserId: added.authorUserId ?? 'another user',
        startedAtUtc: added.firstSubmittedAtUtc,
        firstSubmittedAtUtc: added.firstSubmittedAtUtc,
        idempotencyKey: randomUUID(),
        currentVersionNumber: firstFlagReason === undefined ? 1 : 2,
        narrative: added.narrative ?? '',
        goals: added.goals ?? [],
        commonItemGroups: [],
        isFlagged: flagReason !== null,
        flagReason,
        flagStatus,
        flaggedVersion: flag.flaggedVersion,
      });
    },
    /**
     * The To review list as `GET /api/reviews?status=toReview` answers it (design.md §6.4), as the store stands when
     * it is asked, for `serveReviews`: each submitted note in To review, an archived participant's too, with its
     * participant's ID and names, its date, its author, the reason and time of its flagged version, its current
     * version's number and whether that version is not flagged; the oldest flag first, then by participant and date,
     * in place of the note's ID.
     */
    toReview: (): ToReviewRow[] =>
      [...submitted]
        .flatMap(([key, held]) => {
          const header = headers.get(key.slice(0, -11));
          if (header === undefined || held.flagStatus !== 'ToReview' || held.flaggedVersion === null) {
            return [];
          }

          return [{
            participantId: header.id,
            givenName: header.givenName,
            familyName: header.familyName,
            noteDate: key.slice(-10),
            authorDisplayName: held.authorDisplayName,
            flagReason: held.flaggedVersion.flagReason,
            flaggedAtUtc: held.flaggedVersion.createdAtUtc,
            currentVersionNumber: held.currentVersionNumber,
            flagRemovedLater: !held.isFlagged,
          }];
        })
        .sort(
          (a, b) =>
            Date.parse(a.flaggedAtUtc) - Date.parse(b.flaggedAtUtc)
            || `${a.participantId}/${a.noteDate}`.localeCompare(`${b.participantId}/${b.noteDate}`),
        ),
    /**
     * Keeps the version that the next Submit makes, as the server does, but aborts its answer, as a phone that loses
     * its signal while the answer is on its way.
     */
    loseNextSubmitAnswer: () => {
      loseNextSubmitAnswer = true;
    },
  };
}

/** What `notesStore` gives. */
export type NotesStore = ReturnType<typeof notesStore>;

/** Whether the request is a save of a note's working copy, `PUT {base}/draft`. */
export const isDraftSaveRequest = (request: Request) =>
  request.method() === 'PUT' && draftPath.test(new URL(request.url()).pathname);

/** A pattern for the next request for a note's working copy, its read or its save, for `holdNext`. */
export const draftRequests = '**/api/participants/*/notes/*/draft';

/**
 * Aborts each save of a note's working copy, `PUT {base}/draft`, that the page sends from now on, in the browser, as a
 * phone that loses its signal on the way would, so that autosave retries it; its reads go on as before. A later `serve`
 * of a store overrides it.
 */
export async function failDraftSaves(page: Page): Promise<void> {
  await page.route(
    ({ pathname }) => draftPath.test(pathname),
    (route) => (route.request().method() === 'PUT' ? route.abort('internetdisconnected') : route.fallback()),
  );
}

/** Whether `pathname` is one that `failNoteReads` fails. */
const isNoteRead = (pathname: string) =>
  [headerPath, historyPath, notePath, draftPath].some((path) => path.test(pathname));

/**
 * Aborts each `GET` of a participant's header, of their history, of their note and of its working copy that the page
 * sends from now on, in the browser, as a phone with no signal would; with `olderOnly`, only each page of the history
 * after the first, which Show older asks for with `before`. A failure of the first loads then shows about 1 s after
 * the page opens, after the query client's silent retry. A later `serve` of a store overrides it.
 */
export async function failNoteReads(page: Page, { olderOnly = false } = {}): Promise<void> {
  await page.route(
    (url) => (olderOnly ? historyPath.test(url.pathname) && url.searchParams.has('before') : isNoteRead(url.pathname)),
    (route) => (route.request().method() === 'GET' ? route.abort('internetdisconnected') : route.fallback()),
  );
}
