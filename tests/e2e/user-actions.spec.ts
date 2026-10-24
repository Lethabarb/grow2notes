import { randomUUID } from 'node:crypto';
import type { Page, Response } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectFoundationsToPass } from './foundations.ts';
import { expectNothingKept } from './kept.ts';
import { failMe, manager, serveMe } from './me.ts';
import { holdNext, recordRequests } from './requests.ts';
import { expectViewA, signInInPlaceAs } from './signedOut.ts';
import {
  alex,
  answerChanges,
  chris,
  deactivations,
  inviteThroughThePage,
  jo,
  kim,
  people,
  reactivations,
  sam,
  serveUsers,
  setupLinks,
  signInAndOpenUsers,
  withChanged,
  type AdminUser,
  type ChangeAnswer,
} from './users.ts';

// Reset sign-in, Deactivate and Reactivate on a user's page in the published app (S00.06.04, S00.06.05), against
// users.md's AC3, AC21 to AC33 and AC39, with the parts of the dialog shell that only a browser shows (T04's Notes).
// Two tests sign in for real, through setup with a passkey on the virtual authenticator, so that the actions meet the
// app's own requests; the others answer /api/auth/me, /api/admin/users and the changes in the browser (me.ts,
// users.ts), so that none reaches the app.

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
// A button of the page by its whole name, apart from the dialog's, whose names are longer.
const pageButton = (page: Page, name: string) => main(page).getByRole('button', { name, exact: true });
// The action error region above the actions, a <div>, apart from a dialog's alert line, a <p> (users.md item 5).
const pageAlert = (page: Page) => main(page).locator('div[role="alert"]');
// The summary list's Status row, the <div> round its term and its fact (users.md component C items 2 and 4).
const statusRow = (page: Page) =>
  main(page).locator('dl > div').filter({ has: page.locator('dt', { hasText: /^Status$/ }) });
const dialog = (page: Page) => page.getByRole('alertdialog');
const dialogTitle = (page: Page) => dialog(page).getByRole('heading', { level: 2 });
const dialogButtons = (page: Page) => dialog(page).getByRole('button');
const dialogButton = (page: Page, name: string) => dialog(page).getByRole('button', { name, exact: true });
const dialogAlert = (page: Page) => dialog(page).getByRole('alert');
const dialogStatus = (page: Page) => dialog(page).getByRole('status');

// The confirm buttons' backgrounds, tokens.css's --colour-error for the warning style and --colour-action for the
// standard one.
const red = 'rgb(164, 0, 15)';
const blue = 'rgb(0, 85, 139)';

// The words of users.md's *Dialogs* and *States* › User detail, with the typographic apostrophe of src/copy.
const resetJo = 'Reset sign-in for Jo Smith';
const resetJoBody =
  'Only do this after you have checked, by phone or in person, that Jo Smith is the one asking. Their current way ' +
  'of signing in will be removed, and they will be signed out everywhere. A new setup link will be emailed to ' +
  'jo.smith@example.org.';
const deactivateJo = 'Deactivate Jo Smith';
const deactivateJoBody = 'Jo Smith will be signed out everywhere now and can’t sign in. Their notes stay.';
const someoneChangedJo = 'Someone else has just changed Jo Smith’s account. Go back to see the latest details.';
const signedOutHeading = 'You’ve been signed out';
const lastManagerNotice =
  'You are the only active manager, so you cannot reset your own sign-in, deactivate your account or make yourself ' +
  'a worker. To do any of these, first make someone else a manager.';

// The server's time in the answer to a change, which only Resend invite shows.
const sentAt = 'Wed, 14 Oct 2026 05:12:00 GMT';
const done: ChangeAnswer = { sentAt };

// Jo Smith, Deactivated on 14 October, as the list gives them after a deactivation.
const joDeactivated = withChanged(people, jo, { status: 'Deactivated', deactivatedAtUtc: '2026-10-14T05:12:00Z' });

