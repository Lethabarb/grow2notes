import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';

/**
 * The tab's history, which no script in the page can read, through Chromium's DevTools protocol: every address in it,
 * and the index of the one shown.
 */
export async function tabHistory(page: Page): Promise<{ urls: string[]; currentIndex: number }> {
  const devTools = await page.context().newCDPSession(page);
  const { entries, currentIndex } = await devTools.send('Page.getNavigationHistory');
  // Left attached until the page closes: detaching a DevTools session takes the page's virtual authenticator with it,
  // and a later passkey prompt would then find none.
  return { urls: entries.map(({ url }) => url), currentIndex };
}

/**
 * Expects no entry before the current one in the tab's history to be a setup page, and Back to leave the app
 * (sign-in.md *Setup*: "Back from Today does not return to a setup or sign-in step").
 */
export async function expectNoWayBackToSetup(page: Page) {
  const { urls, currentIndex } = await tabHistory(page);
  expect(urls.slice(0, currentIndex)).not.toContainEqual(expect.stringContaining('/setup'));
  const app = new URL(page.url()).origin;
  await page.goBack();
  await expect(page).toHaveURL((url) => url.origin !== app);
}
