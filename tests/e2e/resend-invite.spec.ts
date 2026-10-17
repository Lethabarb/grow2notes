import { randomUUID } from 'node:crypto';
import type { Page, Request, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { manager, serveMe } from './me.ts';
import { holdNext } from './requests.ts';
import { alex, answerSetupLinks, chris, jo, people, sam, serveUsers, setupLinks } from './users.ts';

// Resend invite on an Invited user's page in the published app (S00.06.03), against users.md's AC21, AC23 and AC33,
// with axe with its sentence and with each failure (AC39). One test signs in for real, through setup with a passkey on
// the virtual authenticator, and invites a worker through Invite user, so that Resend invite meets the app's own
// POST /api/admin/users/{id}/setup-link and its Date header; the others answer /api/auth/me, /api/admin/users and the
// resend in the browser (me.ts, users.ts), so that none reaches the app.

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const resendInvite = (page: Page) => main(page).getByRole('button', { name: /^(Resend invite|Sending…)$/ });
// Resend invite's line for its result, the status region after its button (users.md *Live regions*).
const resendLine = (page: Page) => main(page).locator('button + [role="status"]');
// The action error region above the actions (users.md component C item 5).
const alertLine = (page: Page) => main(page).getByRole('alert');
// The summary list's Status row, the <div> round its term and its fact (users.md component C items 2 and 4).
const statusRow = (page: Page) =>
  main(page).locator('dl > div').filter({ has: page.locator('dt', { hasText: /^Status$/ }) });

// Two times at which the server sends a link, a minute apart, as its Date header writes them: 4:12 pm and 4:13 pm in
// Melbourne, which is on daylight saving time (UTC+11) from 4 October 2026.
const sentAt0512 = 'Wed, 14 Oct 2026 05:12:00 GMT';
const sentAt0513 = 'Wed, 14 Oct 2026 05:13:00 GMT';
const sentAt412pm = 'Setup link sent to alex.park@example.org at 4:12\u00A0pm. The old link no longer works.';
const sentAt413pm = 'Setup link sent to alex.park@example.org at 4:13\u00A0pm. The old link no longer works.';

const isSetupLink = (request: Request) =>
  request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/setup-link');
const answersSetupLink = (response: Response) => isSetupLink(response.request());

/**
 * Records the `If-Match` of each Resend invite that the page sends from now on. Gives a function that gives them once
 * the browser has reported every request that the page sent before the call, as it reports them in the order they
 * were sent (tests/README.md).
 */
function recordSetupLinks(page: Page): () => Promise<(string | undefined)[]> {
  const ifMatches: (string | undefined)[] = [];
  page.on('request', (request) => {
    if (isSetupLink(request)) {
      ifMatches.push(request.headers()['if-match']);
    }
  });

  return async () => {
    await page.evaluate(async () => {
      await fetch('/healthz');
    });
    return ifMatches;
  };
}

/**
 * The time of a Date header in Melbourne, as the page writes it (microcopy.md §3): "4:12 pm", with a non-breaking
 * space, and "midday" and "midnight" on the hour.
 */
function melbourneTime(dateHeader: string): string {
  const parts = new Intl.DateTimeFormat('en-AU', {
    timeZone: 'Australia/Melbourne',
    hour: 'numeric',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(dateHeader));
  const [hour, minute] = (['hour', 'minute'] as const).map((type) => Number(parts.find((p) => p.type === type)?.value));
  if (minute === 0 && (hour === 0 || hour === 12)) {
    return hour === 0 ? 'midnight' : 'midday';
  }

  return `${hour % 12 || 12}:${String(minute).padStart(2, '0')}\u00A0${hour < 12 ? 'am' : 'pm'}`;
}

/** Alex Park's page, for me.ts's manager, from a list of users.ts's people, both answered in the browser. */
async function openAlex(page: Page) {
  await serveMe(page, manager);
  await serveUsers(page, people);
  await page.goto(`/manage/users/${alex.id}`);
  await expect(heading(page)).toHaveText(alex.displayName);
}

test('signed in for real, a worker invited through Invite user gets the sentence, timed by the app', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.goto(invite.setupLink);
  await main(page).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(heading(page)).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(1);
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Users', exact: true }).click();
  await main(page).getByRole('link', { name: 'Invite user', exact: true }).click();
  const worker = { displayName: 'Robin Sample', email: `robin.sample.${randomUUID()}@example.org` };
  await main(page).getByRole('textbox', { name: 'Name' }).fill(worker.displayName);
  await main(page).getByRole('textbox', { name: 'Email address' }).fill(worker.email);
  await main(page).getByRole('button', { name: 'Send invite' }).click();
  await expect(heading(page)).toHaveText('Users');
  await main(page).getByRole('link', { name: worker.displayName, exact: true }).click();
  await expect(heading(page)).toHaveText(worker.displayName);
  await expect(main(page).getByRole('definition')).toHaveText([worker.email, 'Worker', 'Invited']);

  const answered = page.waitForResponse(answersSetupLink);
  await resendInvite(page).click();

  // The app's answer, to the stamp that the list gave, which the page sent quoted, at the time in its Date header.
  const response = await answered;
  expect(response.status()).toBe(204);
  expect(response.request().headers()['if-match']).toMatch(/^"[^"]+"$/);
  const sentAt = response.headers()['date'];
  expect(sentAt).toMatch(/ GMT$/);
  await expect(resendLine(page)).toHaveText(
    `Setup link sent to ${worker.email} at ${melbourneTime(sentAt)}. The old link no longer works.`,
  );
  await expect(resendInvite(page)).toBeFocused();
  await expect(resendInvite(page)).not.toHaveAttribute('aria-disabled');
  await expect(alertLine(page)).toBeEmpty();
  await expectAxeToPass(page);
});

test.describe('on a device whose clock is on another day, in Los Angeles', () => {
  test.use({ timezoneId: 'America/Los_Angeles' });

  test('Resend invite sends If-Match once, keeps focus, and says when the server sent it, in Melbourne', async ({
    page,
  }) => {
    // Before the page loads, so that its Date runs on Playwright's clock, from a day the server's answer is not.
    await page.clock.install({ time: new Date('2031-02-03T22:45:00Z') });
    await answerSetupLinks(page, { sentAt: sentAt0512 });
    const ifMatches = recordSetupLinks(page);
    await openAlex(page);
    expect(await page.evaluate(() => new Date().toString())).toMatch(/^Mon Feb 03 2031 14:\d\d:\d\d GMT-0800/);
    await expect(resendInvite(page)).toHaveAccessibleDescription(
      'Sends a new setup link. The old link stops working.',
    );
    await expect(resendLine(page)).toBeEmpty();

    await resendInvite(page).click();

    await expect(resendLine(page)).toHaveText(sentAt412pm);
    await expect(resendInvite(page)).toBeFocused();
    await expect(alertLine(page)).toBeEmpty();
    expect(await ifMatches()).toEqual([`"stamp-${alex.id}"`]);
    await expectAxeToPass(page);

    // A minute later, a second press: busy and its line emptied while it is out, then the new time.
    await page.clock.runFor(60_000);
    const heldResend = await holdNext(page, setupLinks);
    await resendInvite(page).click();
    const route = await heldResend();
    await expect(resendInvite(page)).toHaveAttribute('aria-disabled', 'true');
    await expect(resendLine(page)).toBeEmpty();
    await expect(resendInvite(page)).toHaveAccessibleName('Sending…');
    await expect(page.locator('[disabled]')).toHaveCount(0);
    await route.fulfill({ status: 204, headers: { Date: sentAt0513 } });

    await expect(resendLine(page)).toHaveText(sentAt413pm);
    await expect(resendInvite(page)).toBeFocused();
    expect(await ifMatches()).toHaveLength(2);
  });
});

test('a 412 refetches the list, then says someone changed the account, with focus kept, and passes axe', async ({
  page,
}) => {
  await answerSetupLinks(page, { status: 412, problem: { code: 'precondition.failed' } });
  await openAlex(page);
  // The list as it now is, with Alex Park's new stamp, which the refetch brings.
  await serveUsers(page, [sam, chris, jo, { ...alex, concurrencyStamp: 'stamp-2' }]);
  const refetched = page.waitForResponse(
    (response) => response.request().method() === 'GET' && new URL(response.url()).pathname === '/api/admin/users',
  );

  await resendInvite(page).click();

  expect(await (await refetched).json()).toContainEqual({ ...alex, concurrencyStamp: 'stamp-2' });
  await expect(alertLine(page)).toHaveText(
    'Someone else has just changed Alex Park’s account. The page now shows the latest details.',
  );
  await expect(resendInvite(page)).toBeFocused();
  await expect(resendLine(page)).toBeEmpty();
  await expectAxeToPass(page);

  // The next press sends the new stamp.
  const ifMatches = recordSetupLinks(page);
  await answerSetupLinks(page, { sentAt: sentAt0512 });
  await resendInvite(page).click();
  await expect(resendLine(page)).toHaveText(sentAt412pm);
  await expect(alertLine(page)).toBeEmpty();
  expect(await ifMatches()).toEqual(['"stamp-2"']);
});

test('a 412 whose refetch shows Alex now Active takes Resend invite away, focus going to the Status row', async ({
  page,
}) => {
  await answerSetupLinks(page, { status: 412, problem: { code: 'precondition.failed' } });
  await openAlex(page);
  // The list as it now is: Alex Park has set up their account meanwhile.
  await serveUsers(page, [
    sam,
    chris,
    jo,
    { ...alex, status: 'Active', activatedAtUtc: '2026-10-14T05:00:00Z', concurrencyStamp: 'stamp-2' },
  ]);

  await resendInvite(page).click();

  await expect(alertLine(page)).toHaveText(
    'Someone else has just changed Alex Park’s account. The page now shows the latest details.',
  );
  await expect(resendInvite(page)).toHaveCount(0);
  // The row that shows why the button has gone, rather than the page itself (users.md component C item 2).
  await expect(statusRow(page)).toBeFocused();
  await expect(statusRow(page).getByRole('definition')).toHaveText('Active');
  await expectAxeToPass(page);
});

for (const { cut, answer, sentence } of [
  { cut: 'the network cut', answer: 'no connection', sentence: 'Not sent: no connection. Try again.' },
  { cut: 'a 500', answer: { status: 500 }, sentence: 'Not sent: something went wrong. Try again.' },
] as const) {
  test(`with ${cut}, Resend invite says so, keeps focus, sends nothing by itself, and passes axe`, async ({ page }) => {
    // Before the page loads, so that its timers run on Playwright's clock, which the test runs a minute on.
    await page.clock.install();
    await answerSetupLinks(page, answer);
    const ifMatches = recordSetupLinks(page);
    await openAlex(page);

    await resendInvite(page).click();

    await expect(alertLine(page)).toHaveText(sentence);
    await expect(resendInvite(page)).toBeFocused();
    await expect(resendInvite(page)).not.toHaveAttribute('aria-disabled');
    await expect(resendLine(page)).toBeEmpty();
    await expectAxeToPass(page);
    await page.clock.runFor(60_000);
    expect(await ifMatches()).toHaveLength(1);
  });
}