// The two dialogs, each with its confirm button for Jo Smith, its request, its busy line and its failure's start.
const dialogs = [
  { trigger: 'Reset sign-in', confirm: resetJo, changes: setupLinks, busy: 'Resetting sign-in…', notDone: 'Not reset' },
  {
    trigger: 'Deactivate',
    confirm: deactivateJo,
    changes: deactivations,
    busy: 'Deactivating…',
    notDone: 'Not deactivated',
  },
] as const;

/** `user`'s page for me.ts's manager, Sam Lee, from a list of `users`, both answered in the browser. */
async function openPage(page: Page, user: AdminUser, users: readonly AdminUser[] = people) {
  await serveMe(page, manager);
  await serveUsers(page, users);
  await page.goto(`/manage/users/${user.id}`);
  await expect(heading(page)).toHaveText(user.displayName);
}

/** Whether the response answers a change to a user whose path ends with `change`. */
const answersChange = (change: string) => (response: Response) =>
  response.request().method() === 'POST' && new URL(response.url()).pathname.endsWith(`/${change}`);

test.describe('Reset sign-in and Deactivate open their dialogs', () => {
  test('Reset sign-in opens on its title with no ring; Enter then does nothing; Go back and Escape close it', async ({
    page,
  }) => {
    await openPage(page, jo);
    const sent = recordRequests(page);
    const trigger = pageButton(page, 'Reset sign-in');
    // Not red: only the dialog's last step is (users.md component C item 6).
    await expect(trigger).not.toHaveCSS('background-color', red);

    await trigger.focus();
    await page.keyboard.press('Enter');

    await expect(dialog(page)).toHaveAccessibleName('Reset sign-in for Jo Smith?');
    await expect(dialog(page)).toHaveAccessibleDescription(resetJoBody);
    await expect(dialogTitle(page)).toBeFocused();
    // The title is not a control, so it draws no ring (confirm-dialog.md *States*).
    await expect(dialogTitle(page)).toHaveCSS('outline-style', 'none');
    await expect(dialogButtons(page)).toHaveText([resetJo, 'Go back']);
    await expect(dialogButton(page, resetJo)).toHaveCSS('background-color', red);
    await expect(dialogAlert(page)).toBeEmpty();
    await expect(dialogStatus(page)).toBeEmpty();
    // Enter pressed again at once lands on the title, which confirms nothing (confirm-dialog.md *Behaviour* 2).
    await page.keyboard.press('Enter');
    await expect(dialogTitle(page)).toBeFocused();
    await expectAxeToPass(page);

    await dialogButton(page, 'Go back').click();
    await expect(dialog(page)).toHaveCount(0);
    await expect(trigger).toBeFocused();

    await trigger.click();
    await expect(dialogTitle(page)).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(dialog(page)).toHaveCount(0);
    await expect(trigger).toBeFocused();
    expect((await sent()).filter((request) => request.startsWith('POST'))).toEqual([]);
    await expect(statusRow(page).getByRole('definition')).toHaveText('Active');
  });

  test("Deactivate opens on its title, with design.md's words, a blue confirm button, and Go back", async ({
    page,
  }) => {
    await openPage(page, jo);
    const sent = recordRequests(page);
    const trigger = pageButton(page, 'Deactivate');

    await trigger.focus();
    await page.keyboard.press('Enter');

    await expect(dialog(page)).toHaveAccessibleName('Deactivate Jo Smith?');
    await expect(dialog(page)).toHaveAccessibleDescription(deactivateJoBody);
    await expect(dialogTitle(page)).toBeFocused();
    await expect(dialogTitle(page)).toHaveCSS('outline-style', 'none');
    await expect(dialogButtons(page)).toHaveText([deactivateJo, 'Go back']);
    await expect(dialogButton(page, deactivateJo)).toHaveCSS('background-color', blue);
    await page.keyboard.press('Enter');
    await expect(dialogTitle(page)).toBeFocused();
    await expectAxeToPass(page);

    await page.keyboard.press('Escape');
    await expect(dialog(page)).toHaveCount(0);
    await expect(trigger).toBeFocused();
    expect((await sent()).filter((request) => request.startsWith('POST'))).toEqual([]);
  });

  test("Deactivate has an Invited person's own words, of the setup link that will stop working", async ({ page }) => {
    await openPage(page, alex);

    await pageButton(page, 'Deactivate').click();

    await expect(dialog(page)).toHaveAccessibleName('Deactivate Alex Park?');
    await expect(dialog(page)).toHaveAccessibleDescription(
      'Alex Park has not set up their account yet. Their setup link will stop working.',
    );
    await expect(dialogButtons(page)).toHaveText(['Deactivate Alex Park', 'Go back']);
    await expectAxeToPass(page);
  });
});

