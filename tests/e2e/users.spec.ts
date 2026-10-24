import type { Locator, Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { whereFocusIs } from './focus.ts';
import { boxOf, setTextTo200Percent } from './layout.ts';
import { manager, serveMe, worker } from './me.ts';
import { holdNext } from './requests.ts';
import { alex, chris, failUsers, jo, kim, madeUpUser, people, sam, serveUsers } from './users.ts';

// The Users list and the user detail in the published app (S00.06.01), against users.md's criteria for access, the
// list and the detail page, with the detail's actions for each status (S00.06.04, S00.06.05). The first test signs in
// for real, through setup with a passkey on the virtual authenticator, to meet the app's own GET /api/admin/users; the
// others answer /api/auth/me and /api/admin/users in the browser with made-up people (me.ts, users.ts), so that
// neither request reaches the app.

// The notice on the only active manager's own page (users.md *States* › User detail).
const lastManagerNotice =
  'You are the only active manager, so you cannot reset your own sign-in, deactivate your account or make yourself ' +
  'a worker. To do any of these, first make someone else a manager.';

/** A page title: "Grow2Notes – ", with an en dash, and the page's name (app-shell.md component 3). */
const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const inviteUser = (page: Page) => main(page).getByRole('link', { name: 'Invite user', exact: true });
// The region's line, beside the page's visually hidden status region (users.md *Live regions*).
const statusLine = (page: Page) => main(page).getByRole('status').and(page.locator(':not(.visually-hidden)'));
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });
const lists = (page: Page) => main(page).getByRole('list');
// The shell's bar between <header> and <main>, which holds the detail page's back link (app-shell.md component 3a).
const pageBar = (page: Page) => page.locator('#page-bar');
// The nav items marked current, Manage alone on both pages (users.md component A item 1).
const marked = (page: Page) => page.getByRole('banner').locator('[aria-current]');

// users.md's four, in the list's order: the current users by name, then the Deactivated.
const sorted = [['Alex Park', 'Jo Smith', 'Sam Lee'], ['Chris Ng']];

/** The row whose link is named `name`. */
const rowOf = (page: Page, name: string) =>
  main(page).getByRole('listitem').filter({ has: page.getByRole('link', { name, exact: true }) });

/** Expects the lists in `<main>` to hold these rows, in order, each row one link, named by the person's name alone. */
async function expectLists(page: Page, names: string[][]) {
  await expect(lists(page)).toHaveCount(names.length);
  for (const [index, rows] of names.entries()) {
    const list = lists(page).nth(index);
    await expect(list.getByRole('listitem')).toHaveCount(rows.length);
    await expect(list.getByRole('link')).toHaveText(rows);
  }
}

/**
 * The lines under a row's name as they show, without the commas that only screen readers get (`Pause` and `Sep`): the
 * email, then "{Status} · {Role}".
 */
function shownLines(row: Locator): Promise<string[]> {
  return row.locator('p').evaluate((detail) => {
    const shown = detail.cloneNode(true) as HTMLElement;
    shown.querySelectorAll('.visually-hidden').forEach((hidden) => {
      hidden.remove();
    });
    return Array.from(shown.children, (line) => line.textContent ?? '');
  });
}

type Edges = { left: number; right: number; top: number; bottom: number };

/** Where a tap on the box lands: its middle. */
const middleOf = ({ left, right, top, bottom }: Edges) => ({ x: (left + right) / 2, y: (top + bottom) / 2 });

/** The box of the first line of the element's text, where a tap on the text itself lands. */
function firstLineOf(locator: Locator): Promise<Edges> {
  return locator.evaluate((element) => {
    const text = document.createRange();
    text.selectNodeContents(element);
    const { left, right, top, bottom } = text.getClientRects()[0];
    return { left, right, top, bottom };
  });
}

/**
 * Where a tap on a part of a row lands: on the name, the email or the status, each in its first line of text; on the
 * chevron; or on the row's blank space, between the end of the name and the chevron.
 */
async function pointIn(row: Locator, part: 'name' | 'email' | 'status' | 'chevron' | 'blank space') {
  // The detail's lines are its <span>s: the email, then the status and role.
  const [email, status] = [row.locator('p > span').nth(0), row.locator('p > span').nth(1)];
  const { x, y, width, height } = await boxOf(row.locator('svg'));
  const chevron = { left: x, right: x + width, top: y, bottom: y + height };
  switch (part) {
    case 'name':
      return middleOf(await firstLineOf(row.getByRole('link')));
    case 'email':
      return middleOf(await firstLineOf(email));
    case 'status':
      return middleOf(await firstLineOf(status));
    case 'chevron':
      return middleOf(chevron);
    case 'blank space': {
      const name = await firstLineOf(row.getByRole('link'));
      return middleOf({ ...name, left: name.right, right: chevron.left });
    }
  }
}

