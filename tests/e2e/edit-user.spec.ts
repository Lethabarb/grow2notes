import { randomUUID } from 'node:crypto';
import type { Page, Request, Route } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops } from './focus.ts';
import { expectFoundationsToPass } from './foundations.ts';
import { expectNothingKept } from './kept.ts';
import { failMe, manager, serveMe } from './me.ts';
import { holdNext, recordRequests } from './requests.ts';
import { expectViewA } from './signedOut.ts';
import {
  answerEdits,
  chris,
  edits,
  failUsers,
  inviteThroughThePage,
  jo,
  kim,
  people,
  sam,
  serveUsers,
  signInAndOpenUsers,
  withChanged,
  type AdminUser,
} from './users.ts';

// Edit user in the published app (S00.06.06), against users.md's AC1, AC3, AC33 for the PUT, AC34 to AC38 and AC39,
// with axe in each state. One test signs in for real, through setup with a passkey on the virtual authenticator, so
// that Save meets the app's own PUT /api/admin/users/{id}; the others answer /api/auth/me, /api/admin/users and the
// save in the browser (me.ts, users.ts), so that none reaches the app.

const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nameField = (page: Page) => main(page).getByRole('textbox', { name: 'Name' });
const emailField = (page: Page) => main(page).getByRole('textbox', { name: 'Email address' });
const workerRadio = (page: Page) => main(page).getByRole('radio', { name: 'Worker' });
const managerRadio = (page: Page) => main(page).getByRole('radio', { name: 'Manager' });
const saveButton = (page: Page) => main(page).getByRole('button', { name: /^(Save|Saving…)$/ });
const alertLine = (page: Page) => main(page).getByRole('alert');
// The error summary: the focusable box round its heading (form-validation.md *Anatomy*).
const summary = (page: Page) => main(page).locator('div[tabindex="-1"]:has(> h2)');
// The region's line, beside the page's visually hidden status region (users.md *Live regions*).
const statusLine = (page: Page) => main(page).getByRole('status').and(page.locator(':not(.visually-hidden)'));
const pageBar = (page: Page) => page.locator('#page-bar');
// The user page's facts, by their terms (users.md component C item 4).
const fact = (page: Page, term: string) =>
  main(page)
    .locator('dl > div')
    .filter({ has: page.locator('dt', { hasText: new RegExp(`^${term}$`) }) })
    .locator('dd');

// The words of users.md's component D and *States* › Edit user, with the typographic apostrophe of src/copy.
const nameHint = 'Shown on the notes they write';
const emailHint = 'Changing the email resets sign-in and sends a setup link to the new address.';
const ownEmailHint =
  'Changing your email resets your sign-in. You will be signed out, and a setup link will be sent to the new address.';
const lastManagerNotice =
  'You are the only active manager, so you cannot change your email or make yourself a worker. First make someone ' +
  'else a manager.';
const lastManagerRole =
  'You are the only active manager, so you cannot make yourself a worker. Make someone else a manager first.';
const lastManagerEmail =
  'You are the only active manager, so you cannot change your email. Make someone else a manager first.';
const changedWhileEditing =
  'Someone else changed this user while you were editing. Check the details, then save again.';
const emailFormat = 'Enter an email address in the correct format, like name@example.com';
const emailInUse =
  'Another account already uses this email address. Use a different one, or find the person in the Users list.';

const isSave = (request: Request) => request.method() === 'PUT';

/** Expects the summary to list these messages, each linked to the field with the id after its `#`. */
async function expectSummary(page: Page, items: [message: string, href: string][]) {
  const listed = summary(page).getByRole('listitem');
  await expect(listed).toHaveText(items.map(([message]) => message));
  for (const [index, [, href]] of items.entries()) {
    await expect(listed.nth(index).getByRole('link')).toHaveAttribute('href', href);
  }
}

