import type { Page } from '@playwright/test';
import type { AdminParticipant } from './participants.ts';

/**
 * A participant's live note for the list's date, as `GET /api/today` gives it on their row (design.md §6.3): its
 * status and author, whether the signed-in user wrote it, when it was started, the time of the author's last save on
 * their own draft alone, the first submitted time on a submitted note alone, and whether its current version is
 * flagged, which a draft never is. Each time is in UTC, written with `Z`.
 */
export type TodayNote = {
  status: 'Draft' | 'Submitted';
  authorDisplayName: string;
  isMine: boolean;
  startedAtUtc: string;
  savedAtUtc?: string;
  firstSubmittedAtUtc?: string;
  isFlagged: boolean;
};

/** Each participant's note on Today, by their ID, or `null` for none, read as each list is answered. */
export type NoteOf = (participantId: string) => TodayNote | null;

/**
 * A participant on Today, as `GET /api/today` lists them (design.md §6.3): their ID and their names as stored, and
 * their note today, if any.
 */
export type TodayParticipant = Pick<AdminParticipant, 'id' | 'givenName' | 'familyName'> & { note: TodayNote | null };

/** The Today list, as `GET /api/today` answers it: the server's date in Melbourne, and the participants on it. */
export type TodayList = { date: string; participants: TodayParticipant[] };

/**
 * One of the signed-in user's own unfinished drafts, as `GET /api/me/drafts` lists it (design.md §6.3): its
 * participant's ID and names as stored, archived since or not; its note's date; whether it is a draft or a pending edit
 * on a submitted note; and when it was last saved, in UTC, written with `Z`.
 */
export type MyDraft = {
  participantId: string;
  givenName: string;
  familyName: string;
  noteDate: string;
  kind: 'draft' | 'pendingEdit';
  savedAtUtc: string;
};

/** The signed-in user's unfinished drafts, read as each list is answered. */
export type DraftsOf = () => readonly MyDraft[];

// Today's two reads: the day's list, and the caller's unfinished drafts.
const todayRequest = '**/api/today';
const myDraftsRequest = '**/api/me/drafts';

/** No participant has a note today. */
const noNotes: NoteOf = () => null;

/**
 * The list for `date` as the API gives it: the active ones of `participants`, made-up people such as participants.ts's,
 * each as their ID and names, with the note that `noteOf` gives them, by default none, in the order given, which the
 * API's is, by family name, then given name, then ID.
 */
export const todayList = (
  date: string,
  participants: readonly AdminParticipant[],
  noteOf: NoteOf = noNotes,
): TodayList => ({
  date,
  participants: participants
    .filter((participant) => participant.status === 'Active')
    .map(({ id, givenName, familyName }) => ({ id, givenName, familyName, note: noteOf(id) })),
});

/**
 * Answers each `GET /api/today` that the page sends from now on, in the browser, with
 * `todayList(date, participants, noteOf)`, worked out afresh for each, so that the notes of a store in memory, such as
 * `notesStore`'s `todayNotes(me)`, show as they stand when the page asks. It overrides the empty list that `serveMe`
 * gives, as Playwright tries the latest route first, so call it after `serveMe`.
 */
export async function serveToday(
  page: Page,
  date: string,
  participants: readonly AdminParticipant[],
  noteOf: NoteOf = noNotes,
): Promise<void> {
  await page.route(todayRequest, (route) => route.fulfill({ json: todayList(date, participants, noteOf) }));
}

/**
 * Answers each `GET /api/me/drafts` that the page sends from now on, in the browser, with what `draftsOf` gives then,
 * such as `notesStore`'s `myDrafts(me)`, so that Your unfinished drafts shows the drafts of a store in memory as they
 * stand when the page asks. It overrides the empty list that `serveMe` gives, as `serveToday` does, so call it after
 * `serveMe`.
 */
export async function serveMyDrafts(page: Page, draftsOf: DraftsOf): Promise<void> {
  await page.route(myDraftsRequest, (route) => route.fulfill({ json: draftsOf() }));
}

/**
 * Fails each `GET /api/today` that the page sends from now on, in the browser: `'no connection'` aborts it, as a phone
 * with no signal would, and a status answers it with that status and no body, such as `503` for a server problem. The
 * failure shows about 1 s after the request, after the query client's silent retry. A later `serveToday` overrides it.
 */
export async function failToday(page: Page, failure: 'no connection' | number): Promise<void> {
  await page.route(todayRequest, (route) =>
    failure === 'no connection' ? route.abort('internetdisconnected') : route.fulfill({ status: failure }),
  );
}