/**
 * How the element's own text lies: whether it wraps onto more than one line, whether any of it reaches past the sides
 * of the viewport or of the nearest element that matches `box`, itself or an ancestor, such as its row's `li`, and
 * whether the page scrolls sideways.
 */
function textFit(text: Locator, box: string) {
  return text.evaluate((element, boxSelector) => {
    // Its own text, without the hidden comma at the end of a row's email, which is clipped to a pixel.
    const lines = [...element.childNodes]
      .filter((node) => node.nodeType === Node.TEXT_NODE)
      .flatMap((node) => {
        const range = document.createRange();
        range.selectNodeContents(node);
        return [...range.getClientRects()];
      });
    const sides = element.closest(boxSelector)?.getBoundingClientRect();
    const viewportWidth = document.documentElement.clientWidth;
    const [left, right] = [Math.max(sides?.left ?? 0, 0), Math.min(sides?.right ?? viewportWidth, viewportWidth)];
    return {
      wraps: new Set(lines.map(({ top }) => Math.round(top))).size > 1,
      reachesPastItsBox: lines.some((line) => line.left < left || line.right > right),
      scrollsSideways: document.documentElement.scrollWidth > viewportWidth,
    };
  }, box);
}

/**
 * How a row looks: the colour, background colour, opacity and filter of the row, its name, its detail and each line of
 * it.
 */
function looksOf(row: Locator) {
  return row.evaluate((element) =>
    [element, ...element.querySelectorAll('a, p, p > span')].map((part) => {
      const { color, backgroundColor, opacity, filter } = getComputedStyle(part);
      return { color, backgroundColor, opacity, filter };
    }),
  );
}

test('signed in for real, Manage then Users lists the new manager alone, Active · Manager, whose page opens', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.goto(invite.setupLink);
  await main(page).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(heading(page)).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(1);

  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  await expect(heading(page)).toHaveText('Manage');
  const listed = page.waitForResponse((response) => new URL(response.url()).pathname === '/api/admin/users');
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();

  // The app's own answer, for the organisation that admin bootstrap made, whose only user is its first manager.
  const answer = await listed;
  expect(answer.status()).toBe(200);
  expect(await answer.json()).toEqual([
    expect.objectContaining({
      email: invite.email,
      displayName: invite.displayName,
      role: 'Manager',
      status: 'Active',
    }),
  ]);
  await expectLists(page, [[invite.displayName]]);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveCount(0);
  expect(await shownLines(rowOf(page, invite.displayName))).toEqual([invite.email, 'Active · Manager']);

  await main(page).getByRole('link', { name: invite.displayName, exact: true }).click();
  await expect(heading(page)).toHaveText(invite.displayName);
  await expect(main(page).getByRole('term')).toHaveText(['Email address', 'Role', 'Status']);
  await expect(main(page).getByRole('definition')).toHaveText([invite.email, 'Manager', 'Active']);
  // The organisation's only active manager, on their own page: Edit, and the notice in place of Reset sign-in and
  // Deactivate (users.md *Which actions show, by status*, AC22).
  await expect(main(page).getByRole('link')).toHaveText(['Edit']);
  await expect(main(page).getByRole('button')).toHaveCount(0);
  await expect(main(page).getByText(lastManagerNotice)).toBeVisible();
});

test('the current users show by name, then the Deactivated under their <h2>, each row with email and status', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveUsers(page, people);
  await page.goto('/manage/users');

  await expect(heading(page)).toHaveText('Users');
  await expect(page).toHaveTitle(title('Users'));
  await expectLists(page, sorted);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveText('Deactivated');
  const order = await main(page)
    .locator('h2, ul')
    .evaluateAll((elements) => elements.map(({ localName }) => localName));
  expect(order).toEqual(['ul', 'h2', 'ul']);
  // Name, email, then status and role, the viewer's own row with no "(you)" (users.md component A item 6). The link's
  // description has a space before each hidden comma, as Chromium computes it too, which screen readers do not voice
  // (S00.05.01's Notes).
  for (const { displayName, email, status, role } of people) {
    const link = rowOf(page, displayName).getByRole('link');
    await expect(link).toHaveAccessibleName(displayName);
    await expect(link).toHaveAccessibleDescription(`${email} , ${status} , ${role}`);
    expect(await shownLines(rowOf(page, displayName))).toEqual([email, `${status} · ${role}`]);
  }

  await expectAxeToPass(page);
});