test.describe('confirming', () => {
  test('Reset sign-in sends one POST with If-Match; on its 204 the Status row reads Invited, focused', async ({
    page,
  }) => {
    await openPage(page, jo);
    await answerChanges(page, setupLinks, done);
    await serveUsers(page, withChanged(people, jo, { status: 'Invited' }));
    const sent = recordRequests(page);
    await pageButton(page, 'Reset sign-in').click();

    await dialogButton(page, resetJo).click();

    await expect(dialog(page)).toHaveCount(0);
    await expect(statusRow(page)).toBeFocused();
    await expect(statusRow(page).getByRole('definition')).toHaveText('Invited');
    // Resend invite in its place, and no banner (users.md *States* › User detail).
    await expect(pageButton(page, 'Reset sign-in')).toHaveCount(0);
    await expect(pageButton(page, 'Resend invite')).toBeVisible();
    await expect(pageAlert(page)).toBeEmpty();
    await expect(main(page).locator('button + [role="status"]')).toBeEmpty();
    expect(await sent()).toEqual([
      `POST /api/admin/users/${jo.id}/setup-link "stamp-${jo.id}"`,
      'GET /api/admin/users',
    ]);
    await expectAxeToPass(page);
  });

  test('Deactivate sends one POST with If-Match; on its 204 the Status row reads Deactivated, focused', async ({
    page,
  }) => {
    await openPage(page, jo);
    await answerChanges(page, deactivations, done);
    await serveUsers(page, joDeactivated);
    const sent = recordRequests(page);
    await pageButton(page, 'Deactivate').click();

    await dialogButton(page, deactivateJo).click();

    await expect(dialog(page)).toHaveCount(0);
    await expect(statusRow(page)).toBeFocused();
    await expect(statusRow(page).getByRole('definition')).toHaveText('Deactivated');
    await expect(main(page).getByRole('button')).toHaveCount(1);
    await expect(pageButton(page, 'Reactivate')).toBeVisible();
    await expect(pageAlert(page)).toBeEmpty();
    expect(await sent()).toEqual([
      `POST /api/admin/users/${jo.id}/deactivate "stamp-${jo.id}"`,
      'GET /api/admin/users',
    ]);
    await expectAxeToPass(page);
  });

  for (const { trigger, confirm, changes, busy } of dialogs) {
    test(`${trigger}'s dialog, busy, ignores presses and Escape, keeps its labels, and says "${busy}" at 1 s`, async ({
      page,
    }) => {
      // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
      await page.clock.install();
      await openPage(page, jo);
      const sent = recordRequests(page);
      await pageButton(page, trigger).click();
      const heldChange = await holdNext(page, changes);

      // Paused a second ahead of the page's time, so that it moves only as the test runs it on from the press.
      await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
      await dialogButton(page, confirm).click();
      const route = await heldChange();
      await page.clock.runFor(999);

      for (const name of [confirm, 'Go back']) {
        await expect(dialogButton(page, name)).toHaveAttribute('aria-disabled', 'true');
      }
      await expect(dialogStatus(page)).toBeEmpty();
      // Pressed as Playwright presses a button it takes for disabled, and Escape, which the dialog stops while busy.
      await dialogButton(page, confirm).click({ force: true });
      await dialogButton(page, 'Go back').click({ force: true });
      await page.keyboard.press('Escape');
      await expect(dialog(page)).toBeVisible();
      await expect(dialogButtons(page)).toHaveText([confirm, 'Go back']);

      await page.clock.runFor(1);
      await expect(dialogStatus(page)).toHaveText(busy);
      await expect(dialogButtons(page)).toHaveText([confirm, 'Go back']);
      await expect(page.locator('[disabled]')).toHaveCount(0);
      // On the real clock, which axe's own timers need, while the request is still out.
      await page.clock.resume();
      await expectAxeToPass(page);

      await route.abort('internetdisconnected');
      await expect(dialogAlert(page)).not.toBeEmpty();
      expect((await sent()).filter((request) => request.startsWith('POST'))).toHaveLength(1);
    });
  }
});

