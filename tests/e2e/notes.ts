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
  /** 0 for a draft, and the version being edited for a pending edit. */
  baseVersion: number;
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

/** A Submit or Save changes that the page sent: its `Idempotency-Key` and its body. */
export type VersionPost = { idempotencyKey: string | undefined; body: VersionBody };

/** A note's flag status, as the API names it (design.md §5.3), which Submit's answer and a manager's history carry. */
export type FlagStatus = 'None' | 'ToReview' | 'Reviewed';

/** A review as the store keeps it and a manager's `GET {base}` lists it (design.md §6.3): newest first. */
export type StoredReview = {
  versionNumber: number;
  reviewedBy: { id: string; displayName: string };
  reviewedAtUtc: string;
  comment: string | null;
};

/** A Mark reviewed that the page sent: its JSON body (design.md §6.4). */
export type ReviewBody = { versionNumber: number; comment: string | null };

/** A submitted note to put in the store, as `addSubmitted` takes it: who wrote it, when, and its version 1. */
export type SubmittedNote = {
  authorDisplayName: string;
  /** The author's user ID, someone else's by default. */
  authorUserId?: string;
  firstSubmittedAtUtc: string;
  narrative?: string;
  /** Every goal of its snapshot, in order, with its tick; an ID by default made from its place in the list. */
  goals?: { id?: string; text: string; isTicked: boolean }[];
  /** The reason of a flagged version; an unflagged one has none. */
  flagReason?: string;
  /**
   * The reason of version 1, flagged, when a version 2 has been saved over it, as Save changes saves one, which
   * removed the flag or changed its reason: the note stays in To review on version 1's reason and time (A15),
   * and reads as version 2, with `flagReason`, if any, as its own.
   */
  firstFlagReason?: string;
  /** Set by hand; by default `ToReview` for a note with a flagged version, as Submit leaves it. */
  flagStatus?: FlagStatus;
  /** Its reviews, newest first, for a note given the `Reviewed` status by hand; none by default. */
  reviews?: StoredReview[];
  /**
   * Who saved the note's current version, and when, for a note that has been edited: it then reads as version 2 with
   * the Edited line. By default its author, when it was first submitted, for a note with a `firstFlagReason`, which
   * has a version 2; none, one version, otherwise.
   */
  lastEdit?: { by: string; atUtc: string };
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

/**
 * A pending edit as the store keeps it: a working copy of a submitted note's current version, one for each editor,
 * based on the version it was made from and rebased by Start again; `authorUserId` is the editor.
 */
type PendingEdit = HeldDraft & { noteKey: string; baseVersion: number };

/** A version saved with an `Idempotency-Key`, which the same key finds again. */
type SavedVersion = { noteKey: string; versionNumber: number; createdAtUtc: string };

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
  /** The snapshot that every version of the note shares, and the current version's ticks and picks on it. */
  snapshot: NoteGoal[];
  groups: NoteGroup[];
  ticked: string[];
  picked: string[];
  tickedItems: string[];
  narrative: string;
  goals: { text: string; isTicked: boolean }[];
  commonItemGroups: ReturnType<typeof readGroupsOf>;
  isFlagged: boolean;
  flagReason: string | null;
  flagStatus: FlagStatus;
  flaggedVersion: FlaggedVersion | null;
  /** Every review of the note, newest first. */
  reviews: StoredReview[];
  /** Who saved the current version, and when, once it is version 2 or later; `null` while it is version 1. */
  lastEdit: { by: string; atUtc: string } | null;
};

/** Whether `me` may edit a submitted note: its author, or a manager (design.md §2, D19). */
const mayEdit = (me: Me, held: Pick<HeldSubmitted, 'authorUserId'>) =>
  held.authorUserId === me.userId || me.role === 'Manager';

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
// the form reads, autosave puts and Cancel deletes; its versions, which Submit and Save changes post to; its
// discard; and its reviews, which Mark reviewed posts to.
const headerPath = /^\/api\/participants\/([^/]+)$/;
const historyPath = /^\/api\/participants\/([^/]+)\/notes$/;
const notePath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)$/;
const draftPath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)\/draft$/;
const versionsPath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)\/versions$/;
const discardPath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)\/discard$/;
const reviewsPath = /^\/api\/participants\/([^/]+)\/notes\/([^/]+)\/reviews$/;
const notePaths = [headerPath, historyPath, notePath, draftPath, versionsPath, discardPath, reviewsPath];

