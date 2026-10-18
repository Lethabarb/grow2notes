import { randomUUID } from 'node:crypto';
import type { Locator, Page, Request, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test, type Invite, type VirtualAuthenticator } from './fixtures.ts';
import { expectNothingKept } from './kept.ts';
import { manager, serveMe } from './me.ts';
import { answerInvites, holdNextInvite, people, serveUsers } from './users.ts';

// Invite user in the published app (S00.06.02), against users.md's criteria for access and Invite, AC1, AC3 and AC13
// to AC20, with axe in each state (AC39). Two tests sign in for real, through setup with a passkey on the virtual
// authenticator, so that Send invite meets the app's own POST /api/admin/users; the others answer /api/auth/me and
// /api/admin/users in the browser (me.ts, users.ts), so that no invite reaches the app.

const title = (pageName: string) => `Grow2Notes – ${pageName}`;

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nameField = (page: Page) => main(page).getByRole('textbox', { name: 'Name' });
const emailField = (page: Page) => main(page).getByRole('textbox', { name: 'Email address' });
const workerRadio = (page: Page) => main(page).getByRole('radio', { name: 'Worker' });
const managerRadio = (page: Page) => main(page).getByRole('radio', { name: 'Manager' });
const sendInvite = (page: Page) => main(page).getByRole('button', { name: /^(Send invite|Sending…)$/ });
const alertLine = (page: Page) => main(page).getByRole('alert');
// The error summary: the focusable box round its heading (form-validation.md *Anatomy*).
const summary = (page: Page) => main(page).locator('div[tabindex="-1"]:has(> h2)');
const pageBar = (page: Page) => page.locator('#page-bar');

// The words of users.md's *States* › Invite user.
const nameHint = 'Shown on the notes they write';
const emailHint = 'Grow2Notes will email them a setup link. It works once and lasts 7 days.';
const enterName = 'Enter the person’s name';
const nameTooLong = 'Name must be 100 characters or less';
const enterEmail = 'Enter an email address';
const emailFormat = 'Enter an email address in the correct format, like name@example.com';
const emailInUse =
  'Another account already uses this email address. Use a different one, or find the person in the Users list.';
const chooseRole = 'Choose Worker or Manager';
const somethingWentWrong = 'Not sent: something went wrong. Try again.';

const isInvite = (request: Request) =>
  request.method() === 'POST' && new URL(request.url()).pathname === '/api/admin/users';
const answersInvite = (response: Response) => isInvite(response.request());

/** The id of the focused element, or, for one with none, its element name. */
const focusedId = (page: Page) =>
  page.evaluate(() => document.activeElement?.id || document.activeElement?.localName || '');

/**
 * Counts the invites that the page sends from now on. Gives a function that gives the count once the browser has
 * reported every request that the page sent before the call: it reports them in the order they were sent, so it has
 * reported each once the test has the answer to a request of its own (tests/README.md).
 */
function countInvites(page: Page): () => Promise<number> {
  let count = 0;
  page.on('request', (request) => {
    if (isInvite(request)) {
      count += 1;
    }
  });

  return async () => {
    await page.evaluate(async () => {
      await fetch('/healthz');
    });
    return count;
  };
}

/** Expects the summary to list these messages, each linked to the field with the id after its `#`, or unlinked. */
async function expectSummary(page: Page, items: [message: string, href?: string][]) {
  const listed = summary(page).getByRole('listitem');
  await expect(listed).toHaveText(items.map(([message]) => message));
  for (const [index, [, href]] of items.entries()) {
    const link = listed.nth(index).getByRole('link');
    await (href === undefined ? expect(link).toHaveCount(0) : expect(link).toHaveAttribute('href', href));
  }
}

/** Expects the fields to hold what was typed, with Manager chosen when `role` says so. */
async function expectKept(page: Page, name: string, email: string, role: 'Worker' | 'Manager' = 'Worker') {
  await expect(nameField(page)).toHaveValue(name);
  await expect(emailField(page)).toHaveValue(email);
  await expect(role === 'Worker' ? workerRadio(page) : managerRadio(page)).toBeChecked();
}