test.describe('when a dialog cannot do what it asked', () => {
  for (const { trigger, confirm, changes, notDone } of dialogs) {
    test(`${trigger}'s 412 takes its confirm button away, says so, focuses Go back; the page shows Jo anew`, async ({
      page,
    }) => {
      await openPage(page, jo);
      await answerChanges(page, changes, { status: 412, problem: { code: 'precondition.failed' } });
      await pageButton(page, trigger).click();
      // The list as it now is: someone else has changed Jo's name.
      await serveUsers(page, withChanged(people, jo, { displayName: 'Jo Smithers' }));

      await dialogButton(page, confirm).click();

      await expect(dialogAlert(page)).toHaveText(someoneChangedJo);
      await expect(dialogButtons(page)).toHaveText(['Go back']);
      await expect(dialogButton(page, 'Go back')).toBeFocused();
      await expectAxeToPass(page);

      await dialogButton(page, 'Go back').click();

      await expect(dialog(page)).toHaveCount(0);
      await expect(heading(page)).toHaveText('Jo Smithers');
      await expect(pageButton(page, trigger)).toBeFocused();
      await expect(pageAlert(page)).toBeEmpty();
    });

    for (const { cut, answer, cause } of [
      { cut: 'the network cut', answer: 'no connection', cause: 'no connection' },
      { cut: 'a 500', answer: { status: 500 }, cause: 'something went wrong' },
    ] as const) {
      test(`with ${cut}, ${trigger}'s dialog says so, focus on its confirm button, sending nothing again`, async ({
        page,
      }) => {
        // Before the page loads, so that its timers run on Playwright's clock, which the test runs a minute on.
        await page.clock.install();
        await openPage(page, jo);
        await answerChanges(page, changes, answer);
        const sent = recordRequests(page);
        await pageButton(page, trigger).click();

        await dialogButton(page, confirm).click();

        await expect(dialogAlert(page)).toHaveText(`${notDone}: ${cause}. Try again.`);
        await expect(dialogButton(page, confirm)).toBeFocused();
        for (const name of [confirm, 'Go back']) {
          await expect(dialogButton(page, name)).not.toHaveAttribute('aria-disabled');
        }
        await expectAxeToPass(page);
        await page.clock.runFor(60_000);
        expect((await sent()).filter((request) => request.startsWith('POST'))).toHaveLength(1);
      });
    }
  }

  test('a 409 user.not_active in the Reset sign-in dialog says "Not reset: something went wrong. Try again."', async ({
    page,
  }) => {
    await openPage(page, jo);
    await answerChanges(page, setupLinks, { status: 409, problem: { code: 'user.not_active' } });
    await pageButton(page, 'Reset sign-in').click();

    await dialogButton(page, resetJo).click();

    await expect(dialogAlert(page)).toHaveText('Not reset: something went wrong. Try again.');
    await expect(dialogButtons(page)).toHaveText([resetJo, 'Go back']);
    await expect(dialogButton(page, resetJo)).toBeFocused();
  });

  for (const { trigger, own, changes, words } of [
    {
      trigger: 'Reset sign-in',
      own: 'Reset my sign-in',
      changes: setupLinks,
      words:
        'You are the only active manager, so you cannot reset your own sign-in. Make someone else a manager first.',
    },
    {
      trigger: 'Deactivate',
      own: 'Deactivate my account',
      changes: deactivations,
      words:
        'You are the only active manager, so you cannot deactivate your account. Make someone else a manager first.',
    },
  ]) {
    test(`on your own page, ${trigger}'s 409 user.last_manager gives the "you" words with Go back alone`, async ({
      page,
    }) => {
      await openPage(page, sam, [...people, kim]);
      await answerChanges(page, changes, { status: 409, problem: { code: 'user.last_manager' } });
      await pageButton(page, trigger).click();
      // Kim Tran deactivated themselves at the same moment, which the list fetched again shows.
      await serveUsers(page, withChanged([...people, kim], kim, { status: 'Deactivated' }));

      await dialogButton(page, own).click();

      await expect(dialogAlert(page)).toHaveText(words);
      await expect(dialogButtons(page)).toHaveText(['Go back']);
      await expect(dialogButton(page, 'Go back')).toBeFocused();

      await dialogButton(page, 'Go back').click();

      // The button that opened it has gone with the list fetched again, so the Status row has focus.
      await expect(main(page).getByText(lastManagerNotice)).toBeVisible();
      await expect(statusRow(page)).toBeFocused();
    });
  }
});