test('with nobody Deactivated, one list and no <h2>, an accent and lower case sorted as the collator sorts', async ({
  page,
}) => {
  const emile = madeUpUser(
    '9e4a1c7f-3b62-4d05-b8e9-6c1f2a4d8e37',
    'Émile Durand',
    'emile.durand@example.org',
    'Active',
  );
  const david = madeUpUser('2b7e9d4c-8f13-4a6e-9d52-3c0a7f1e5b84', 'david kim', 'david.kim@example.org', 'Invited');
  await serveMe(page, manager);
  await serveUsers(page, [sam, emile, jo, david, alex]);
  await page.goto('/manage/users');

  // A sort by code unit would put the name in lower case, and then the one with an accent, after Sam Lee.
  await expectLists(page, [['Alex Park', 'david kim', 'Émile Durand', 'Jo Smith', 'Sam Lee']]);
  await expect(main(page).getByRole('heading', { level: 2 })).toHaveCount(0);

  await expectAxeToPass(page);
});

test('a tap on the name, email, status, chevron or blank space opens the user, and each row is one Tab stop', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveUsers(page, people);
  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto('/manage/users');
  await expectLists(page, sorted);

  for (const part of ['name', 'email', 'status', 'chevron', 'blank space'] as const) {
    const { x, y } = await pointIn(rowOf(page, alex.displayName), part);
    // The row's one link is what the point hits, as its ::after covers the whole row.
    const hit = await page.evaluate(([atX, atY]) => {
      const element = document.elementFromPoint(atX, atY);
      return `${element?.localName} ${element?.textContent}`;
    }, [x, y]);
    expect(hit, part).toBe('a Alex Park');
    await page.mouse.click(x, y);
    await expect(page, part).toHaveURL(`/manage/users/${alex.id}`);
    await expect(heading(page)).toHaveText(alex.displayName);

    await page.goBack();
    await expect(page).toHaveURL('/manage/users');
    await expectLists(page, sorted);
  }

  // From Invite user, each row in turn, current then Deactivated, and nothing after the last.
  await inviteUser(page).focus();
  const stops: string[] = [];
  for (let stop = 0; stop < 5; stop += 1) {
    await page.keyboard.press('Tab');
    stops.push(await whereFocusIs(page));
  }
  expect(stops).toEqual([...sorted.flat(), 'outside the page']);
});

test('with the list held for 1.5 s, nothing shows for 1 s, then "Loading users…", then the rows', async ({
  page,
}) => {
  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await serveMe(page, manager);
  const heldList = await holdNext(page, '**/api/admin/users');
  await page.goto('/manage');
  await expect(heading(page)).toHaveText('Manage');

  // Paused a second ahead of the page's time, so that it moves only as the test runs it on from the press of Users.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  const list = await heldList();

  // The <h1> and Invite user at once, and no rows, sentence or line (users.md *States*: Waiting).
  await page.clock.runFor(999);
  await expect(heading(page)).toHaveText('Users');
  await expect(inviteUser(page)).toBeVisible();
  await expect(statusLine(page)).toBeEmpty();
  await expect(lists(page)).toHaveCount(0);
  await page.clock.runFor(1);
  await expect(statusLine(page)).toHaveText('Loading users…');
  await expect(lists(page)).toHaveCount(0);

  // The answer, at 1.5 s, brings the rows and clears the line.
  await page.clock.runFor(500);
  await page.clock.resume();
  await list.fulfill({ json: people });
  await expectLists(page, sorted);
  await expect(statusLine(page)).toBeEmpty();
});

