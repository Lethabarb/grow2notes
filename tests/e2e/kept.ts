import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';

/**
 * Expects none of `values`, such as a person's name and address, where the page could keep them (users.md AC3): the
 * title, the address, which holds no query or fragment, `history.state`, which holds React Router's own key and index
 * and nothing of the app's in its usr (app-shell.md › Structure), and the browser's storage (D22, design.md §9.6).
 */
export async function expectNothingKept(page: Page, ...values: string[]) {
  const kept = await page.evaluate(async () => ({
    title: document.title,
    address: location.href,
    state: history.state as unknown,
    storage: [
      localStorage.length,
      sessionStorage.length,
      (await indexedDB.databases()).length,
      (await caches.keys()).length,
    ],
  }));
  for (const value of values) {
    expect(kept.title).not.toContain(value);
    expect(kept.address).not.toContain(value);
    expect(kept.address).not.toContain(encodeURIComponent(value));
  }

  expect(kept.state).toEqual({ usr: null, key: expect.any(String), idx: expect.any(Number) });
  expect(kept.storage).toEqual([0, 0, 0, 0]);
}