test.describe('on your own page, with a second active manager', () => {
  for (const { trigger, title, body, own, changes } of [
    {
      trigger: 'Reset sign-in',
      title: 'Reset your own sign-in?',
      body: 'You will be signed out everywhere now. A new setup link will be emailed to sam.lee@example.org.',
      own: 'Reset my sign-in',
      changes: setupLinks,
    },
    {
      trigger: 'Deactivate',
      title: 'Deactivate your own account?',
      body: 'You will be signed out everywhere now and cannot sign in again. Your notes stay.',
      own: 'Deactivate my account',
      changes: deactivations,
    },
  ]) {
    test(`${trigger} has the "your own" words, and its 204 loads the app afresh on sign-in`, async ({ page }) => {
      await openPage(page, sam, [...people, kim]);
      await answerChanges(page, changes, done);
      const sent = recordRequests(page);
      await pageButton(page, trigger).click();

      await expect(dialog(page)).toHaveAccessibleName(title);
      await expect(dialog(page)).toHaveAccessibleDescription(body);
      await expect(dialogButtons(page)).toHaveText([own, 'Go back']);
      await expectAxeToPass(page);
      // The change ends Sam Lee's session, so the app's next answer to /api/auth/me is a 401.
      await failMe(page, 401);

      await dialogButton(page, own).click();

      await expectViewA(page);
      await expect(page).toHaveURL('/');
      // No list was fetched before the page went, as the session had ended.
      expect((await sent()).filter((request) => request === 'GET /api/admin/users')).toEqual([]);
    });
  }
});

