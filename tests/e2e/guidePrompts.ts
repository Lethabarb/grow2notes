import type { Page } from '@playwright/test';

/** The design's sketch of the prompts (guide-prompts.md), with a blank line inside them, which is kept too. */
export const savedPrompts = 'Mood and wellbeing today?\nWhat did you do together?\n\nAnything to follow up?';

/** A made-up row version of the organisation, as the API gives it in the `ETag`, without the quotes. */
const promptsVersion = 'AAAAAAAAB9E=';

/** The `GET`'s answer: the prompts as JSON, with the row's version in the `ETag`, as the API gives them. */
export const promptsAnswer = (text: string, version = promptsVersion) => ({
  json: { text },
  headers: { ETag: `"${version}"` },
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