test('with no connection, the list fails within 2 s, then says still, and a Try again that loads focuses <h1>', async ({
  page,
}) => {
  await serveMe(page, manager);
  await failUsers(page);
  await page.goto('/manage/users');

  // Within 2 s of the page loading, as the request and its one silent retry, 1 s later, each fail at once.
  await expect(statusLine(page)).toHaveText('The user list did not load: no connection. Try again.', {
    timeout: 2_000,
  });
  await expect(heading(page)).toHaveText('Users');
  await expect(inviteUser(page)).toBeVisible();
  await expect(lists(page)).toHaveCount(0);
  await expectAxeToPass(page);

  await tryAgain(page).click();
  await expect(statusLine(page)).toHaveText('The user list still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();

  await serveUsers(page, people);
  await tryAgain(page).click();
  await expectLists(page, sorted);
  // Try again has gone with the sentence, so focus goes to the <h1> (users.md *States*).
  await expect(heading(page)).toBeFocused();
  await expect(statusLine(page)).toBeEmpty();
});

test('at 320 px and 200% text, an 80-character email wraps in its row and on its page, no sideways scroll', async ({
  page,
}) => {
  const long = madeUpUser(
    '6d2f8b1e-5a94-4c37-a1e6-9b4d0f7c3e52',
    'Bartholomew Featherstonehaugh',
    'bartholomew.featherstonehaugh-montgomery.support-coordinator-team-au@example.org',
    'Active',
  );
  expect(long.email).toHaveLength(80);
  await serveMe(page, manager);
  await serveUsers(page, [sam, long]);
  await page.setViewportSize({ width: 320, height: 800 });
  await page.goto('/manage/users');
  await setTextTo200Percent(page);

  const row = rowOf(page, long.displayName);
  await expect(row).toBeVisible();
  expect(await textFit(row.locator('p > span').first(), 'li')).toEqual({
    wraps: true,
    reachesPastItsBox: false,
    scrollsSideways: false,
  });

  await row.getByRole('link').click();
  await expect(heading(page)).toHaveText(long.displayName);
  expect(await textFit(main(page).getByRole('definition').first(), 'dd')).toEqual({
    wraps: true,
    reachesPastItsBox: false,
    scrollsSideways: false,
  });
});

test('the Deactivated row looks as the others do: the same colours and opacity, with nothing faded', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveUsers(page, people);
  await page.goto('/manage/users');
  await expectLists(page, sorted);

  const deactivated = rowOf(page, chris.displayName);
  // The name in the body text's colour, tokens.css's --colour-text, at full opacity (users.md component A item 6).
  await expect(deactivated.getByRole('link')).toHaveCSS('color', 'rgb(11, 12, 12)');
  await expect(deactivated).toHaveCSS('opacity', '1');
  const looks = await looksOf(deactivated);
  for (const { displayName } of [alex, jo, sam]) {
    expect(looks, displayName).toEqual(await looksOf(rowOf(page, displayName)));
  }
});

test("in a dark contrast theme, each row's chevron is drawn in the theme's text colour, as its divider is", async ({
  page,
}) => {
  // A Windows contrast theme, as Chromium emulates one: a dark one, on whose black a chevron left grey is faint.
  await page.emulateMedia({ forcedColors: 'active', colorScheme: 'dark' });
  await serveMe(page, manager);
  await serveUsers(page, people);
  await page.goto('/manage/users');
  await expectLists(page, sorted);

  for (const row of await main(page).getByRole('listitem').all()) {
    const { text, chevron, divider } = await row.evaluate((item) => ({
      text: getComputedStyle(item).color,
      chevron: getComputedStyle(item.querySelector('path')!).stroke,
      divider: getComputedStyle(item).borderBlockEndColor,
    }));
    expect({ chevron, divider }).toEqual({ chevron: text, divider: text });
  }
});