test.describe('the session', () => {
  for (const { trigger, confirm, changes } of dialogs) {
    test(`a 401 from ${trigger}'s dialog closes it before view C; signing in again brings back the same page`, async ({
      page,
    }) => {
      await openPage(page, jo);
      await answerChanges(page, changes, { status: 401 });
      await pageButton(page, trigger).click();
      await failMe(page, 401);

      await dialogButton(page, confirm).click();

      // A sign-in form behind an open modal would be inert, and its heading could not take focus (confirm-dialog.md
      // *Behaviour* 8).
      await expect(heading(page)).toHaveText(signedOutHeading);
      await expect(heading(page)).toBeFocused();
      await expect(page.locator('dialog[open]')).toHaveCount(0);
      await expect(page).toHaveURL(`/manage/users/${jo.id}`);

      await signInInPlaceAs(page, manager);

      await expect(heading(page)).toHaveText(jo.displayName);
      await expect(pageButton(page, trigger)).toBeVisible();
      await expect(dialog(page)).toHaveCount(0);
      await expect(page).toHaveURL(`/manage/users/${jo.id}`);
    });

    test(`at 28:00 the warning closes ${trigger}'s dialog, sending nothing; after its 204, focus is back`, async ({
      page,
    }) => {
      // Before the page loads, so that its timers and Date run on Playwright's clock, which the test runs on to 28:00.
      await page.clock.install();
      await openPage(page, jo);
      await page.route('**/api/auth/ping', (route) => route.fulfill({ status: 204 }));
      await pageButton(page, trigger).click();
      await expect(dialogTitle(page)).toBeFocused();
      const sent = recordRequests(page);

      await page.clock.runFor('28:00');

      const warning = page.getByRole('dialog', { name: 'You’ll be signed out in 2 minutes' });
      const staySignedIn = warning.getByRole('button', { name: 'Stay signed in' });
      await expect(warning).toBeVisible();
      await expect(staySignedIn).toBeFocused();
      await expect(dialog(page)).toHaveCount(0);
      expect(await sent()).toEqual([]);

      await staySignedIn.click();

      await expect(warning).toBeHidden();
      // Where the closed confirmation gave focus back, and so the warning too.
      await expect(pageButton(page, trigger)).toBeFocused();
      expect(await sent()).toEqual(['POST /api/auth/ping']);
    });
  }
});

