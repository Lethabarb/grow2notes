import type { Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { manager, serveMe, worker, type Me } from './me.ts';
import { people, serveUsers } from './users.ts';

// The shell of the published app, signed in as a made-up worker and manager, whose GET /api/auth/me the tests answer
// themselves (me.ts), against app-shell.md's acceptance criteria (S00.05.01).

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

// Each role's links and buttons in the header, in order (app-shell.md › Navigation).
const headerItems: Record<Me['role'], string[]> = {
  Worker: ['Grow2Notes', 'Today', 'Account'],
  Manager: ['Grow2Notes', 'Account', 'Today', 'Flagged', 'Report', 'Manage'],
};

// Each role's rows in the header where nothing wraps: a worker's one, a manager's two (app-shell.md *Layout - phone*).
const headerRows: Record<Me['role'], number> = { Worker: 1, Manager: 2 };

for (const me of [worker, manager]) {
  const role = me.role.toLowerCase();

  test(`as a ${role}, the shell keeps its order, items and static header at 320, 375 and 1280 px`, async ({ page }) => {
    await serveMe(page, me);
    await page.goto('/');

    for (const width of [320, 375, 1280]) {
      await page.setViewportSize({ width, height: 800 });

      // Exactly the role's items among those that show, as Sign out in the closed panel does not, so no menu button,
      // and each on screen rather than pushed off it at some width.
      const items = page.getByRole('banner').locator('a, button').filter({ visible: true });
      await expect(items).toHaveText(headerItems[me.role]);
      for (const item of await items.all()) {
        await expect(item).toBeInViewport({ ratio: 1 });
      }

      // The skip link, the header, the before-main bar slot, the one <main> and the session warning's <dialog>
      // (app-shell.md › Structure).
      const frame = await page
        .locator('#root > *')
        .evaluateAll((elements) => elements.map(({ localName, id }) => (id === '' ? localName : `${localName}#${id}`)));
      expect(frame).toEqual(['a', 'header', 'div#page-bar', 'main#main-content', 'dialog']);
      await expect(page.locator('#root > a')).toHaveText('Skip to main content');
      await expect(page.locator('main')).toHaveCount(1);

      // A fixed or sticky bar would hide the focused field under a phone's keyboard and at 200% text (component 2).
      await expect(page.getByRole('banner')).toHaveCSS('position', 'static');
      const pinned = await page.evaluate(() =>
        Array.from(document.querySelectorAll('body *'))
          .filter((element) => ['fixed', 'sticky'].includes(getComputedStyle(element).position))
          .map((element) => (element.cloneNode(false) as Element).outerHTML),
      );
      expect(pinned).toEqual([]);
    }
  });

  test(`as a ${role}, the skip link is the first Tab stop, shows, and focuses <main> where it is`, async ({ page }) => {
    await serveMe(page, me);
    await page.goto('/');
    await expect(page.getByRole('button', { name: 'Account' })).toBeVisible();
    const address = page.url();
    const historyLength = await page.evaluate(() => history.length);

    await page.keyboard.press('Tab');
    const skipLink = page.getByRole('link', { name: 'Skip to main content' });
    await expect(skipLink).toBeFocused();
    // No longer clipped to a pixel, and over the header rather than under it (app-shell.md component 1).
    const shown = await skipLink.evaluate((link) => {
      const { x, y, width, height } = link.getBoundingClientRect();
      return {
        biggerThanAPixel: width > 1 && height > 1,
        onTop: document.elementFromPoint(x + width / 2, y + height / 2) === link,
      };
    });
    expect(shown).toEqual({ biggerThanAPixel: true, onTop: true });

    await page.keyboard.press('Enter');
    await expect(page.locator('main')).toBeFocused();
    expect(page.url()).toBe(address);
    expect(await page.evaluate(() => history.length)).toBe(historyLength);
  });

  test(`as a ${role}, the header passes the WCAG 2.2 AA checks, Account closed and open`, async ({ page }) => {
    await serveMe(page, me);
    await page.goto('/');
    const account = page.getByRole('button', { name: 'Account' });
    const panel = page.locator('#account-panel');
    await expect(account).toBeVisible();
    // .panel[hidden] keeps the panel's display: flex from showing it while it is closed, which jsdom cannot show
    // (S00.05.01's Notes).
    await expect(panel).toBeHidden();
    await expectAxeToPass(page);

    await account.click();
    await expect(panel).toBeVisible();
    await expect(panel.getByText(`Signed in as ${me.displayName}`, { exact: true })).toBeVisible();
    await expect(panel.getByRole('button', { name: 'Sign out' })).toBeVisible();
    await expectAxeToPass(page);
  });

  test(`as a ${role}, the header wraps at 320 px and 200% text, no sideways scroll or overlap`, async ({ page }) => {
    await serveMe(page, me);
    await page.setViewportSize({ width: 320, height: 800 });
    await page.goto('/');
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
    await expectHeaderToWrap(page, headerRows[me.role]);

    // Text at 200%: Chromium's default text size at 32 px, where it is 16 (S00.05.01's Notes).
    await page.setViewportSize({ width: 375, height: 800 });
    const cdp = await page.context().newCDPSession(page);
    await cdp.send('Page.setFontSizes', { fontSizes: { standard: 32 } });
    await expect(page.locator('body')).toHaveCSS('font-size', '36px');
    await expectHeaderToWrap(page, headerRows[me.role]);
  });
}

test("Today's heading and the body text take the type scale, under index.html's head", async ({ page }) => {
  await serveMe(page, worker);
  // A phone's width, below the one breakpoint, (min-width: 40rem).
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/');
  const heading = page.getByRole('main').getByRole('heading', { level: 1 });
  await expect(heading).toContainText('Thursday 1 October');

  // Without tokens.css and base.css the browser's defaults would give a 32 px <h1> at every width, 16 px body text and
  // a color-scheme of normal, so these also show that the stylesheet loaded under the policy.
  await expect(heading).toHaveCSS('font-size', '28px');
  await expect(page.locator('body')).toHaveCSS('font-size', '18px');
  // Light without only, so that a browser's own page darkening still works (app-shell.md component 8).
  await expect(page.locator('html')).toHaveCSS('color-scheme', 'light');

  // A laptop's width, from the breakpoint on.
  await page.setViewportSize({ width: 1280, height: 720 });
  await expect(heading).toHaveCSS('font-size', '32px');
  await expect(page.locator('body')).toHaveCSS('font-size', '18px');

  // The page's language, and a viewport tag with neither maximum-scale nor user-scalable, so the reader can zoom.
  await expect(page.locator('html')).toHaveAttribute('lang', 'en-AU');
  await expect(page.locator('meta[name="viewport"]')).toHaveAttribute('content', 'width=device-width, initial-scale=1');
});

test('the Manage page passes the WCAG 2.2 AA checks', async ({ page }) => {
  await serveMe(page, manager);
  await page.goto('/manage');
  await expect(page.getByRole('main').getByRole('link')).toHaveText([
    'Participants',
    'Common items',
    'Guide prompts',
    'Users',
  ]);

  await expectAxeToPass(page);
});

test("a worker's /manage shows Page not found, which passes the WCAG 2.2 AA checks", async ({ page }) => {
  await serveMe(page, worker);
  await page.goto('/manage');

  await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toHaveText('Page not found');
  await expect(page).toHaveTitle(title('Page not found'));
  // The nav stays (app-shell.md component 4). That it marks nothing is shown in the manager's walk below, at addresses
  // that the table marks, as a worker's nav has no item that /manage could mark.
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();

  await expectAxeToPass(page);
});

/**
 * A link that a walk follows, in the header or in `<main>`, with the address and the page it opens, and the nav item
 * marked current there, if any.
 */
type Step = { region: 'banner' | 'main'; link: string; path: string; pageName: string; current?: string };

// Each role's header links, then, for the manager, each Manage link, going back to Manage through the nav between
// them. Until they are built, Flagged, Report and the first three Manage links open Page not found (S00.05.01's first
// two criteria), which marks no item, though component 2b's table marks Flagged, Report or Manage at its address. Users
// opens the Users list (S00.06.01), a child page of Manage, which marks Manage.
const walks: { me: Me; steps: Step[] }[] = [
  {
    me: worker,
    steps: [
      { region: 'banner', link: 'Grow2Notes', path: '/', pageName: 'Today', current: 'Today' },
      { region: 'banner', link: 'Today', path: '/', pageName: 'Today', current: 'Today' },
    ],
  },
  {
    me: manager,
    steps: [
      { region: 'banner', link: 'Grow2Notes', path: '/', pageName: 'Today', current: 'Today' },
      { region: 'banner', link: 'Today', path: '/', pageName: 'Today', current: 'Today' },
      { region: 'banner', link: 'Flagged', path: '/flagged', pageName: 'Page not found' },
      { region: 'banner', link: 'Report', path: `/reports/daily/${manager.today}`, pageName: 'Page not found' },
      ...[
        ['Participants', '/manage/participants'],
        ['Common items', '/manage/common-items'],
        ['Guide prompts', '/manage/guide-prompts'],
      ].flatMap(([link, path]): Step[] => [
        { region: 'banner', link: 'Manage', path: '/manage', pageName: 'Manage', current: 'Manage' },
        { region: 'main', link, path, pageName: 'Page not found' },
      ]),
      { region: 'banner', link: 'Manage', path: '/manage', pageName: 'Manage', current: 'Manage' },
      { region: 'main', link: 'Users', path: '/manage/users', pageName: 'Users', current: 'Manage' },
    ],
  },
];

test('the walks mark the nav, name no one in titles, and keep no app state in history or storage', async ({ page }) => {
  // The Users list's request, answered in the browser with made-up people, as no made-up manager has a session.
  await serveUsers(page, people);
  for (const { me, steps } of walks) {
    await serveMe(page, me);
    await page.goto('/');

    for (const { region, link, path, pageName, current } of steps) {
      await page.getByRole(region).getByRole('link', { name: link, exact: true }).click();
      await expect(page).toHaveURL(path);
      // One item at most (app-shell.md component 2b), and none on Page not found (component 4).
      const marked = page.getByRole('banner').locator('[aria-current]');
      await expect(marked).toHaveText(current === undefined ? [] : [current]);
      // Exactly the page's name, which no made-up name, nor any other data, can reach (app-shell.md component 3).
      await expect(page).toHaveTitle(title(pageName));
      // React Router's own key and index, and nothing of the app's own in its usr (app-shell.md › Structure).
      await expect
        .poll(() => page.evaluate(() => history.state as unknown), { message: `history.state after ${link}` })
        .toEqual({ usr: null, key: expect.any(String), idx: expect.any(Number) });
    }
  }

  // Nothing of either person's is kept on the device (D22, design.md §9.6).
  const stored = await page.evaluate(async () => ({
    localStorage: localStorage.length,
    sessionStorage: sessionStorage.length,
    indexedDB: (await indexedDB.databases()).length,
  }));
  expect(stored).toEqual({ localStorage: 0, sessionStorage: 0, indexedDB: 0 });
});

/**
 * Expects the header to wrap, the targets it shows on more lines than its `rows` (app-shell.md *Layout - phone*), with
 * every one inside the viewport, no two of them overlapping and no sideways scroll (app-shell.md › Navigation). A
 * hidden target, such as Sign out in the closed panel, has an empty box at the top left, which would count as a line.
 */
async function expectHeaderToWrap(page: Page, rows: number) {
  const layout = await page.evaluate(() => {
    const viewportWidth = document.documentElement.clientWidth;
    const shown = Array.from(document.querySelectorAll('header a, header button')).filter((target) =>
      target.checkVisibility(),
    );
    const targets = shown.map((target) => ({ name: target.textContent, box: target.getBoundingClientRect() }));
    const outside = targets.filter(({ box }) => box.left < 0 || box.right > viewportWidth).map(({ name }) => name);
    const overlapping = targets.flatMap(({ name, box }, index) =>
      targets
        .slice(index + 1)
        .filter(
          (other) =>
            box.left < other.box.right &&
            other.box.left < box.right &&
            box.top < other.box.bottom &&
            other.box.top < box.bottom,
        )
        .map((other) => `${name} and ${other.name}`),
    );
    return {
      lines: new Set(targets.map(({ box }) => box.top)).size,
      outside,
      overlapping,
      scrollsSideways: document.documentElement.scrollWidth > viewportWidth,
    };
  });

  expect(layout.lines, 'lines of targets').toBeGreaterThan(rows);
  expect(layout.outside, 'targets outside the viewport').toEqual([]);
  expect(layout.overlapping, 'overlapping targets').toEqual([]);
  expect(layout.scrollsSideways, 'sideways scroll').toBe(false);
}