/**
 * Signs the invited manager in through setup with a passkey on the virtual authenticator, as setup.spec.ts does, and
 * opens Invite user from the Users list, through Manage, as a manager does.
 */
async function signInAndOpenInviteUser(page: Page, invite: Invite, authenticator: VirtualAuthenticator) {
  await page.goto(invite.setupLink);
  await main(page).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(heading(page)).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(1);

  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  await expect(main(page).getByRole('list')).toBeVisible();
  await main(page).getByRole('link', { name: 'Invite user', exact: true }).click();
  await expect(heading(page)).toHaveText('Invite user');
}

/** Invite user for me.ts's manager, opened from a Users list of users.ts's people, both answered in the browser. */
async function openInviteUser(page: Page) {
  await serveMe(page, manager);
  await serveUsers(page, people);
  await page.goto('/manage/users');
  await main(page).getByRole('link', { name: 'Invite user', exact: true }).click();
  await expect(heading(page)).toHaveText('Invite user');
}

/** The row of the Users list whose link is named `name`, and the lines under the name, as they show. */
async function rowLines(page: Page, name: string): Promise<string[]> {
  const row: Locator = main(page)
    .getByRole('listitem')
    .filter({ has: page.getByRole('link', { name, exact: true }) });
  return row.locator('p > span').evaluateAll((lines) =>
    lines.map((line) => {
      const shown = line.cloneNode(true) as HTMLElement;
      shown.querySelectorAll('.visually-hidden').forEach((hidden) => {
        hidden.remove();
      });
      return shown.textContent ?? '';
    }),
  );
}