/** Expects the fields to hold these values, the radio of `role` chosen. */
async function expectKept(page: Page, name: string, email: string, role: 'Worker' | 'Manager') {
  await expect(nameField(page)).toHaveValue(name);
  await expect(emailField(page)).toHaveValue(email);
  await expect(role === 'Worker' ? workerRadio(page) : managerRadio(page)).toBeChecked();
}

/** The PUTs that the page sends from now on, as their `If-Match` and their bodies, read once the test asks. */
function recordSaves(page: Page): () => { ifMatch: string | undefined; body: unknown }[] {
  const saves: { ifMatch: string | undefined; body: unknown }[] = [];
  page.on('request', (request) => {
    if (isSave(request)) {
      saves.push({ ifMatch: request.headers()['if-match'], body: request.postDataJSON() as unknown });
    }
  });
  return () => saves;
}

/**
 * `user`'s Edit user page for me.ts's manager, Sam Lee, from a list of `users`, both answered in the browser, opened
 * by Edit on their page, as a manager opens it.
 */
async function openEdit(page: Page, user: AdminUser, users: readonly AdminUser[] = people) {
  await serveMe(page, manager);
  await serveUsers(page, users);
  await page.goto(`/manage/users/${user.id}`);
  await main(page).getByRole('link', { name: 'Edit', exact: true }).click();
  await expect(heading(page)).toHaveText('Edit user');
  await expect(nameField(page)).toHaveValue(user.displayName);
}

/** Lets a save held by `holdNext` go, answered as the app answers it: `200`, with no body and no `ETag` (D78). */
async function saveHeld(held: () => Promise<Route>) {
  await (await held()).fulfill({ status: 200 });
}

test("Edit user opens on its <h1> with Jo Smith's values and the hint for someone else, in Tab order (AC34)", async ({
  page,
}) => {
  await openEdit(page, jo);

  await expect(page).toHaveURL(`/manage/users/${jo.id}/edit`);
  await expect(page).toHaveTitle(title('Edit user'));
  await expect(heading(page)).toBeFocused();
  await expect(page.getByRole('banner').locator('[aria-current]')).toHaveText(['Manage']);
  await expect(pageBar(page).getByRole('link')).toHaveText('Jo Smith');
  await expect(pageBar(page).getByRole('link')).toHaveAttribute('href', `/manage/users/${jo.id}`);
  await expectKept(page, 'Jo Smith', 'jo.smith@example.org', 'Worker');
  await expect(nameField(page)).toHaveAccessibleDescription(nameHint);
  await expect(emailField(page)).toHaveAccessibleDescription(emailHint);
  await expect(main(page).getByText(lastManagerNotice)).toHaveCount(0);
  await expect(summary(page)).toHaveCount(0);
  await expect(alertLine(page)).toBeEmpty();
  await expect(page.locator('[disabled]')).toHaveCount(0);
  await expectAxeToPass(page);

  // The back link, Name, Email address, Role as one stop on the chosen radio, then Save (users.md *Focus order*).
  await pageBar(page).getByRole('link').focus();
  await expectStops(page, 'Tab', ['Name', 'Email address', 'Worker', 'Save']);

  // Back by the back link, one entry, as browser Back goes, to Jo Smith's page, its <h1> focused.
  await pageBar(page).getByRole('link').click();
  await expect(page).toHaveURL(`/manage/users/${jo.id}`);
  await expect(heading(page)).toHaveText('Jo Smith');
  await expect(heading(page)).toBeFocused();
});