test.describe('Reactivate', () => {
  test('sends one POST with If-Match and no dialog; on its 204 the Status row reads Active, focused', async ({
    page,
  }) => {
    await openPage(page, chris);
    await answerChanges(page, reactivations, done);
    await serveUsers(page, withChanged(people, chris, { status: 'Active', deactivatedAtUtc: null }));
    const sent = recordRequests(page);
    await expect(pageButton(page, 'Reactivate')).toHaveAccessibleDescription('Lets them sign in again.');
    await expectAxeToPass(page);

    await pageButton(page, 'Reactivate').click();

    await expect(statusRow(page)).toBeFocused();
    await expect(statusRow(page).getByRole('definition')).toHaveText('Active');
    await expect(dialog(page)).toHaveCount(0);
    await expect(pageButton(page, 'Reset sign-in')).toBeVisible();
    await expect(pageButton(page, 'Deactivate')).toBeVisible();
    await expect(pageAlert(page)).toBeEmpty();
    expect(await sent()).toEqual([
      `POST /api/admin/users/${chris.id}/reactivate "stamp-${chris.id}"`,
      'GET /api/admin/users',
    ]);
    await expectAxeToPass(page);
  });

  test('for someone the list fetched again has with no way to sign in, the Status row reads Invited', async ({
    page,
  }) => {
    await openPage(page, chris);
    await answerChanges(page, reactivations, done);
    await serveUsers(
      page,
      withChanged(people, chris, { status: 'Invited', activatedAtUtc: null, deactivatedAtUtc: null }),
    );

    await pageButton(page, 'Reactivate').click();

    await expect(statusRow(page)).toBeFocused();
    await expect(statusRow(page).getByRole('definition')).toHaveText('Invited');
    await expect(pageButton(page, 'Resend invite')).toBeVisible();
    await expect(pageButton(page, 'Deactivate')).toBeVisible();
  });

  test('a 412 fetches the list, then says so; focus stays on Reactivate while Chris is still Deactivated', async ({
    page,
  }) => {
    await page.clock.install();
    await openPage(page, chris);
    await answerChanges(page, reactivations, { status: 412, problem: { code: 'precondition.failed' } });
    await serveUsers(page, withChanged(people, chris, {}));
    const sent = recordRequests(page);

    await pageButton(page, 'Reactivate').click();

    await expect(pageAlert(page)).toHaveText(
      'Someone else has just changed Chris Ng’s account. The page now shows the latest details.',
    );
    await expect(pageButton(page, 'Reactivate')).toBeFocused();
    await expectAxeToPass(page);
    await page.clock.runFor(60_000);
    expect((await sent()).filter((request) => request.startsWith('POST'))).toHaveLength(1);
  });

  test('a 412 whose list shows Chris now Active takes Reactivate away, focus going to the Status row', async ({
    page,
  }) => {
    await openPage(page, chris);
    await answerChanges(page, reactivations, { status: 412, problem: { code: 'precondition.failed' } });
    await serveUsers(page, withChanged(people, chris, { status: 'Active', deactivatedAtUtc: null }));

    await pageButton(page, 'Reactivate').click();

    await expect(pageAlert(page)).toHaveText(
      'Someone else has just changed Chris Ng’s account. The page now shows the latest details.',
    );
    await expect(pageButton(page, 'Reactivate')).toHaveCount(0);
    await expect(statusRow(page)).toBeFocused();
    await expect(statusRow(page).getByRole('definition')).toHaveText('Active');
  });

  for (const { cut, answer, sentence } of [
    { cut: 'the network cut', answer: 'no connection', sentence: 'Not reactivated: no connection. Try again.' },
    { cut: 'a 500', answer: { status: 500 }, sentence: 'Not reactivated: something went wrong. Try again.' },
  ] as const) {
    test(`with ${cut}, Reactivate says so, keeps focus, sends nothing by itself, and passes axe`, async ({ page }) => {
      await page.clock.install();
      await openPage(page, chris);
      await answerChanges(page, reactivations, answer);
      const sent = recordRequests(page);

      await pageButton(page, 'Reactivate').click();

      await expect(pageAlert(page)).toHaveText(sentence);
      await expect(pageButton(page, 'Reactivate')).toBeFocused();
      await expect(pageButton(page, 'Reactivate')).not.toHaveAttribute('aria-disabled');
      await expectAxeToPass(page);
      await page.clock.runFor(60_000);
      expect((await sent()).filter((request) => request.startsWith('POST'))).toHaveLength(1);
    });
  }
});

test('signed in for real, a worker invited through Invite user is deactivated, then reactivated: Invited', async ({
  page,
  invite,
  authenticator,
}) => {
  await signInAndOpenUsers(page, invite, authenticator);
  const worker = { displayName: 'Robin Sample', email: `robin.sample.${randomUUID()}@example.org` };
  await inviteThroughThePage(page, worker, 'Worker');
  await main(page).getByRole('link', { name: worker.displayName, exact: true }).click();
  await expect(heading(page)).toHaveText(worker.displayName);
  await expectAxeToPass(page);

  await pageButton(page, 'Deactivate').click();
  await expect(dialog(page)).toHaveAccessibleDescription(
    `${worker.displayName} has not set up their account yet. Their setup link will stop working.`,
  );
  await expectAxeToPass(page);
  const deactivated = page.waitForResponse(answersChange('deactivate'));
  await dialogButton(page, `Deactivate ${worker.displayName}`).click();

  // The app's answer, to the stamp that the list gave, which the page sent quoted.
  const deactivation = await deactivated;
  expect(deactivation.status()).toBe(204);
  expect(deactivation.request().headers()['if-match']).toMatch(/^"[^"]+"$/);
  await expect(statusRow(page)).toBeFocused();
  await expect(statusRow(page).getByRole('definition')).toHaveText('Deactivated');
  await expectNothingKept(page, worker.displayName, worker.email);
  await expectAxeToPass(page);

  const reactivated = page.waitForResponse(answersChange('reactivate'));
  await pageButton(page, 'Reactivate').click();

  // Deactivated while still Invited, so with no way to sign in, and Invited again (D75).
  expect((await reactivated).status()).toBe(204);
  await expect(statusRow(page)).toBeFocused();
  await expect(statusRow(page).getByRole('definition')).toHaveText('Invited');
  await expect(pageButton(page, 'Resend invite')).toBeVisible();
  await expect(pageButton(page, 'Deactivate')).toBeVisible();
  await expect(pageAlert(page)).toBeEmpty();
  await expectNothingKept(page, worker.displayName, worker.email);
  await expectAxeToPass(page);
});

