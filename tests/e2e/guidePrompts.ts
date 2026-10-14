import type { Page, Route } from '@playwright/test';

/** The design's sketch of the prompts (guide-prompts.md), with a blank line inside them, which is kept too. */
export const savedPrompts = 'Mood and wellbeing today?\nWhat did you do together?\n\nAnything to follow up?';

/** A made-up row version of the organisation, as the API gives it in the `ETag`, without the quotes. */
export const promptsVersion = 'AAAAAAAAB9E=';

/** The row's version once a save has moved it on. */
export const nextVersion = 'AAAAAAAAB9I=';

/** 06:12 UTC on 1 October 2026, as an answer's `Date` gives it: 4:12 pm in Melbourne, before daylight saving starts. */
const savedAt = 'Thu, 01 Oct 2026 06:12:00 GMT';

/** The `GET`'s answer: the prompts as JSON, with the row's version in the `ETag`, as the API gives them. */
export const promptsAnswer = (text: string, version = promptsVersion) => ({
  json: { text },
  headers: { ETag: `"${version}"` },
});

/** The `PUT`'s `200`, as the API gives it: no body, the row's new version in the `ETag`, and the server's `Date`. */
export const savedAnswer = (version = nextVersion, date = savedAt) => ({
  status: 200,
  headers: { ETag: `"${version}"`, Date: date },
});

/**
 * Answers each `GET /api/admin/guide-prompts` that the page sends from now on with `text`, in the browser, as `serveMe`
 * answers `/api/auth/me`, so that the request never reaches the app, where no made-up manager has a session. A later
 * call overrides it, as Playwright tries the latest route first.
 */
export async function servePrompts(page: Page, text: string): Promise<void> {
  await page.route('**/api/admin/guide-prompts', (route) =>
    route.request().method() === 'GET' ? route.fulfill(promptsAnswer(text)) : route.fallback(),
  );
}

/**
 * Aborts each `GET /api/admin/guide-prompts` that the page sends from now on, in the browser, as a phone with no signal
 * would, as `failMe` does with `'no connection'`.
 */
export async function failPrompts(page: Page): Promise<void> {
  await page.route('**/api/admin/guide-prompts', (route) =>
    route.request().method() === 'GET' ? route.abort('internetdisconnected') : route.fallback(),
  );
}

/** A `PUT` of the prompts that the page sent: the `text` of its JSON body, and its `If-Match`. */
export type SentSave = { text: string; ifMatch: string | null };

/** What the `PUT` on `route` sent. */
export async function sentSave(route: Route): Promise<SentSave> {
  const request = route.request();
  return { text: (request.postDataJSON() as { text: string }).text, ifMatch: await request.headerValue('If-Match') };
}

/**
 * Meets each `PUT /api/admin/guide-prompts` that the page sends from now on, in the browser, with `answer`, such as
 * `route.fulfill(savedAnswer())`, so that no save reaches the app, and gives the list of those sent, which grows as the
 * page sends them. A later call overrides it, as with `servePrompts`.
 */
export async function serveSaves(page: Page, answer: (route: Route) => Promise<void>): Promise<SentSave[]> {
  const sent: SentSave[] = [];
  await page.route('**/api/admin/guide-prompts', async (route) => {
    if (route.request().method() !== 'PUT') {
      await route.fallback();
      return;
    }

    sent.push(await sentSave(route));
    await answer(route);
  });
  return sent;
}

/**
 * The organisation's prompts as the API keeps them, for the pages of two managers to load and save through, in the
 * browser: a `GET` gives the text with the row's version in the `ETag`; a `PUT` whose `If-Match` names that version
 * stores its text and moves the version on, answering `200` with the new `ETag` and the time in the `Date`, and any
 * other gets `412 precondition.failed` and stores nothing (design.md §6.6, §6.7).
 */
export function promptsStore(text: string) {
  let stored = text;
  let version = 1;
  const entityTag = () => `"version-${version}"`;

  return {
    /** The text stored now. */
    text: () => stored,
    /** Answers the page's requests for the prompts from now on, as `servePrompts` and `serveSaves` do. */
    serve: (page: Page) =>
      page.route('**/api/admin/guide-prompts', async (route) => {
        const request = route.request();
        if (request.method() === 'GET') {
          await route.fulfill({ json: { text: stored }, headers: { ETag: entityTag() } });
          return;
        }

        const { text: sent, ifMatch } = await sentSave(route);
        if (ifMatch !== entityTag()) {
          await route.fulfill({
            status: 412,
            contentType: 'application/problem+json',
            json: { type: 'about:blank', title: 'Precondition Failed', status: 412, code: 'precondition.failed' },
          });
          return;
        }

        stored = sent;
        version += 1;
        await route.fulfill({ status: 200, headers: { ETag: entityTag(), Date: new Date().toUTCString() } });
      }),
  };
}