test('as the only active manager, your own page has the notice; Worker and a new address are refused (AC35)', async ({
  page,
}) => {
  await openEdit(page, sam);
  const sent = recordRequests(page);

  await expect(emailField(page)).toHaveAccessibleDescription(ownEmailHint);
  const [headingBox, noticeBox, nameBox] = await Promise.all(
    [heading(page), main(page).getByText(lastManagerNotice), nameField(page)].map((part) => part.boundingBox()),
  );
  expect(noticeBox!.y).toBeGreaterThan(headingBox!.y);
  expect(nameBox!.y).toBeGreaterThan(noticeBox!.y);
  await expectAxeToPass(page);

  await workerRadio(page).check();
  await saveButton(page).click();
  await expect(summary(page)).toBeFocused();
  await expectSummary(page, [[lastManagerRole, '#role-worker']]);
  await expect(main(page).getByRole('group', { name: 'Role' })).toHaveAccessibleDescription(
    `Error: ${lastManagerRole}`,
  );
  await expect(page).toHaveTitle(`Error: ${title('Edit user')}`);
  await expectAxeToPass(page);

  // Manager again clears it, and its prefix in the title.
  await managerRadio(page).check();
  await expect(summary(page)).toHaveCount(0);
  await expect(page).toHaveTitle(title('Edit user'));

  await emailField(page).fill('sam.leigh@example.org');
  await saveButton(page).click();
  await expect(summary(page)).toBeFocused();
  await expectSummary(page, [[lastManagerEmail, '#email']]);
  await expect(emailField(page)).toHaveAccessibleDescription(`${ownEmailHint} Error: ${lastManagerEmail}`);
  expect((await sent()).filter((request) => request.startsWith('PUT'))).toEqual([]);

  // Your own address in other letter case is no new address (D76): it clears the message as typed, and is saved,
  // landing on your own page with no reload.
  await answerEdits(page, 'saved');
  await serveUsers(page, withChanged(people, sam, { email: 'Sam.Lee@example.org' }));
  await page.evaluate(() => {
    (window as unknown as { notReloaded: boolean }).notReloaded = true;
  });
  await emailField(page).fill('Sam.Lee@example.org');
  await expect(summary(page)).toHaveCount(0);
  await saveButton(page).click();

  await expect(page).toHaveURL(`/manage/users/${sam.id}`);
  await expect(heading(page)).toHaveText('Sam Lee');
  await expect(heading(page)).toBeFocused();
  await expect(fact(page, 'Email address')).toHaveText('Sam.Lee@example.org');
  expect(await page.evaluate(() => (window as unknown as { notReloaded?: boolean }).notReloaded)).toBe(true);
  expect((await sent()).filter((request) => request.startsWith('PUT'))).toEqual([
    `PUT /api/admin/users/${sam.id} "stamp-${sam.id}"`,
  ]);
});

test('while the list loads, the <h1> shows at once with no field, then "Loading user…", and passes axe', async ({
  page,
}) => {
  await serveMe(page, manager);
  const heldList = await holdNext(page, '**/api/admin/users');
  await page.goto(`/manage/users/${jo.id}/edit`);
  const list = await heldList();

  await expect(heading(page)).toHaveText('Edit user');
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
  await expect(statusLine(page)).toHaveText('Loading user…');
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
  await expectAxeToPass(page);

  await list.fulfill({ json: people });
  await expectKept(page, 'Jo Smith', 'jo.smith@example.org', 'Worker');
  await expect(statusLine(page)).toBeEmpty();
});

test('with no connection, Edit user keeps its <h1> above its failure and Try again, and passes axe', async ({
  page,
}) => {
  await serveMe(page, manager);
  await failUsers(page);
  await page.goto(`/manage/users/${jo.id}/edit`);

  await expect(statusLine(page)).toHaveText('This user did not load: no connection. Try again.', { timeout: 2_000 });
  await expect(heading(page)).toHaveText('Edit user');
  await expect(main(page).getByRole('button', { name: 'Try again' })).toBeVisible();
  await expect(main(page).getByRole('textbox')).toHaveCount(0);
  await expectAxeToPass(page);
});

