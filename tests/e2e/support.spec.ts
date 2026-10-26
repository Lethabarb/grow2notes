import type { Page, Request } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { scrollsSideways } from './layout.ts';
import { serveMe, support } from './me.ts';

// The Support page in the published app (S07.01.01), against app-shell.md › Support page.

const heading = 'This account is only used to connect an AI assistant.';

const isLogout = (request: Request) => new URL(request.url()).pathname === '/api/auth/logout';

const main = (page: Page) => page.getByRole('main');
const signOutOnPage = (page: Page) => main(page).getByRole('button', { name: 'Sign out' });
const account = (page: Page) => page.getByRole('button', { name: 'Account' });
const signOutInPanel = (page: Page) => page.locator('#account-panel').getByRole('button', { name: 'Sign out' });

test.beforeEach(async ({ page }) => {
  await serveMe(page, support);
});

for (const path of ['/', '/manage/users', '/no-such-page']) {
  test(`at ${path}, a Support account sees only the Support page, with no navigation`, async ({ page }) => {
    await page.goto(path);

    await expect(main(page).getByRole('heading', { level: 1 })).toHaveText(heading);
    await expect(page).toHaveTitle('Grow2Notes – Connect an AI assistant');
    await expect(page.getByRole('navigation')).toHaveCount(0);
    await expect(signOutOnPage(page)).toBeVisible();
  });
}

test('Sign out on the page sends one logout, then the app loads afresh', async ({ page }) => {
  await page.goto('/');
  const logout = page.waitForRequest(isLogout);
  const loaded = page.waitForEvent('load');

  await signOutOnPage(page).click();

  expect((await logout).method()).toBe('POST');
  await loaded;
});

test('Sign out in the Account panel sends one logout, then the app loads afresh', async ({ page }) => {
  await page.goto('/');
  await account(page).click();
  const logout = page.waitForRequest(isLogout);
  const loaded = page.waitForEvent('load');

  await signOutInPanel(page).click();

  expect((await logout).method()).toBe('POST');
  await loaded;
});

test('the Support page passes axe, and at 375 px does not scroll sideways', async ({ page }) => {
  await page.goto('/');
  await expect(signOutOnPage(page)).toBeVisible();
  await expectAxeToPass(page);

  await page.setViewportSize({ width: 375, height: 800 });

  expect(await scrollsSideways(page), 'sideways scroll at 375 px').toBe(false);
});

test('with the keyboard alone, Tab reaches the page\'s Sign out, and Enter signs out', async ({ page }) => {
  await page.goto('/');
  await expect(signOutOnPage(page)).toBeVisible();

  // The wordmark and Account come first, and the page's Sign out is the next stop after them.
  for (let tabs = 0; tabs < 5; tabs += 1) {
    if (await signOutOnPage(page).evaluate((button) => button === document.activeElement)) {
      break;
    }

    await page.keyboard.press('Tab');
  }

  await expect(signOutOnPage(page)).toBeFocused();
  const logout = page.waitForRequest(isLogout);
  await page.keyboard.press('Enter');
  expect((await logout).method()).toBe('POST');
});
