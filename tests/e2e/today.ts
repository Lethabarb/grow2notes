import type { Page } from '@playwright/test';
import type { AdminParticipant } from './participants.ts';

/**
 * A participant on Today, as `GET /api/today` lists them (design.md §6.3): their ID and their names as stored, and no
 * note today.
 */
export type TodayParticipant = Pick<AdminParticipant, 'id' | 'givenName' | 'familyName'> & { note: null };

/** The Today list, as `GET /api/today` answers it: the server's date in Melbourne, and the participants on it. */
export type TodayList = { date: string; participants: TodayParticipant[] };

// Today's one read.
const todayRequest = '**/api/today';

/**
 * The list for `date` as the API gives it: the active ones of `participants`, made-up people such as participants.ts's,
 * each as their ID and names, with no note, in the order given, which the API's is, by family name, then given name,
 * then ID.
 */
export const todayList = (date: string, participants: readonly AdminParticipant[]): TodayList => ({
  date,
  participants: participants
    .filter((participant) => participant.status === 'Active')
    .map(({ id, givenName, familyName }) => ({ id, givenName, familyName, note: null })),
});

/**
 * Answers each `GET /api/today` that the page sends from now on, in the browser, with `todayList(date, participants)`.
 * It overrides the empty list that `serveMe` gives, as Playwright tries the latest route first, so call it after
 * `serveMe`.
 */
export async function serveToday(page: Page, date: string, participants: readonly AdminParticipant[]): Promise<void> {
  const list = todayList(date, participants);
  await page.route(todayRequest, (route) => route.fulfill({ json: list }));
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
