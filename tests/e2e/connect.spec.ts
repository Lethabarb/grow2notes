import type { Page, Route } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { scrollsSideways } from './layout.ts';
import { type Me, manager, serveMe, support, worker } from './me.ts';

// The Connect an AI assistant screen in the published app, under its production Content Security Policy (S07.01.07;
// mcp-server.md §4.4 and §10 item 6). Its /api/connect is stubbed: the access is switched off in the app under test,
// and the sign-in that leads here needs a person. This file imports `test` from fixtures.ts, so a page that logs a
// Content Security Policy violation fails its test.

const address = '/connect?request=made-up-handle';
const heading = 'Connect an AI assistant to Grow2Notes';
// What the server answers a choice with: the same-origin address at which it issues the code.
const authoriseAddress = '/oauth/authorize?client_id=made-up&decision=made-up-decision';
// Where the server's redirect from that address ends, at the assistant, in a made-up code, state and issuer.
const callback =
  'https://assistant.example/callback?code=made-up-code&state=made-up-state&iss=https%3A%2F%2Fgrow2notes.example%2F';

const registered = {
  assistantName: 'Example Assistant',
  company: 'Example AI Ltd',
  dataLocation: 'in the United States',
  clientIdHost: 'assistant.example',
  redirectHost: 'callback.assistant.example',
  loopbackOnly: false,
};

const main = (page: Page) => page.getByRole('main');
const allow = (page: Page) => main(page).getByRole('button', { name: 'Allow' });
const cancel = (page: Page) => main(page).getByRole('button', { name: 'Cancel' });
const isConnect = (url: URL) => url.pathname === '/api/connect';

/** Answers `GET /api/connect` with the registration, and `POST /api/connect` with the server's address. */
async function serveConnect(page: Page, details: typeof registered = registered) {
  await page.route(isConnect, (route: Route) =>
    route.request().method() === 'GET'
      ? route.fulfill({ json: details })
      : route.fulfill({ json: { next: authoriseAddress } }),
  );
}

/** Opens the screen for `me`, with the registration `details`, and waits for its heading. */
async function openAs(page: Page, me: Me, details: typeof registered = registered) {
  await serveMe(page, me);
  await serveConnect(page, details);
  await page.goto(address);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(heading);
}

const views: [string, Me, typeof registered][] = [
  ['manager', manager, registered],
  ['Support', support, registered],
  ['worker', worker, registered],
  ['loopback', manager, { ...registered, loopbackOnly: true }],
];

for (const [name, me, details] of views) {
  test(`the ${name} view passes axe, has one h1, and does not scroll sideways at 375 px`, async ({ page }) => {
    await openAs(page, me, details);

    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page).toHaveTitle('Grow2Notes – Connect an AI assistant');
    await expectAxeToPass(page);

    await page.setViewportSize({ width: 375, height: 800 });

    expect(await scrollsSideways(page), 'sideways scroll at 375 px').toBe(false);
  });
}

test('the views say what each person needs to read', async ({ page }) => {
  await openAs(page, manager);
  await expect(main(page)).toContainText('Signed in as Sam Lee (manager).');
  await expect(main(page)).toContainText('assistant.example');
  await expect(main(page)).toContainText('callback.assistant.example');
  await expect(main(page)).not.toContainText('runs on your computer');

  await openAs(page, support);
  await expect(main(page)).toContainText('Signed in as Ivy Chen (Support).');

  await openAs(page, manager, { ...registered, loopbackOnly: true });
  await expect(main(page)).toContainText('This assistant runs on your computer.');

  await openAs(page, worker);
  await expect(main(page)).toContainText('Only managers can connect an AI assistant to Grow2Notes.');
  await expect(allow(page)).toHaveCount(0);
  await expect(cancel(page)).toBeVisible();
});

test('with the keyboard alone, Tab reaches Allow and then Cancel', async ({ page }) => {
  await openAs(page, manager);

  // The wordmark and Account come first, and Allow is the next stop after them.
  for (let tabs = 0; tabs < 5; tabs += 1) {
    if (await allow(page).evaluate((button) => button === document.activeElement)) {
      break;
    }

    await page.keyboard.press('Tab');
  }

  await expect(allow(page)).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(cancel(page)).toBeFocused();
});

test('Allow ends at the assistant’s address, under the production Content Security Policy', async ({ page }) => {
  await openAs(page, manager);
  // The server's answer to the navigation: a redirect to the assistant, which a stand-in answers.
  await page.route(
    (url) => url.pathname === '/oauth/authorize',
    (route) => route.fulfill({ status: 302, headers: { location: callback } }),
  );
  await page.route(
    (url) => url.hostname === 'assistant.example',
    (route) => route.fulfill({ contentType: 'text/html', body: '<!doctype html><title>Assistant</title>' }),
  );

  await allow(page).click();

  await page.waitForURL(callback);
  expect(page.url()).toBe(callback);
  // A violation is a console error, which fixtures.ts fails the test on.
});