// users.md's *Which actions show, by status* (AC21): Edit; Resend invite while Invited, or Reset sign-in while Active;
// then Deactivate, or Reactivate while Deactivated. Sam Lee's own page offers both with a second active manager (AC22).
for (const { user, users, buttons } of [
  { user: alex, users: people, buttons: ['Resend invite', 'Deactivate'] },
  { user: jo, users: people, buttons: ['Reset sign-in', 'Deactivate'] },
  { user: chris, users: people, buttons: ['Reactivate'] },
  { user: sam, users: [...people, kim], buttons: ['Reset sign-in', 'Deactivate'] },
]) {
  const actions = ['Edit', ...buttons].join(', ');
  test(`${user.displayName}'s page, ${user.status}, shows the summary list and ${actions}, and passes axe`, async ({
    page,
  }) => {
    await serveMe(page, manager);
    await serveUsers(page, users);
    await page.goto(`/manage/users/${user.id}`);

    await expect(heading(page)).toHaveText(user.displayName);
    await expect(page).toHaveTitle(title('User'));
    await expect(main(page).getByRole('term')).toHaveText(['Email address', 'Role', 'Status']);
    await expect(main(page).getByRole('definition')).toHaveText([user.email, user.role, user.status]);
    // Edit is a link, the other actions buttons, and nothing is disabled (users.md AC21).
    await expect(main(page).getByRole('link')).toHaveText(['Edit']);
    const edit = main(page).getByRole('link', { name: 'Edit' });
    await expect(edit).toHaveAttribute('href', `/manage/users/${user.id}/edit`);
    // By name, as a button's text holds its busy label too, which only shows while it is busy.
    await expect(main(page).getByRole('button')).toHaveCount(buttons.length);
    for (const [index, name] of buttons.entries()) {
      await expect(main(page).getByRole('button').nth(index)).toHaveAccessibleName(name);
    }
    await expect(page.locator('[disabled]')).toHaveCount(0);
    // The back link is in the bar before <main>, which Skip to main content passes.
    await expect(pageBar(page).getByRole('link')).toHaveText('Users');
    await expect(pageBar(page).getByRole('link')).toHaveAttribute('href', '/manage/users');

    // The serious action after the rule, apart from the routine ones (users.md component C item 7).
    await expect(main(page).getByRole('separator')).toHaveCount(1);
    await expect(main(page).getByText(lastManagerNotice)).toHaveCount(0);

    // From the back link, Edit, then each button, and nothing after the last, as the Status row adds no stop (users.md
    // *Focus order*).
    await pageBar(page).getByRole('link').focus();
    for (const stop of [edit, ...buttons.map((name) => main(page).getByRole('button', { name }))]) {
      await page.keyboard.press('Tab');
      await expect(stop).toBeFocused();
    }
    await page.keyboard.press('Tab');
    expect(await whereFocusIs(page)).toBe('outside the page');

    await expectAxeToPass(page);
  });
}

test("the only active manager's own page shows Edit, the rule and the notice, with no stop after Edit (AC22)", async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveUsers(page, people);
  await page.goto(`/manage/users/${sam.id}`);

  await expect(heading(page)).toHaveText(sam.displayName);
  await expect(main(page).getByRole('link')).toHaveText(['Edit']);
  await expect(main(page).getByRole('button')).toHaveCount(0);
  await expect(main(page).getByRole('separator')).toHaveCount(1);
  const notice = main(page).getByText(lastManagerNotice);
  await expect(notice).toBeVisible();
  // Below the rule, in the content column, never greyed: body text on the notice's tint (status-messages.md).
  const [rule, noticeBox] = [await boxOf(main(page).getByRole('separator')), await boxOf(notice)];
  expect(noticeBox.y).toBeGreaterThan(rule.y);
  await expect(notice).toHaveCSS('color', 'rgb(11, 12, 12)');
  await expect(page.locator('[disabled]')).toHaveCount(0);

  // The notice and the Status row add no stop (users.md *Focus order*).
  await pageBar(page).getByRole('link').focus();
  await page.keyboard.press('Tab');
  await expect(main(page).getByRole('link', { name: 'Edit' })).toBeFocused();
  await page.keyboard.press('Tab');
  expect(await whereFocusIs(page)).toBe('outside the page');

  await expectAxeToPass(page);
});