test('signed in, a double tap sends one invite, "Sending…" from 400 ms, then the list shows the row', async ({
  page,
  invite,
  authenticator,
}) => {
  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await signInAndOpenInviteUser(page, invite, authenticator);
  const person = { displayName: 'Robin Sample', email: `robin.sample.${randomUUID()}@example.org` };
  await nameField(page).fill(person.displayName);
  await emailField(page).fill(person.email);
  await expectNothingKept(page, person.displayName, person.email);
  const invitesSent = countInvites(page);
  const heldInvite = await holdNextInvite(page);

  // Paused a second ahead of the page's time, so that it moves only as the test runs it on from the double tap.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  await sendInvite(page).dblclick();
  const route = await heldInvite();
  await page.clock.runFor(399);
  await expect(sendInvite(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(sendInvite(page)).toHaveAccessibleName('Send invite');
  await page.clock.runFor(1);
  await expect(sendInvite(page)).toHaveAccessibleName('Sending…');
  await expect(sendInvite(page)).toBeFocused();

  await page.clock.resume();
  const answered = page.waitForResponse(answersInvite);
  await route.continue();
  expect((await answered).status()).toBe(201);

  // The list, in the form's place, with focus on its <h1>, the new row Invited · Worker, and no banner (AC18).
  await expect(page).toHaveURL('/manage/users');
  await expect(heading(page)).toHaveText('Users');
  await expect(heading(page)).toBeFocused();
  await expect(main(page).getByRole('link', { name: person.displayName, exact: true })).toBeVisible();
  expect(await rowLines(page, person.displayName)).toEqual([person.email, 'Invited · Worker']);
  await expect(main(page).getByRole('alert')).toHaveCount(0);
  for (const region of await main(page).getByRole('status').all()) {
    await expect(region).toBeEmpty();
  }

  expect(await invitesSent()).toBe(1);
  await expectNothingKept(page, person.displayName, person.email);

  // Back goes to the list before the form, which the list replaced, not to the form.
  await page.goBack();
  await expect(page).toHaveURL('/manage/users');
  await expect(heading(page)).toHaveText('Users');
  await expect(nameField(page)).toHaveCount(0);
});

test("signed in for real, the manager's own address in capitals, then zoë@example.org, are refused on Email", async ({
  page,
  invite,
  authenticator,
}) => {
  await signInAndOpenInviteUser(page, invite, authenticator);
  const ownInCapitals = invite.email.toUpperCase();
  await nameField(page).fill('Sam Sample');
  await emailField(page).fill(ownInCapitals);
  await main(page).getByText('Manager', { exact: true }).click();

  // The app's 409 user.email_in_use, on Email, linked from the summary, which takes focus (AC19).
  let answered = page.waitForResponse(answersInvite);
  await sendInvite(page).click();
  expect((await answered).status()).toBe(409);
  await expect(summary(page)).toBeFocused();
  await expectSummary(page, [[emailInUse, '#email']]);
  await expect(emailField(page)).toHaveAccessibleDescription(`${emailHint} Error: ${emailInUse}`);
  await expect(emailField(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(page).toHaveTitle(`Error: ${title('Invite user')}`);
  await expectKept(page, 'Sam Sample', ownInCapitals, 'Manager');
  await expectAxeToPass(page);

  // The app's 422 under email for an address with a letter outside ASCII, which the loose check passes.
  await emailField(page).fill('zoë@example.org');
  await expect(summary(page)).toHaveCount(0);
  answered = page.waitForResponse(answersInvite);
  await sendInvite(page).click();
  expect((await answered).status()).toBe(422);
  await expect(summary(page)).toBeFocused();
  await expectSummary(page, [[emailFormat, '#email']]);
  await expect(emailField(page)).toHaveAccessibleDescription(`${emailHint} Error: ${emailFormat}`);
  await expect(page).toHaveTitle(`Error: ${title('Invite user')}`);
  await expectKept(page, 'Sam Sample', 'zoë@example.org', 'Manager');
  await expectAxeToPass(page);
});

test('Invite user opens with Worker chosen, both fields empty and no error, in Tab order, and passes axe', async ({
  page,
}) => {
  await openInviteUser(page);

  await expect(page).toHaveURL('/manage/users/new');
  await expect(page).toHaveTitle(title('Invite user'));
  await expect(page.getByRole('banner').locator('[aria-current]')).toHaveText(['Manage']);
  await expect(pageBar(page).getByRole('link')).toHaveText('Users');
  await expect(pageBar(page).getByRole('link')).toHaveAttribute('href', '/manage/users');
  await expectKept(page, '', '');
  await expect(managerRadio(page)).not.toBeChecked();
  await expect(summary(page)).toHaveCount(0);
  await expect(alertLine(page)).toBeEmpty();
  await expect(nameField(page)).toHaveAccessibleDescription(nameHint);
  await expect(emailField(page)).toHaveAccessibleDescription(emailHint);
  await expect(main(page).getByRole('group', { name: 'Role' })).toBeVisible();
  await expect(workerRadio(page)).toHaveAccessibleDescription('Writes notes and reads past notes');
  await expect(managerRadio(page)).toHaveAccessibleDescription(
    'Can also review flagged notes, download reports, and set up participants and users',
  );
  await expect(page.locator('[disabled]')).toHaveCount(0);
  await expectAxeToPass(page);

  // The back link, Name, Email address, Role as one stop on the chosen radio, then Send invite (users.md *Focus
  // order*); the arrow keys move within Role and choose as they move.
  await pageBar(page).getByRole('link').focus();
  const stops: string[] = [];
  for (let stop = 0; stop < 4; stop += 1) {
    await page.keyboard.press('Tab');
    stops.push(await focusedId(page));
  }
  expect(stops).toEqual(['displayName', 'email', 'role-worker', 'button']);
  await page.keyboard.press('Shift+Tab');
  await page.keyboard.press('ArrowDown');
  await expect(managerRadio(page)).toBeChecked();
  await expect(managerRadio(page)).toBeFocused();
});

test('Send invite with both fields empty focuses the summary of both messages, and each fix clears its own', async ({
  page,
}) => {
  await openInviteUser(page);
  const invitesSent = countInvites(page);

  await sendInvite(page).click();
  await expect(summary(page)).toBeFocused();
  await expectSummary(page, [
    [enterName, '#displayName'],
    [enterEmail, '#email'],
  ]);
  await expect(nameField(page)).toHaveAccessibleDescription(`${nameHint} Error: ${enterName}`);
  await expect(emailField(page)).toHaveAccessibleDescription(`${emailHint} Error: ${enterEmail}`);
  await expect(page).toHaveTitle(`Error: ${title('Invite user')}`);
  expect(await invitesSent()).toBe(0);
  await expectAxeToPass(page);

  // A summary link goes to its field, and the address does not change (form-validation.md).
  await summary(page).getByRole('link', { name: enterEmail }).click();
  await expect(emailField(page)).toBeFocused();
  await expect(page).toHaveURL('/manage/users/new');

  // AC15: as each field is fixed, its message and summary item go, and focus stays where the typing is.
  await nameField(page).fill('Alex Park');
  await expectSummary(page, [[enterEmail, '#email']]);
  await expect(nameField(page)).toBeFocused();
  await emailField(page).fill('alex');
  await expectSummary(page, [[emailFormat, '#email']]);
  await emailField(page).fill('alex@example.org');
  await expect(summary(page)).toHaveCount(0);
  await expect(page).toHaveTitle(title('Invite user'));
  await expect(emailField(page)).toBeFocused();

  // AC16: "alex", and a name of 101 characters, and one pasted longer still is kept whole.
  await emailField(page).fill('alex');
  await nameField(page).fill('A'.repeat(101));
  await sendInvite(page).click();
  await expectSummary(page, [
    [nameTooLong, '#displayName'],
    [emailFormat, '#email'],
  ]);
  await nameField(page).fill('B'.repeat(150));
  await expect(nameField(page)).toHaveValue('B'.repeat(150));
  expect(await invitesSent()).toBe(0);
});

test('a 422 under role and one under a key with no field show as users.md says, focused, with values kept', async ({
  page,
}) => {
  await openInviteUser(page);
  await answerInvites(page, {
    status: 422,
    problem: { code: 'validation.failed', errors: { role: ['x'], organisationId: ['y'] } },
  });
  await nameField(page).fill('Alex Park');
  await emailField(page).fill('alex.park@example.org');
  await managerRadio(page).check();

  await sendInvite(page).click();

  await expect(summary(page)).toBeFocused();
  await expectSummary(page, [[somethingWentWrong], [chooseRole, '#role-worker']]);
  await expect(main(page).getByRole('group', { name: 'Role' })).toHaveAccessibleDescription(`Error: ${chooseRole}`);
  await expect(page).toHaveTitle(`Error: ${title('Invite user')}`);
  await expectKept(page, 'Alex Park', 'alex.park@example.org', 'Manager');
  await expect(alertLine(page)).toBeEmpty();
  await expectAxeToPass(page);

  // Choosing a role clears its message; the unknown key's item stays until the next press.
  await workerRadio(page).check();
  await expectSummary(page, [[somethingWentWrong]]);
});

for (const { cut, answer, sentence } of [
  { cut: 'the network cut', answer: 'no connection', sentence: 'Not sent: no connection. Try again.' },
  { cut: 'a 500', answer: { status: 500 }, sentence: somethingWentWrong },
] as const) {
  test(`with ${cut}, Send invite says so above the button, keeps focus there and clears nothing`, async ({
    page,
  }) => {
    await openInviteUser(page);
    await answerInvites(page, answer);
    await nameField(page).fill('Alex Park');
    await emailField(page).fill('alex.park@example.org');
    await managerRadio(page).check();

    await sendInvite(page).click();

    await expect(alertLine(page)).toHaveText(sentence);
    await expect(sendInvite(page)).toBeFocused();
    await expect(sendInvite(page)).not.toHaveAttribute('aria-disabled');
    await expect(summary(page)).toHaveCount(0);
    await expect(page).toHaveTitle(title('Invite user'));
    await expectKept(page, 'Alex Park', 'alex.park@example.org', 'Manager');
    await expectAxeToPass(page);
  });
}

test('while an invite is out, Send invite is busy, says "Sending…", and passes axe', async ({ page }) => {
  await openInviteUser(page);
  const heldInvite = await holdNextInvite(page);
  await nameField(page).fill('Alex Park');
  await emailField(page).fill('alex.park@example.org');

  await sendInvite(page).click();
  const route = await heldInvite();

  await expect(sendInvite(page)).toHaveAccessibleName('Sending…');
  await expect(sendInvite(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(page.locator('[disabled]')).toHaveCount(0);
  await expectAxeToPass(page);
  await route.abort('internetdisconnected');
  await expect(alertLine(page)).toHaveText('Not sent: no connection. Try again.');
});