for (const { given, user, fill, changed, status } of [
  {
    given: 'a new name and role for Jo Smith, who stays Active',
    user: jo,
    fill: { name: '  Jo Smyth ', role: 'Manager' as const },
    changed: { displayName: 'Jo Smyth', role: 'Manager' as const },
    status: 'Active',
  },
  {
    given: 'a new address for Jo Smith, who is then Invited',
    user: jo,
    fill: { email: 'jo.smyth@example.org' },
    changed: { email: 'jo.smyth@example.org', status: 'Invited' as const, activatedAtUtc: null },
    status: 'Invited',
  },
  {
    given: 'a new address for Deactivated Chris Ng, who stays Deactivated',
    user: chris,
    fill: { email: 'chris.ng@example.com' },
    changed: { email: 'chris.ng@example.com' },
    status: 'Deactivated',
  },
]) {
  test(`Save of ${given} sends one PUT with If-Match, then shows their page by a replace (AC36)`, async ({
    page,
  }) => {
    await openEdit(page, user);
    await answerEdits(page, 'saved');
    const after = withChanged(people, user, changed);
    await serveUsers(page, after);
    const saves = recordSaves(page);
    if (fill.name !== undefined) {
      await nameField(page).fill(fill.name);
    }
    if (fill.email !== undefined) {
      await emailField(page).fill(fill.email);
    }
    if (fill.role !== undefined) {
      await managerRadio(page).check();
    }

    await saveButton(page).click();

    const shown = after.find(({ id }) => id === user.id)!;
    await expect(page).toHaveURL(`/manage/users/${user.id}`);
    await expect(heading(page)).toHaveText(shown.displayName);
    await expect(heading(page)).toBeFocused();
    await expect(page).toHaveTitle(title('User'));
    await expect(fact(page, 'Email address')).toHaveText(shown.email);
    await expect(fact(page, 'Role')).toHaveText(shown.role);
    await expect(fact(page, 'Status')).toHaveText(status);
    // No banner (users.md component C item 2).
    await expect(main(page).locator('div[role="alert"]')).toBeEmpty();
    expect(saves()).toEqual([
      {
        ifMatch: `"stamp-${user.id}"`,
        body: { displayName: shown.displayName, email: shown.email, role: shown.role },
      },
    ]);
    await expectNothingKept(page, user.displayName, user.email, shown.displayName, shown.email);
    await expectAxeToPass(page);

    // The user's page replaced the form, so Back goes to the page that opened it.
    await page.goBack();
    await expect(page).toHaveURL(`/manage/users/${user.id}`);
    await expect(heading(page)).toHaveText(shown.displayName);
  });
}

test('a 412 says so above Save, keeps the values and focus there; a second Save sends the new stamp (AC37)', async ({
  page,
}) => {
  await openEdit(page, jo);
  await answerEdits(page, { status: 412, problem: { code: 'precondition.failed' } });
  await serveUsers(page, withChanged(people, jo, {}));
  const saves = recordSaves(page);
  await nameField(page).fill('Jo Smyth');
  await managerRadio(page).check();

  await saveButton(page).click();

  await expect(alertLine(page)).toHaveText(changedWhileEditing);
  await expect(saveButton(page)).toBeFocused();
  await expect(saveButton(page)).not.toHaveAttribute('aria-disabled');
  await expect(summary(page)).toHaveCount(0);
  await expect(page).toHaveTitle(title('Edit user'));
  await expectKept(page, 'Jo Smyth', 'jo.smith@example.org', 'Manager');
  await expectAxeToPass(page);

  await answerEdits(page, 'saved');
  await serveUsers(page, withChanged(people, jo, { displayName: 'Jo Smyth', role: 'Manager' }));
  await saveButton(page).click();

  await expect(heading(page)).toHaveText('Jo Smyth');
  expect(saves().map(({ ifMatch }) => ifMatch)).toEqual([`"stamp-${jo.id}"`, '"stamp-2"']);
});