test("with no connection, a user's page shows its fallback <h1>, its failure and Try again, and passes axe", async ({
  page,
}) => {
  await serveMe(page, manager);
  await failUsers(page);
  await page.goto(`/manage/users/${jo.id}`);

  await expect(statusLine(page)).toHaveText('This user did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(heading(page)).toHaveText('User');
  await expect(tryAgain(page)).toBeVisible();
  await expect(pageBar(page).getByRole('link')).toHaveText('Users');

  await expectAxeToPass(page);
});

test('an ID not on the list shows Page not found with the nav and no back link', async ({ page }) => {
  await serveMe(page, manager);
  await serveUsers(page, people);
  const path = '/manage/users/4f2c8e1a-6b3d-4a97-8e05-1d7b9c3f2a68';

  await page.goto(path);
  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle(title('Page not found'));
  await expect(page).toHaveURL(path);
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
  await expect(marked(page)).toHaveCount(0);
  await expect(pageBar(page).getByRole('link')).toHaveCount(0);
});

test("a worker at the Users list, Invite user, a user's page and Edit user meets Page not found", async ({ page }) => {
  await serveMe(page, worker);

  for (const path of ['/manage/users', '/manage/users/new', `/manage/users/${jo.id}`, `/manage/users/${jo.id}/edit`]) {
    await page.goto(path);
    await expect(heading(page)).toHaveText('Page not found');
    await expect(main(page).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
    await expect(page).toHaveTitle(title('Page not found'));
  }
});

test('from Manage, through Users and each user, Manage is marked and no name or email is kept anywhere', async ({
  page,
}) => {
  await serveMe(page, manager);
  await serveUsers(page, people);
  await page.goto('/manage');

  /**
   * Expects the page's title, Manage alone marked, the address, which holds the user's ID alone, and `history.state`,
   * with React Router's own key and index and nothing of the app's own in its usr (app-shell.md › Structure).
   */
  async function expectPage(pageName: string, path: string) {
    await expect(page).toHaveTitle(title(pageName));
    await expect(marked(page)).toHaveText(['Manage']);
    await expect(marked(page)).toHaveAttribute('aria-current', 'true');
    await expect(page).toHaveURL(path);
    await expect
      .poll(() => page.evaluate(() => history.state as unknown), { message: `history.state at ${path}` })
      .toEqual({ usr: null, key: expect.any(String), idx: expect.any(Number) });
  }

  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  await expectLists(page, sorted);
  await expectPage('Users', '/manage/users');
  for (const { id, displayName } of people) {
    await main(page).getByRole('link', { name: displayName, exact: true }).click();
    await expect(heading(page)).toHaveText(displayName);
    await expectPage('User', `/manage/users/${id}`);

    await pageBar(page).getByRole('link', { name: 'Users' }).click();
    await expectLists(page, sorted);
    await expectPage('Users', '/manage/users');
  }

  // Nothing of anyone's is kept on the device (D22, design.md §9.6).
  const stored = await page.evaluate(async () => ({
    localStorage: localStorage.length,
    sessionStorage: sessionStorage.length,
    indexedDB: (await indexedDB.databases()).length,
    cacheStorage: (await caches.keys()).length,
  }));
  expect(stored).toEqual({ localStorage: 0, sessionStorage: 0, indexedDB: 0, cacheStorage: 0 });
});

test('back from a user far down the list, by the back link or browser Back, focuses their row where it was', async ({
  page,
}) => {
  // More users than fit at 320 px, so that the one opened is far down the list, each with a made-up ID and name (A35).
  const many = Array.from({ length: 30 }, (_, index) => {
    const number = String(index + 1).padStart(2, '0');
    return madeUpUser(
      `6b1f0c4e-2d7a-4e93-8a5c-1f0e9d7c30${number}`,
      `Sample Person ${number}`,
      `sample.person.${number}@example.org`,
      'Active',
    );
  });
  await serveMe(page, manager);
  await serveUsers(page, [sam, ...many]);
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto('/manage');
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  const row = main(page).getByRole('link', { name: 'Sample Person 25', exact: true });
  await row.scrollIntoViewIfNeeded();
  const scrollY = () => page.evaluate(() => window.scrollY);
  const kept = await scrollY();
  expect(kept).toBeGreaterThan(0);
  // React Router's own key and index, and nothing of the app's in its usr (app-shell.md › Structure).
  const listEntry = await page.evaluate(() => history.state as unknown);
  expect(listEntry).toEqual({ usr: null, key: expect.any(String), idx: expect.any(Number) });

  for (const [way, goBack] of [
    ['the back link', () => pageBar(page).getByRole('link', { name: 'Users' }).click()],
    ['browser Back', () => page.goBack()],
  ] as const) {
    await row.click();
    await expect(heading(page)).toHaveText('Sample Person 25');
    await expect(heading(page)).toBeFocused();
    expect(await scrollY(), way).toBe(0);

    await goBack();

    await expect(row, way).toBeFocused();
    await expect(row, way).toBeInViewport();
    expect(await scrollY(), way).toBe(kept);
    // The list's own entry again, so the back link went back one entry, as browser Back does, adding none.
    expect(await page.evaluate(() => history.state as unknown), way).toEqual(listEntry);
  }

  // Nothing of anyone's is kept on the device (D22, design.md §9.6).
  const stored = await page.evaluate(async () => ({
    localStorage: localStorage.length,
    sessionStorage: sessionStorage.length,
    indexedDB: (await indexedDB.databases()).length,
    cacheStorage: (await caches.keys()).length,
  }));
  expect(stored).toEqual({ localStorage: 0, sessionStorage: 0, indexedDB: 0, cacheStorage: 0 });
});