/** Whether a request is for the To review list, `GET /api/reviews?status=toReview`. */
const isToReviewList = (pathname: string, search: string) =>
  pathname === '/api/reviews' && new URLSearchParams(search).get('status') === 'toReview';

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
 * before yesterday (design.md A65); for a submitted note, `403` to a worker who did not write it, the caller's own
 * pending edit, or the current version as a template at its base; autosave's `PUT`, which creates the note with the
 * creating save's checks, or updates the caller's own draft as design.md §5.6 numbers a page's saves, by `clientId`
 * and `seq`, or creates or updates the caller's pending edit of a submitted note at the base the note has for them;
 * Submit's and Save changes' `POST {base}/versions`, with their checks in the API's order, the first of which puts a
 * flagged version 1 in To review and the second saves version n + 1 over version n; Cancel's `DELETE {base}/draft`,
 * which deletes the caller's own pending edit; and Discard draft's `POST {base}/discard`, with its checks in the
 * API's order, which hides the draft from every list and screen and frees the day for a new note.
 * `serve` answers one page's requests as one person's, `myDrafts` gives a person's unfinished drafts and `toReview`
 * the To review list.
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
  let loseNextReviewAnswer = false;
  const headers = new Map(participants.map(({ id, givenName, familyName, status }) => [
    id,
    { id, givenName, familyName, status },
  ]));
  const goals = new Map(participants.map(({ id }) => [id, (firstGoals[id] ?? []).map(goalOf)]));
  const drafts = new Map<string, HeldDraft>();
  const submitted = new Map<string, HeldSubmitted>();
  const edits = new Map<string, PendingEdit>();
  const versionKeys = new Map<string, SavedVersion>();
  const discarded = new Map<string, DiscardedDraft[]>();
  const saves: DraftSave[] = [];
  const submits: VersionPost[] = [];
  const reviewRequests: ReviewBody[] = [];
  const cancels: { participantId: string; noteDate: string; userId: string }[] = [];

  /** The key of `me`'s pending edit of the note at `noteKey`. */
  const editKey = (noteKey: string, me: Me) => `${noteKey}/${me.userId}`;

  /** The `409 note.version_conflict` of a note's current version, naming who saved it and when, if it has one. */
  function conflictOf(held: HeldSubmitted | undefined) {
    return held === undefined
      ? problem(409, 'note.version_conflict')
      : problem(409, 'note.version_conflict', {
          currentVersion: held.currentVersionNumber,
          createdBy: held.lastEdit?.by ?? held.authorDisplayName,
          createdAtUtc: held.lastEdit?.atUtc ?? held.firstSubmittedAtUtc,
        });
  }

  /**
   * How a working copy numbers a page's saves (design.md §5.6): the held time for a save sent again, `409
   * draft.taken_over` for another page's later save but its first, and `null` for a save that applies.
   */
  function numbered(held: HeldDraft, sent: DraftSave) {
    if (held.lastClientId === sent.clientId) {
      // The page's save sent again after its answer was lost, or one that a later save of the page overtook: the time
      // of the save that the working copy holds, and nothing changed.
      return sent.seq <= held.lastSeq ? { status: 200, json: { savedAtUtc: held.savedAtUtc } } : null;
    }

    // Another page has saved since this one last did, and only a page's first save takes the working copy over.
    return sent.seq === 1 ? null : problem(409, 'draft.taken_over');
  }

  /** The current version's content as a working copy holds it, for a pending edit to begin from. */
  const contentOf = (held: HeldSubmitted) => ({
    snapshot: [...held.snapshot],
    groups: [...held.groups],
    ticked: [...held.ticked],
    picked: [...held.picked],
    tickedItems: [...held.tickedItems],
    narrative: held.narrative,
    isFlagged: held.isFlagged,
    flagReason: held.flagReason,
  });

  /**
   * The note at `{base}`, as `GET {base}` answers it to `me`: a submitted note's current version, with its flag
   * status and reviews for a manager alone; a draft's header,
   * who started it and when, and, to a manager alone, its author's working copy in the same shape as a version, every
   * goal of its snapshot in order with its tick; or `404` for none, a discarded draft's day included.
   */
  function note(me: Me, participantId: string, noteDate: string) {
    const key = `${participantId}/${noteDate}`;
    const held = submitted.get(key);
    if (held !== undefined && headers.has(participantId)) {
      const { authorDisplayName, firstSubmittedAtUtc, narrative, isFlagged, flagReason, goals: ticks } = held;
      const author = { id: held.authorUserId, displayName: authorDisplayName };
      const current = {
        versionNumber: held.currentVersionNumber,
        narrative,
        isFlagged,
        flagReason,
        goals: ticks,
        commonItemGroups: held.commonItemGroups,
      };
      const edit = held.lastEdit === null ? { isEdited: false } : { isEdited: true, lastEdit: held.lastEdit };
      // A manager alone is given the review status and every review, newest first.
      const review = me.role === 'Manager' ? { flagStatus: held.flagStatus, reviews: held.reviews } : {};
      return { json: { status: 'Submitted', author, firstSubmittedAtUtc, ...edit, ...review, current } };
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
    const held = submitted.get(`${participantId}/${noteDate}`);
    if (held !== undefined) {
      // A submitted note: only its author or a manager has a form, the caller's own pending edit if they have one, and
      // else the current version as a template, at its base, which the first save makes a pending edit.
      if (!mayEdit(me, held)) {
        return problem(403, 'note.not_editable');
      }

      const edit = edits.get(editKey(`${participantId}/${noteDate}`, me));
      const content = edit ?? contentOf(held);
      const saved = edit === undefined ? {} : { savedAtUtc: edit.savedAtUtc };
      const json = {
        baseVersion: edit?.baseVersion ?? held.currentVersionNumber,
        isSaved: edit !== undefined,
        guidePrompts: prompts,
        goals: content.snapshot.map((goal) => ({ ...goal, isTicked: content.ticked.includes(goal.id) })),
        commonItemGroups: formGroupsOf(content.groups, content.picked, content.tickedItems),
        narrative: content.narrative,
        isFlagged: content.isFlagged,
        flagReason: content.flagReason,
        ...saved,
      };
      return { json };
    }

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

    if (noteDate > today || (noteDate < daysAfter(today, -1) && me.role !== 'Manager')) {
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
    const held = submitted.get(key);
    if (held !== undefined) {
      return saveEdit(me, key, held, sent, ticked, flag);
    }

    if (draft !== undefined) {
      if (draft.authorUserId !== me.userId) {
        const { authorDisplayName, startedAtUtc } = draft;
        return problem(409, 'note.in_progress_by_other', { authorDisplayName, startedAtUtc });
      }

      // A draft's base is 0, and anything else is a pending edit's page (§6.3).
      if (sent.baseVersion !== 0) {
        return problem(409, 'note.version_conflict');
      }

      const numbering = numbered(draft, sent);
      if (numbering !== null) {
        return numbering;
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

    // With no live note, only a base of 0 is one the note has.
    if (sent.baseVersion !== 0) {
      return problem(409, 'note.version_conflict');
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

  /**
   * A save of `me`'s pending edit of a submitted note, as `PUT {base}/draft` answers it: `403 note.not_editable` for a
   * worker who did not write it; with no pending edit yet, one made from the current version when the base is its
   * number, and else `409 note.version_conflict`; and, with one, a save at its own base, or at the current version's,
   * which is Start again, and so moves the base, and else a conflict, before its saves are numbered (A70).
   */
  function saveEdit(
    me: Me,
    key: string,
    held: HeldSubmitted,
    sent: DraftSave,
    ticked: string[],
    flag: Pick<HeldDraft, 'isFlagged' | 'flagReason'>,
  ) {
    if (!mayEdit(me, held)) {
      return problem(403, 'note.not_editable');
    }

    const mine = editKey(key, me);
    const edit = edits.get(mine);
    const base = sent.baseVersion;
    // The base must be one the note has for the caller: the current version's, or a pending edit's own.
    const isCurrent = base === held.currentVersionNumber;
    if (!isCurrent && (edit === undefined || base !== edit.baseVersion)) {
      return problem(409, 'note.version_conflict');
    }

    const numbering = edit === undefined ? null : numbered(edit, sent);
    if (numbering !== null) {
      return numbering;
    }

    const content = edit ?? contentOf(held);
    if (!ticked.every((id) => content.snapshot.some((goal) => goal.id === id))) {
      return refused('tickedGoalIds');
    }

    const refusal = refusedTicks(content.groups, sent);
    if (refusal !== null) {
      return refusal;
    }

    const changes = {
      ticked,
      picked: [...new Set(sent.pickedGroupIds)],
      tickedItems: [...new Set(sent.tickedCommonItemIds)],
      narrative: sent.narrative,
      ...flag,
      savedAtUtc,
      lastClientId: sent.clientId,
      lastSeq: sent.seq,
      baseVersion: base,
    };
    if (edit === undefined) {
      edits.set(mine, {
        ...content,
        authorUserId: me.userId,
        authorDisplayName: me.displayName,
        startedAtUtc: savedAtUtc,
        noteKey: key,
        ...changes,
      });
    } else {
      Object.assign(edit, changes);
    }

    return { status: 200, json: { savedAtUtc } };
  }

  /** Cancel, as `DELETE {base}/draft` answers it: `204` for `me`'s own pending edit, deleted, and else `404`. */
  function cancel(me: Me, participantId: string, noteDate: string) {
    cancels.push({ participantId, noteDate, userId: me.userId });
    if (!headers.has(participantId)) {
      return { status: 404 };
    }

    return edits.delete(editKey(`${participantId}/${noteDate}`, me)) ? { status: 204 } : { status: 404 };
  }

  /**
   * Submit and Save changes, as `POST {base}/versions` answers them, with their checks in the order of T09's Notes and
   * T14's: the body's, `404`, another user's draft, `403` for a submitted note that the caller may not edit, a key
   * used before, a base that is not the note's current version, the flag's reason, the caller's working copy at that
   * base, and its snapshot's checks.
   */
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
    if (!Number.isInteger(sent.baseVersion) || sent.baseVersion < 0) {
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

    if (held !== undefined && !mayEdit(me, held)) {
      return problem(403, 'note.not_editable');
    }

    // The note's flag status as it is now, as the key finds the version, not the status its save set.
    const version = versionKeys.get(idempotencyKey);
    if (version !== undefined) {
      if (version.noteKey !== key || held === undefined) {
        return refused('idempotencyKey');
      }

      const { versionNumber, createdAtUtc } = version;
      return { status: 200, json: { versionNumber, createdAtUtc, ...flagStatusOf(me, held.flagStatus) } };
    }

    if (sent.baseVersion !== (held?.currentVersionNumber ?? 0)) {
      return conflictOf(held);
    }

    if (sent.isFlagged && isBlank(sent.flagReason)) {
      return refused('flagReason');
    }

    // The caller's working copy at the request's base: their draft, or their pending edit of that version.
    const working = held === undefined ? draft : edits.get(editKey(key, me));
    if (working === undefined || ('baseVersion' in working && working.baseVersion !== sent.baseVersion)) {
      return conflictOf(held);
    }

    const ticked = new Set(sent.tickedGoalIds);
    if (![...ticked].every((id) => working.snapshot.some((goal) => goal.id === id))) {
      return refused('tickedGoalIds');
    }

    const refusal = refusedTicks(working.groups, sent);
    if (refusal !== null) {
      return refusal;
    }

    const flagReason = sent.isFlagged ? sent.flagReason : null;
    const content = {
      snapshot: [...working.snapshot],
      groups: [...working.groups],
      ticked: [...ticked],
      picked: [...new Set(sent.pickedGroupIds)],
      tickedItems: [...new Set(sent.tickedCommonItemIds)],
      narrative: sent.narrative,
      goals: working.snapshot.map(({ id, text }) => ({ text, isTicked: ticked.has(id) })),
      commonItemGroups: readGroupsOf(working.groups, sent.pickedGroupIds, sent.tickedCommonItemIds),
      isFlagged: sent.isFlagged,
      flagReason,
    };
    const versionNumber = sent.baseVersion + 1;
    versionKeys.set(idempotencyKey, { noteKey: key, versionNumber, createdAtUtc: savedAtUtc });
    if (held === undefined) {
      const flag = flagOf(flagReason, savedAtUtc);
      submitted.set(key, {
        authorUserId: working.authorUserId,
        authorDisplayName: working.authorDisplayName,
        startedAtUtc: working.startedAtUtc,
        firstSubmittedAtUtc: savedAtUtc,
        idempotencyKey,
        currentVersionNumber: 1,
        lastEdit: null,
        reviews: [],
        ...content,
        ...flag,
      });
      drafts.delete(key);
      return { status: 201, json: { versionNumber, createdAtUtc: savedAtUtc, ...flagStatusOf(me, flag.flagStatus) } };
    }

    // A version over the current one, with the flag's rules of design.md §5.8: a flagged version raises the flag when
    // the version before it was not flagged or had another reason, exactly, and else the note's flag status and flagged
    // version are left as they are. An unflagged version never takes a note out of To review, and no review is touched.
    const raisesFlag = sent.isFlagged && (!held.isFlagged || held.flagReason !== flagReason);
    Object.assign(held, content, {
      currentVersionNumber: versionNumber,
      lastEdit: { by: me.displayName, atUtc: savedAtUtc },
    });
    if (raisesFlag) {
      Object.assign(held, { flagStatus: 'ToReview', flaggedVersion: { flagReason, createdAtUtc: savedAtUtc } });
    }
    edits.delete(editKey(key, me));
    return { status: 201, json: { versionNumber, createdAtUtc: savedAtUtc, ...flagStatusOf(me, held.flagStatus) } };
  }

  /**
   * Mark reviewed, as `POST {base}/reviews` answers it (design.md §6.4): `403` for a worker; the body's checks; `404`
   * for no participant or no live note; `409 review.not_current` for a note that is not To review at the version sent,
   * a draft, a note never flagged or already reviewed included; and otherwise `201 {reviewedAtUtc}`, the note Reviewed
   * and the review kept, its comment trimmed and null when blank.
   */
  function review(me: Me, participantId: string, noteDate: string, sent: ReviewBody) {
    reviewRequests.push(sent);
    if (me.role !== 'Manager') {
      return forbidden;
    }

    if (!Number.isInteger(sent.versionNumber) || sent.versionNumber < 1) {
      return refused('versionNumber');
    }

    if (sent.comment !== null && (typeof sent.comment !== 'string' || sent.comment.length > 500)) {
      return refused('comment');
    }

    const key = `${participantId}/${noteDate}`;
    const held = submitted.get(key);
    if (!headers.has(participantId) || (held === undefined && !drafts.has(key))) {
      return { status: 404 };
    }

    if (held?.flagStatus !== 'ToReview' || held.currentVersionNumber !== sent.versionNumber) {
      return problem(409, 'review.not_current');
    }

    const comment = sent.comment === null || isBlank(sent.comment) ? null : sent.comment.trim();
    reviewNote(held, { id: me.userId, displayName: me.displayName }, comment);
    return { status: 201, json: { reviewedAtUtc: savedAtUtc } };
  }

  /** Puts a review of a note's current version on it, newest first, and the note Reviewed. */
  function reviewNote(held: HeldSubmitted, reviewedBy: StoredReview['reviewedBy'], comment: string | null) {
    held.flagStatus = 'Reviewed';
    held.reviews.unshift({
      versionNumber: held.currentVersionNumber,
      reviewedBy,
      reviewedAtUtc: savedAtUtc,
      comment,
    });
  }

  /** The flag status in a version answer, for a manager alone: a worker never learns it. */
  function flagStatusOf(me: Me, flagStatus: string) {
    return me.role === 'Manager' ? { flagStatus } : {};
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
        currentVersionNumber: 0,
        isEdited: false,
        isFlagged: false,
        flagStatus: 'None',
      })),
      ...[...submitted].filter(([key]) => ofParticipant(key)).map(([key, held]) => ({
        noteDate: key.slice(-10),
        status: 'Submitted',
        authorDisplayName: held.authorDisplayName,
        currentVersionNumber: held.currentVersionNumber,
        isEdited: held.currentVersionNumber > 1,
        isFlagged: held.isFlagged,
        flagStatus: held.flagStatus,
      })),
    ].sort((a, b) => b.noteDate.localeCompare(a.noteDate));
    const notes = rows
      .slice(0, pageSize)
      .map(({ flagStatus, ...row }) => (me.role === 'Manager' ? { ...row, flagStatus } : row));
    return { json: { notes, hasMore: rows.length > pageSize } };
  }

  /**
   * The To review list as `GET /api/reviews?status=toReview` answers it (design.md §6.4), as the store stands when it is
   * asked: each submitted note in To review, an archived participant's too, with its participant's ID and names, its
   * date, its author, the reason and time of its flagged version, its current version's number and whether that version
   * is not flagged; the oldest flag first, then by participant and date, in place of the note's ID.
   */
  function toReviewRows(): ToReviewRow[] {
    return [...submitted]
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
      );
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

    const [, reviewedOf, reviewedDate = ''] = reviewsPath.exec(pathname) ?? [];
    if (reviewedOf !== undefined) {
      return review(me, reviewedOf, reviewedDate, body() as ReviewBody);
    }

    const [, participantId = '', noteDate = ''] = draftPath.exec(pathname) ?? [];
    if (!headers.has(participantId)) {
      return { status: 404 };
    }

    if (method === 'DELETE') {
      return cancel(me, participantId, noteDate);
    }

    return method === 'PUT'
      ? save(me, participantId, noteDate, body() as DraftSave)
      : form(me, participantId, noteDate);
  }

  return {
    /**
     * Answers `page`'s requests for a participant's header, history and notes from now on as `me`'s, each `latencyMs`
     * after it is sent, as on a slow network, and, to a manager, the To review list that a To review note's read view
     * asks for. A later call overrides it, as Playwright tries the latest route first.
     */
    serve: (page: Page, me: Me, { latencyMs = 0 } = {}) =>
      page.route(
        ({ pathname, search }) => notePaths.some((path) => path.test(pathname)) || isToReviewList(pathname, search),
        async (route: Route) => {
          const request = route.request();
          await wait(latencyMs);
          const url = new URL(request.url());
          if (isToReviewList(url.pathname, url.search)) {
            await route.fulfill(me.role === 'Manager' ? { json: toReviewRows() } : forbidden);
            return;
          }

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

          if (reviewsPath.test(url.pathname) && loseNextReviewAnswer) {
            // The review is kept, as the server kept it, but its answer never reaches the page.
            loseNextReviewAnswer = false;
            await route.abort('internetdisconnected');
            return;
          }

          await route.fulfill(answered);
        },
      ),
    /** Every save that the pages sent, in order, whatever it was answered. */
    saves,
    /** Every Submit and Save changes that the pages sent, in order, whatever it was answered. */
    submits,
    /** Every Cancel that the pages sent, in order, whatever it was answered. */
    cancels,
    /** Every Mark reviewed that the pages sent, in order, whatever it was answered. */
    reviewRequests,
    /** `me`'s pending edit of a participant's note for a day, as the last save left it, if they have one. */
    pendingEdit: (participantId: string, noteDate: string, me: Me) =>
      edits.get(editKey(`${participantId}/${noteDate}`, me)),
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
     * participant's names and the time of its last save, in the order the store holds them, as the API promises none;
     * and each pending edit of theirs, a manager's of a past day's note included.
     */
    myDrafts: (me: Me): DraftsOf => () => [
      ...[...drafts]
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
      ...[...edits.values()]
        .filter((edit) => edit.authorUserId === me.userId)
        .flatMap((edit): MyDraft[] => {
          const header = headers.get(edit.noteKey.slice(0, -11));
          if (header === undefined) {
            return [];
          }

          const { id: participantId, givenName, familyName } = header;
          const noteDate = edit.noteKey.slice(-10);
          return [{ participantId, givenName, familyName, noteDate, kind: 'pendingEdit', savedAtUtc: edit.savedAtUtc }];
        }),
    ],
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
        snapshot: [...draft.snapshot],
        groups: [...draft.groups],
        ticked: [...draft.ticked],
        picked: [...draft.picked],
        tickedItems: [...draft.tickedItems],
        narrative: draft.narrative,
        goals: draft.snapshot.map(({ id, text }) => ({ text, isTicked: draft.ticked.includes(id) })),
        commonItemGroups: readGroupsOf(draft.groups, draft.picked, draft.tickedItems),
        isFlagged: draft.isFlagged,
        flagReason: draft.flagReason,
        lastEdit: null,
        reviews: [],
        ...flagOf(draft.flagReason, savedAtUtc),
      });
      drafts.delete(key);
    },
    /**
     * Puts a submitted note of a participant's for a day in the store, as if its author had submitted it, and, with
     * `firstFlagReason` or `lastEdit`, then saved a version 2 over it.
     */
    addSubmitted: (participantId: string, noteDate: string, added: SubmittedNote) => {
      const { flagReason = null, firstFlagReason } = added;
      const flag = flagOf(firstFlagReason ?? flagReason, added.firstSubmittedAtUtc);
      const flagStatus = added.flagStatus ?? flag.flagStatus;
      const edited = firstFlagReason !== undefined || added.lastEdit !== undefined;
      const listed = (added.goals ?? []).map((goal, index) => ({ ...goal, id: goal.id ?? `goal-${index + 1}` }));
      if (flagStatus === 'ToReview' && flag.flaggedVersion === null) {
        throw new Error('A note in To review has a flagged version: give it a flagReason or a firstFlagReason.');
      }

      submitted.set(`${participantId}/${noteDate}`, {
        authorDisplayName: added.authorDisplayName,
        authorUserId: added.authorUserId ?? 'another user',
        startedAtUtc: added.firstSubmittedAtUtc,
        firstSubmittedAtUtc: added.firstSubmittedAtUtc,
        idempotencyKey: randomUUID(),
        currentVersionNumber: edited ? 2 : 1,
        snapshot: listed.map(({ id, text }) => ({ id, text })),
        groups: [],
        ticked: listed.filter((goal) => goal.isTicked).map(({ id }) => id),
        picked: [],
        tickedItems: [],
        narrative: added.narrative ?? '',
        goals: listed.map(({ text, isTicked }) => ({ text, isTicked })),
        commonItemGroups: [],
        isFlagged: flagReason !== null,
        flagReason,
        flagStatus,
        flaggedVersion: flag.flaggedVersion,
        reviews: added.reviews ?? [],
        lastEdit: edited
          ? (added.lastEdit ?? { by: added.authorDisplayName, atUtc: added.firstSubmittedAtUtc })
          : null,
      });
    },
    /**
     * Starts `me`'s pending edit of a submitted note on another page, as they had made a first save of it: from the
     * current version, with what that page holds in place of its words, ticks and flag, saved at `savedAtUtc`, which
     * their unfinished drafts list, and opened at the note's edit form.
     */
    startEdit: (participantId: string, noteDate: string, me: Me, elsewhere: SavedElsewhere = {}) => {
      const key = `${participantId}/${noteDate}`;
      const held = submitted.get(key);
      if (held === undefined) {
        throw new Error(`No submitted note of ${key} is held.`);
      }

      const isFlagged = elsewhere.isFlagged ?? held.isFlagged;
      edits.set(editKey(key, me), {
        ...contentOf(held),
        authorUserId: me.userId,
        authorDisplayName: me.displayName,
        startedAtUtc: elsewhere.savedAtUtc ?? savedAtUtc,
        noteKey: key,
        baseVersion: held.currentVersionNumber,
        ticked: elsewhere.tickedGoalIds ?? held.ticked,
        picked: elsewhere.pickedGroupIds ?? held.picked,
        tickedItems: elsewhere.tickedCommonItemIds ?? held.tickedItems,
        narrative: elsewhere.narrative ?? held.narrative,
        isFlagged,
        flagReason: isFlagged ? (elsewhere.flagReason ?? held.flagReason) : null,
        savedAtUtc: elsewhere.savedAtUtc ?? savedAtUtc,
        lastClientId: randomUUID(),
        lastSeq: 1,
      });
    },
    /**
     * Saves a version over a submitted note's current one as another editor does from another page, named `by`, with
     * what that page holds in place of its words and ticks: the note is then a version later, last changed by `by` at
     * `atUtc`, the time of the store's saves by default, and any pending edit of anyone's is left at its base.
     */
    editElsewhere: (
      participantId: string,
      noteDate: string,
      by: string,
      elsewhere: Pick<SavedElsewhere, 'narrative' | 'tickedGoalIds'> & { atUtc?: string } = {},
    ) => {
      const held = submitted.get(`${participantId}/${noteDate}`);
      if (held === undefined) {
        throw new Error('No submitted note is held.');
      }

      const ticked = elsewhere.tickedGoalIds ?? held.ticked;
      Object.assign(held, {
        currentVersionNumber: held.currentVersionNumber + 1,
        narrative: elsewhere.narrative ?? held.narrative,
        ticked,
        goals: held.snapshot.map(({ id, text }) => ({ text, isTicked: ticked.includes(id) })),
        lastEdit: { by, atUtc: elsewhere.atUtc ?? savedAtUtc },
      });
    },
    /**
     * Marks a participant's note for a day reviewed at its current version, as another manager, named `reviewedBy`,
     * does from another page, with `comment`, if any: the note is then Reviewed, and the page that has it open meets
     * `409 review.not_current` when it presses Mark reviewed.
     */
    reviewElsewhere: (
      participantId: string,
      noteDate: string,
      reviewedBy: StoredReview['reviewedBy'],
      comment: string | null = null,
    ) => {
      const held = submitted.get(`${participantId}/${noteDate}`);
      if (held?.flagStatus !== 'ToReview') {
        throw new Error('No submitted note in To review is held.');
      }

      reviewNote(held, reviewedBy, comment);
    },
    /** The To review list, as `serveReviews` gives it, worked out when it is asked. */
    toReview: toReviewRows,
    /**
     * Keeps the version that the next Submit makes, as the server does, but aborts its answer, as a phone that loses
     * its signal while the answer is on its way.
     */
    loseNextSubmitAnswer: () => {
      loseNextSubmitAnswer = true;
    },
    /**
     * Keeps the review that the next Mark reviewed makes, as the server does, but aborts its answer, as a phone that
     * loses its signal while the answer is on its way.
     */
    loseNextReviewAnswer: () => {
      loseNextReviewAnswer = true;
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

/** Whether `pathname` is one that `failEditRequests` fails. */
const isEditRequest = (method: string, pathname: string) =>
  (method === 'POST' && versionsPath.test(pathname)) || (method === 'DELETE' && draftPath.test(pathname));

/**
 * Aborts each Submit or Save changes, `POST {base}/versions`, and each Cancel, `DELETE {base}/draft`, that the page
 * sends from now on, in the browser, as a phone that loses its signal on the way would; its other requests go on as
 * before. A later `serve` of a store overrides it.
 */
export async function failEditRequests(page: Page): Promise<void> {
  await page.route(
    ({ pathname }) => versionsPath.test(pathname) || draftPath.test(pathname),
    (route) => {
      const request = route.request();
      return isEditRequest(request.method(), new URL(request.url()).pathname)
        ? route.abort('internetdisconnected')
        : route.fallback();
    },
  );
}