test('while Save is out, it is busy, says "Saving…" from 400 ms, sends once, and passes axe', async ({ page }) => {
  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await openEdit(page, jo);
  const saves = recordSaves(page);
  const heldSave = await holdNext(page, edits);

  // Paused a second ahead of the page's time, so that it moves only as the test runs it on from the double tap.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  await saveButton(page).dblclick();
  const route = await heldSave();
  await page.clock.runFor(399);
  await expect(saveButton(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(saveButton(page)).toHaveAccessibleName('Save');
  await page.clock.runFor(1);
  await expect(saveButton(page)).toHaveAccessibleName('Saving…');
  await expect(page.locator('[disabled]')).toHaveCount(0);
  await page.clock.resume();
  await expectAxeToPass(page);

  await route.abort('internetdisconnected');
  await expect(alertLine(page)).toHaveText('Not saved: no connection. Try again.');
  expect(saves()).toHaveLength(1);
});

test.describe('your own account, with a second active manager', () => {
  test('saving yourself as a worker lands on Today, its <h1> focused, with no Manage in the nav (AC38)', async ({
    page,
  }) => {
    await openEdit(page, sam, [...people, kim]);
    const sent = recordRequests(page);
    const heldSave = await holdNext(page, edits);
    await workerRadio(page).check();

    await saveButton(page).click();
    // The app now answers /api/auth/me with the role that the save gave.
    await serveMe(page, { ...manager, role: 'Worker' });
    await saveHeld(heldSave);

    await expect(page).toHaveURL('/');
    await expect(heading(page)).toContainText('Today');
    await expect(heading(page)).toBeFocused();
    const nav = page.getByRole('navigation', { name: 'Main' });
    await expect(nav.getByRole('link', { name: 'Today' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Manage' })).toHaveCount(0);
    await expect(main(page).getByRole('alert')).toHaveCount(0);
    // The list is a manager's, which the app now refuses you (design.md §8.4).
    expect((await sent()).filter((request) => request === 'GET /api/admin/users')).toEqual([]);
    await expectAxeToPass(page);
  });

  test('saving your own new address loads the app afresh on sign-in (AC38)', async ({ page }) => {
    await openEdit(page, sam, [...people, kim]);
    const sent = recordRequests(page);
    const heldSave = await holdNext(page, edits);
    await emailField(page).fill('sam.leigh@example.org');

    await saveButton(page).click();
    // The new address ends Sam Lee's session, so the app's next answer to /api/auth/me is a 401.
    await failMe(page, 401);
    await saveHeld(heldSave);

    await expectViewA(page);
    await expect(page).toHaveURL('/');
    expect((await sent()).filter((request) => request === 'GET /api/admin/users')).toEqual([]);
  });

  test('saving your own new name lands on your page, and the Account panel names you by it', async ({ page }) => {
    await openEdit(page, sam, [...people, kim]);
    const heldSave = await holdNext(page, edits);
    await serveUsers(page, withChanged([...people, kim], sam, { displayName: 'Sam Leigh' }));
    await nameField(page).fill('Sam Leigh');

    await saveButton(page).click();
    await serveMe(page, { ...manager, displayName: 'Sam Leigh' });
    await saveHeld(heldSave);

    await expect(heading(page)).toHaveText('Sam Leigh');
    await page.getByRole('button', { name: 'Account' }).click();
    await expect(page.getByText('Signed in as Sam Leigh')).toBeVisible();
  });
});

test.describe('refused by the app', () => {
  test('an address another account has, then zoë@example.org, show on Email as on Invite user, focused', async ({
    page,
  }) => {
    await openEdit(page, jo);
    await answerEdits(page, { status: 409, problem: { code: 'user.email_in_use' } });
    await emailField(page).fill('ALEX.PARK@example.org');

    await saveButton(page).click();

    await expect(summary(page)).toBeFocused();
    await expectSummary(page, [[emailInUse, '#email']]);
    await expect(emailField(page)).toHaveAccessibleDescription(`${emailHint} Error: ${emailInUse}`);
    await expect(page).toHaveTitle(`Error: ${title('Edit user')}`);
    await expectKept(page, 'Jo Smith', 'ALEX.PARK@example.org', 'Worker');
    await expectAxeToPass(page);

    await answerEdits(page, { status: 422, problem: { code: 'validation.failed', errors: { email: ['x'] } } });
    await emailField(page).fill('zoë@example.org');
    await expect(summary(page)).toHaveCount(0);
    await saveButton(page).click();

    await expect(summary(page)).toBeFocused();
    await expectSummary(page, [[emailFormat, '#email']]);
    await expect(page).toHaveTitle(`Error: ${title('Edit user')}`);
    await expectKept(page, 'Jo Smith', 'zoë@example.org', 'Worker');
    await expect(alertLine(page)).toBeEmpty();
  });

  test('the race of 409 user.last_manager shows on the field changed, and the notice comes', async ({ page }) => {
    await openEdit(page, sam, [...people, kim]);
    await answerEdits(page, { status: 409, problem: { code: 'user.last_manager' } });
    // Kim Tran deactivated themselves as Sam Lee saved, so the list fetched again has Sam Lee the only one.
    await serveUsers(page, withChanged([...people, kim], kim, { status: 'Deactivated' }));
    await workerRadio(page).check();

    await saveButton(page).click();

    await expect(summary(page)).toBeFocused();
    await expectSummary(page, [[lastManagerRole, '#role-worker']]);
    await expect(main(page).getByText(lastManagerNotice)).toBeVisible();
    await expectAxeToPass(page);
  });

  for (const { cut, answer, sentence } of [
    { cut: 'the network cut', answer: 'no connection', sentence: 'Not saved: no connection. Try again.' },
    { cut: 'a 500', answer: { status: 500 }, sentence: 'Not saved: something went wrong. Try again.' },
  ] as const) {
    test(`with ${cut}, Save says so above it, keeps focus and values, and sends nothing again (AC33)`, async ({
      page,
    }) => {
      await page.clock.install();
      await openEdit(page, jo);
      await answerEdits(page, answer);
      const saves = recordSaves(page);
      await nameField(page).fill('Jo Smyth');

      await saveButton(page).click();

      await expect(alertLine(page)).toHaveText(sentence);
      await expect(saveButton(page)).toBeFocused();
      await expect(saveButton(page)).not.toHaveAttribute('aria-disabled');
      await expect(summary(page)).toHaveCount(0);
      await expect(page).toHaveTitle(title('Edit user'));
      await expectKept(page, 'Jo Smyth', 'jo.smith@example.org', 'Worker');
      await expectAxeToPass(page);
      await page.clock.runFor(60_000);
      expect(saves()).toHaveLength(1);
    });
  }
});

test('signed in for real, a worker invited through Invite user is renamed and given a new address: Invited', async ({
  page,
  invite,
  authenticator,
}) => {
  await signInAndOpenUsers(page, invite, authenticator);
  const person = { displayName: 'Robin Sample', email: `robin.sample.${randomUUID()}@example.org` };
  const renamed = { displayName: 'Robin Sampleton', email: `robin.sampleton.${randomUUID()}@example.org` };
  await inviteThroughThePage(page, person, 'Worker');
  await main(page).getByRole('link', { name: person.displayName, exact: true }).click();
  await main(page).getByRole('link', { name: 'Edit', exact: true }).click();
  await expectKept(page, person.displayName, person.email, 'Worker');
  await expectAxeToPass(page);

  await nameField(page).fill(renamed.displayName);
  await emailField(page).fill(renamed.email);
  await expectNothingKept(page, person.displayName, person.email, renamed.displayName, renamed.email);
  const saved = page.waitForResponse((response) => isSave(response.request()));
  await saveButton(page).click();

  // The app's answer, to the stamp that the list gave, which the page sent quoted.
  const save = await saved;
  expect(save.status()).toBe(200);
  expect(save.request().headers()['if-match']).toMatch(/^"[^"]+"$/);
  await expect(heading(page)).toHaveText(renamed.displayName);
  await expect(heading(page)).toBeFocused();
  await expect(fact(page, 'Email address')).toHaveText(renamed.email);
  await expect(fact(page, 'Status')).toHaveText('Invited');
  await expectNothingKept(page, person.displayName, person.email, renamed.displayName, renamed.email);
  await expectAxeToPass(page);
});

test('the form passes the foundations', async ({ page }) => {
  test.slow();
  await openEdit(page, jo);

  await expectFoundationsToPass(page);
});