test("signed in for real, a second manager makes the first's own page offer both; reset, their page signs out", async ({
  page,
  invite,
  authenticator,
  resetSignIn,
  secondDevice,
}) => {
  await signInAndOpenUsers(page, invite, authenticator);
  const second = { displayName: 'Jordan Sample', email: `jordan.sample.${randomUUID()}@example.org` };
  await inviteThroughThePage(page, second, 'Manager');

  // The invite's email is out of the test's reach, so the operator's command gives a new link, as for a lost invite.
  const other = secondDevice.page;
  await other.goto(await resetSignIn(second.email));
  await other.getByRole('main').getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(other.getByRole('main').getByRole('heading', { level: 1 })).toContainText('Today');
  expect(await secondDevice.authenticator.credentials()).toHaveLength(1);

  // With a second active manager, the first manager's own page offers Reset sign-in and Deactivate (AC22).
  await main(page).getByRole('link', { name: invite.displayName, exact: true }).click();
  await expect(heading(page)).toHaveText(invite.displayName);
  await expect(pageButton(page, 'Reset sign-in')).toBeVisible();
  await expect(pageButton(page, 'Deactivate')).toBeVisible();
  await expect(main(page).getByText(lastManagerNotice)).toHaveCount(0);
  await expectAxeToPass(page);

  await page.locator('#page-bar').getByRole('link', { name: 'Users' }).click();
  await main(page).getByRole('link', { name: second.displayName, exact: true }).click();
  await expect(statusRow(page).getByRole('definition')).toHaveText('Active');
  await pageButton(page, 'Reset sign-in').click();
  await expect(dialog(page)).toHaveAccessibleName(`Reset sign-in for ${second.displayName}?`);
  await expectAxeToPass(page);
  const reset = page.waitForResponse(answersChange('setup-link'));
  await dialogButton(page, `Reset sign-in for ${second.displayName}`).click();

  expect((await reset).status()).toBe(204);
  await expect(statusRow(page)).toBeFocused();
  await expect(statusRow(page).getByRole('definition')).toHaveText('Invited');
  await expect(pageButton(page, 'Resend invite')).toBeVisible();
  await expectNothingKept(page, second.displayName, second.email);
  await expectAxeToPass(page);

  // The second manager's open page, whose next request the reset's new stamp refuses.
  await other.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Manage', exact: true }).click();
  await expect(other.getByRole('main').getByRole('heading', { level: 1 })).toHaveText(signedOutHeading);
});

// An Active worker's page and its two dialogs, each in a test of its own, as the foundations change the text size and
// spacing for good.
test("an Active worker's page passes the foundations", async ({ page }) => {
  test.slow();
  await openPage(page, jo);
  await expect(pageButton(page, 'Reset sign-in')).toBeVisible();

  await expectFoundationsToPass(page);
});

for (const { trigger, confirm } of dialogs) {
  test(`the ${trigger} dialog passes the foundations`, async ({ page }) => {
    test.slow();
    await openPage(page, jo);
    await pageButton(page, trigger).click();
    await expect(dialogTitle(page)).toBeFocused();
    await expect(dialogButton(page, confirm)).toBeVisible();

    await expectFoundationsToPass(page);
  });
}
