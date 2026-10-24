import { randomUUID } from 'node:crypto';
import type { Locator, Page } from '@playwright/test';
import { addJaneWithTwoGoals, addParticipantWithGoals, janeCitizen } from './addParticipant.ts';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectFocusAt, tabTo } from './focus.ts';
import { breakfast } from './goals.ts';
import { flaggedItem } from './me.ts';
import { daysAfter } from './notes.ts';
import { signInThroughSetup } from './setupSignIn.ts';
import { expectViewA } from './signedOut.ts';
import { inviteThroughThePage } from './users.ts';

// The eight key flows of design.md §4.13, each done by keyboard alone in the published app at 1280x800, signed in for
// real, with nothing answered in the browser (R03, R23). Two tests hold them, one test.step to a flow, named as §4.13
// names it, so that the report has a result for each: a manager's three, and the five of a worker with a manager.
// Everything that is not a flow, the sign-ins and the made-up people, goals and common items that the flows need, is
// done with the clicks that the other specs use.
//
// Inside a flow step only the keyboard is used (`tabTo` moves, the keys act), but for a native date field: it is
// reached by Tab, and given its date by `fill('YYYY-MM-DD')`, whatever the browser's locale. Every stop that `tabTo`
// passes shows the focus ring, and none traps focus. A change of screen has its focus landing asserted, with the ring,
// before axe runs; axe runs at each change of screen, each result and each dialog, and not at every key.
test.use({ viewport: { width: 1280, height: 800 } });

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
const navItem = (page: Page, name: string) => nav(page).getByRole('link', { name, exact: true });
const link = (page: Page, name: string) => main(page).getByRole('link', { name, exact: true });
const button = (page: Page, name: string) => main(page).getByRole('button', { name, exact: true });
const checkbox = (page: Page, name: string) => main(page).getByRole('checkbox', { name, exact: true });
// The back link, in the shell's bar between <header> and <main>, labelled by the screen that opened the page.
const backLink = (page: Page, label: string) =>
  page.locator('#page-bar').getByRole('link', { name: label, exact: true });
// Today's success banner after Submit, before its <h1>, which takes the focus in the <h1>'s place.
const banner = (page: Page) => main(page).locator('[data-route-focus]');
// A participant's row on Today, apart from the row of one of the signed-in person's own unfinished drafts.
const todayRow = (page: Page, name: string) =>
  main(page).locator('a[data-return-key]:not([data-return-key*="/"])').filter({ hasText: name });
// The status line under a participant's row on Today, which is not in the row's link but describes it.
const todayRowLine = (page: Page, name: string) =>
  main(page)
    .getByRole('listitem')
    .filter({ has: page.locator('a[data-return-key]', { hasText: name }) })
    .locator('p');
const flaggedRow = (page: Page, name: string) => main(page).locator('a[data-return-key]').filter({ hasText: name });
const dialog = (page: Page) => page.getByRole('alertdialog');
const dialogTitle = (page: Page) => dialog(page).getByRole('heading', { level: 2 });
const dialogButton = (page: Page, name: string) => dialog(page).getByRole('button', { name, exact: true });

// The note form: the sections, Flag for manager, Submit note, and More actions with Discard draft in its panel.
const saveStatus = (page: Page) => page.locator('#save-status');
const guidedNotes = (page: Page) => main(page).getByRole('textbox', { name: '3. Guided notes' });
const flagBox = (page: Page) => checkbox(page, 'Flag for manager');
const reasonBox = (page: Page) => main(page).getByRole('textbox', { name: 'Reason', exact: true });
const submitNote = (page: Page) => button(page, 'Submit note');
const moreActions = (page: Page) => button(page, 'More actions');
const discardDraft = (page: Page) => button(page, 'Discard draft');
// Either name, because a busy button renames itself while its request is out.
const saveChanges = (page: Page) => main(page).getByRole('button', { name: /^(Save changes|Saving changes…)$/ });
const savedAnyTime = /^Saved (\d{1,2}:\d{2}\s[ap]m|midday|midnight)$/;

// The Report and Export record screens.
const reportStatus = (page: Page) => page.locator('#report-date-status');
const downloadStatus = (page: Page) =>
  main(page).locator('p[role="status"]').filter({ hasText: 'is ready. Look in your downloads.' });

// A made-up Every note item, group and group item for the note form to offer (A35).
const everyNote = 'Every note';
const everyNoteItem = 'Medication prompted';
const outing = 'Community outing';
const outingItem = 'Travelled by bus or train';

// A second participant, with two goals of their own, for the worker's first note to go to by mistake.
const ravi = { givenName: 'Ravi', familyName: 'de Silva', name: 'Ravi de Silva' };
const raviGoals = ['Cooks a simple lunch', 'Walks to the shops'];

/**
 * Expects focus on `target`, as a keyboard user sees it after a change of screen or a press: the element has it, and
 * the ring shows on it, named as `focus.ts` names a stop, by the text it shows.
 */
async function expectFocusOn(page: Page, target: Locator) {
  await expect(target).toBeFocused();
  await expectFocusAt(page, (await target.innerText()).replace(/\s+/g, ' ').trim());
}

/**
 * Runs axe at both widths with focus where it is, and expects focus to be on the same element afterwards, as a harness
 * that hides a focused control at a phone's width, or moves focus when the window is resized, would take it away.
 */
async function expectAxeKeepingFocus(page: Page) {
  const focused = await page.evaluateHandle(() => document.activeElement);
  await expectAxeToPass(page);
  expect(
    await page.evaluate((element) => element === document.activeElement, focused),
    'focus is on the same element after axe',
  ).toBe(true);
  await focused.dispose();
}

/** Expects the note form to say that the app has saved what was typed, whenever it did. */
async function expectSaved(page: Page) {
  await expect(saveStatus(page)).toHaveText(savedAnyTime, { timeout: 15_000 });
}

/** Types `text` into Guided notes, which Tab reaches, and waits for the app's save of it. */
async function typeGuidedNotes(page: Page, text: string) {
  await tabTo(page, guidedNotes(page));
  await page.keyboard.type(text);
  await expectSaved(page);
}

/** Ticks `target` with Space, which Tab reaches, expecting it to be ticked and to keep the focus. */
async function tick(page: Page, target: Locator) {
  await tabTo(page, target);
  await page.keyboard.press('Space');
  await expect(target).toBeChecked();
  await expect(target).toBeFocused();
}

/**
 * Presses Submit note and then the confirmation's button, which names `name`, by keyboard: the confirmation's title
 * has the focus, and axe passes with it open, before Enter on the button that confirms.
 */
async function submitByKeyboard(page: Page, name: string) {
  await tabTo(page, submitNote(page));
  await page.keyboard.press('Enter');
  await expectFocusOn(page, dialogTitle(page));
  await expect(dialog(page)).toHaveAccessibleName(new RegExp(name));
  await expectAxeKeepingFocus(page);
  await tabTo(page, dialogButton(page, `Submit note for ${name}`));
  await page.keyboard.press('Enter');
}

/**
 * Adds an Every note item, then a group and one item in it, through the Common items page, with clicks, as a manager
 * does, and leaves the page there.
 */
async function addCommonItems(page: Page) {
  await navItem(page, 'Manage').click();
  await link(page, 'Common items').click();
  await expect(heading(page)).toHaveText('Common items');
  const section = (group: string) => main(page).getByRole('region', { name: group, exact: true });
  const addItem = async (group: string, text: string) => {
    await section(group).getByRole('textbox', { name: `New item in ${group}`, exact: true }).fill(text);
    await section(group).getByRole('button', { name: 'Add item', exact: true }).click();
    await expect(section(group).locator('ol > li > div > p')).toHaveText([text]);
  };

  await addItem(everyNote, everyNoteItem);
  await main(page).getByRole('textbox', { name: 'New group', exact: true }).fill(outing);
  await button(page, 'Add group').click();
  await expect(section(outing)).toBeVisible();
  await addItem(outing, outingItem);
}

test('a manager, keyboard only', async ({ page, invite, authenticator }) => {
  test.setTimeout(6 * 60_000);
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  // Yesterday is the day before the server's today, which the nav's Report link holds; the device's clock is never used
  // (A33).
  const reportAddress = await navItem(page, 'Report').getAttribute('href');
  const today = (reportAddress ?? '').replace('/reports/daily/', '');
  expect(today).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  const yesterday = daysAfter(today, -1);

  await test.step('Forgotten note', async () => {
    // Manage, Participants, Jane and Write past-day note, each opened with Enter on its link.
    await tabTo(page, navItem(page, 'Manage'), 'Shift+Tab');
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, link(page, 'Participants'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, link(page, janeCitizen.name));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, link(page, 'Write past-day note'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expectAxeKeepingFocus(page);

    // The date, filled in whatever the browser's locale, then Continue.
    const noteDate = main(page).getByLabel('Note date', { exact: true });
    await tabTo(page, noteDate);
    await noteDate.fill(yesterday);
    await tabTo(page, button(page, 'Continue'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expect(heading(page)).toContainText(janeCitizen.name);
    await expectAxeKeepingFocus(page);

    await tick(page, checkbox(page, breakfast.text));
    await typeGuidedNotes(page, 'Jane made toast and walked to the shops.');
    await submitByKeyboard(page, janeCitizen.name);

    // Submitted: Today, in the form's place, with its banner focused.
    await expect(page).toHaveURL('/');
    await expectFocusOn(page, banner(page));
    await expectAxeKeepingFocus(page);
  });

  await test.step('Daily report', async () => {
    await tabTo(page, navItem(page, 'Report'), 'Shift+Tab');
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(`/reports/daily/${today}`);
    await expectFocusOn(page, heading(page));
    // The past-day note is dated yesterday, so today has nothing to download.
    await expect(reportStatus(page)).toContainText('No submitted notes');
    await expect(button(page, 'Download Word')).toHaveAttribute('aria-disabled', 'true');
    await expectAxeKeepingFocus(page);

    // Previous day: focus stays on it as the screen steps, and yesterday holds the note.
    const previousDay = link(page, 'Previous day');
    await tabTo(page, previousDay);
    const checked = page.waitForResponse(
      (response) => new URL(response.url()).pathname === `/api/reports/daily/${yesterday}/has-notes`,
    );
    await page.keyboard.press('Enter');
    expect(await (await checked).json()).toEqual({ date: yesterday, hasNotes: true });
    await expect(page).toHaveURL(`/reports/daily/${yesterday}`);
    await expectFocusOn(page, previousDay);
    await expect(button(page, 'Download Word')).not.toHaveAttribute('aria-disabled');
    await expectAxeKeepingFocus(page);

    // Both files, by Enter on their buttons; focus stays on the button, and the line under the date says it is ready.
    for (const [label, extension] of [['Download Word', 'docx'], ['Download PDF', 'pdf']] as const) {
      await tabTo(page, button(page, label));
      const download = page.waitForEvent('download');
      await page.keyboard.press('Enter');
      expect((await download).suggestedFilename()).toMatch(new RegExp(`\\.${extension}$`));
      await expect(downloadStatus(page)).toContainText(`.${extension} is ready.`);
      await expectFocusOn(page, button(page, label));
      await expectAxeKeepingFocus(page);
    }
  });

  await test.step('Access request', async () => {
    await tabTo(page, navItem(page, 'Manage'), 'Shift+Tab');
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, link(page, 'Participants'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, link(page, janeCitizen.name));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, link(page, 'Export record'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expectAxeKeepingFocus(page);

    // Both dates by Tab, as they are: From starts at the first note, which is yesterday's, and To at today.
    const from = main(page).getByLabel('From date', { exact: true });
    const to = main(page).getByLabel('To date', { exact: true });
    await tabTo(page, from);
    await expect(from).toHaveValue(yesterday);
    await tabTo(page, to);
    await expect(to).toHaveValue(today);

    // The format by the arrow keys: Tab reaches the first radio, PDF, and ArrowDown chooses Word.
    await tabTo(page, main(page).getByRole('radio', { name: 'PDF', exact: true }));
    await page.keyboard.press('ArrowDown');
    const word = main(page).getByRole('radio', { name: 'Word', exact: true });
    await expect(word).toBeChecked();
    await expect(word).toBeFocused();

    await tabTo(page, button(page, 'Export'));
    const download = page.waitForEvent('download');
    await page.keyboard.press('Enter');
    expect((await download).suggestedFilename()).toMatch(/\.docx$/);
    await expect(page.locator('#export-status')).toContainText('.docx is ready. Look in your downloads.');
    await expectFocusOn(page, button(page, 'Export'));
    await expectAxeKeepingFocus(page);
  });
});

test('a worker and a manager, keyboard only', async ({
  page,
  invite,
  authenticator,
  resetSignIn,
  secondDevice,
  thirdDevice,
}) => {
  test.setTimeout(6 * 60_000);
  const phone = secondDevice.page;
  const newPhone = thirdDevice.page;

  // The manager's preparation, by clicks: Jane and Ravi with two goals each, one Every note item and one group with an
  // item, and a worker, invited through Users, who sets up on a phone of their own from the link that the operator's
  // command gives, which stands for the emailed one.
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  await navItem(page, 'Manage').click();
  await link(page, 'Participants').click();
  await link(page, 'Add participant').click();
  await addParticipantWithGoals(page, ravi, raviGoals);
  await addCommonItems(page);
  await navItem(page, 'Manage').click();
  await link(page, 'Users').click();
  await expect(heading(page)).toHaveText('Users');
  const riley = { displayName: 'Riley Sample', email: `riley.sample.${randomUUID()}@example.org` };
  await inviteThroughThePage(page, riley, 'Worker');
  await signInThroughSetup(
    phone,
    { ...riley, setupLink: await resetSignIn(riley.email) },
    secondDevice.authenticator,
  );

  await test.step('Wrong participant', async () => {
    // The worker opens Ravi's note by mistake and types in it, which saves a draft.
    await tabTo(phone, todayRow(phone, ravi.name));
    await phone.keyboard.press('Enter');
    await expectFocusOn(phone, heading(phone));
    await expect(heading(phone)).toContainText(ravi.name);
    await expectAxeKeepingFocus(phone);
    await typeGuidedNotes(phone, 'Meant to write this for someone else.');

    // More actions, then Discard draft in its panel: the dialog's title has the focus, and its button names Ravi.
    await tabTo(phone, moreActions(phone));
    await phone.keyboard.press('Enter');
    await expect(moreActions(phone)).toHaveAttribute('aria-expanded', 'true');
    await tabTo(phone, discardDraft(phone));
    await phone.keyboard.press('Enter');
    await expectFocusOn(phone, dialogTitle(phone));
    await expectAxeKeepingFocus(phone);
    await tabTo(phone, dialogButton(phone, `Discard draft for ${ravi.name}`));
    await phone.keyboard.press('Enter');

    // Today, in the form's place, with its <h1> focused and no banner, and no draft of Ravi's left.
    await expect(phone).toHaveURL('/');
    await expectFocusOn(phone, heading(phone));
    await expect(banner(phone)).toHaveCount(0);
    await expect(todayRow(phone, ravi.name)).toHaveCount(1);
    await expectAxeKeepingFocus(phone);
  });

  await test.step("Write today's note", async () => {
    // Jane's note, the right one: a goal, the Every note item, the group and its item, and the guided notes.
    await tabTo(phone, todayRow(phone, janeCitizen.name));
    await phone.keyboard.press('Enter');
    await expectFocusOn(phone, heading(phone));
    await expect(heading(phone)).toContainText(janeCitizen.name);
    await expectAxeKeepingFocus(phone);

    await tick(phone, checkbox(phone, breakfast.text));
    await tick(phone, checkbox(phone, everyNoteItem));
    const group = checkbox(phone, outing);
    await tick(phone, group);
    await expect(group).toHaveAttribute('aria-expanded', 'true');
    await tick(phone, checkbox(phone, outingItem));
    await typeGuidedNotes(phone, 'Jane made toast, then went out with the group.');
    await submitByKeyboard(phone, janeCitizen.name);

    // Today, the banner focused, and Jane's row reading Submitted.
    await expect(phone).toHaveURL('/');
    await expectFocusOn(phone, banner(phone));
    await expect(todayRowLine(phone, janeCitizen.name)).toContainText('Submitted');
    await expectAxeKeepingFocus(phone);
  });

  await test.step('Correct a note', async () => {
    // The manager, from the Users list, to Today, whose list now shows the worker's note, and to Jane's read view.
    await tabTo(page, navItem(page, 'Today'), 'Shift+Tab');
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expect(todayRowLine(page, janeCitizen.name)).toContainText('Submitted');
    await tabTo(page, todayRow(page, janeCitizen.name));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expectAxeKeepingFocus(page);

    // Edit, a change, Save changes: the read view in the form's place, its <h1> focused, with its Edited line.
    await tabTo(page, link(page, 'Edit'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expect(heading(page)).toContainText('Editing submitted note');
    await typeGuidedNotes(page, 'Checked with the family. ');
    await tabTo(page, saveChanges(page));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expect(heading(page)).not.toContainText('Editing');
    await expect(main(page).locator('p').filter({ hasText: /^Edited/ })).toBeVisible();
    await expectAxeKeepingFocus(page);

    // Version history, with the two versions.
    await tabTo(page, link(page, 'Version history'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expect(main(page).locator('a[data-return-key]')).toHaveCount(2);
    await expectAxeKeepingFocus(page);

    // The back link of each screen goes back one entry, as browser Back does: Edit and Save changes replaced the entry
    // that Jane's row opened (routeChange.ts), so the note's back link pops to Today, whose rule gives Jane's row the
    // focus it had.
    await tabTo(page, backLink(page, 'Note'), 'Shift+Tab');
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, backLink(page, 'Today'), 'Shift+Tab');
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL('/');
    await expectFocusOn(page, todayRow(page, janeCitizen.name));
    await expectAxeKeepingFocus(page);
  });

  await test.step('Flag', async () => {
    // The worker's half: Ravi's note, with a goal, the guided notes, Flag for manager and its Reason, submitted.
    await tabTo(phone, todayRow(phone, ravi.name));
    await phone.keyboard.press('Enter');
    await expectFocusOn(phone, heading(phone));
    await expect(heading(phone)).toContainText(ravi.name);
    await tick(phone, checkbox(phone, raviGoals[0]));
    await typeGuidedNotes(phone, 'Ravi cooked lunch but was very tired.');
    await tick(phone, flagBox(phone));
    await tabTo(phone, reasonBox(phone));
    await phone.keyboard.type('Seemed unwell and tired all day.');
    await submitByKeyboard(phone, ravi.name);
    await expect(phone).toHaveURL('/');
    await expectFocusOn(phone, banner(phone));
    await expectAxeKeepingFocus(phone);

    // The manager's half: a press on Today, a change of location, asks who is signed in again, which counts the
    // notes to review, so that the menu word says one is waiting. The next stop is Flagged.
    await tabTo(page, navItem(page, 'Today'), 'Shift+Tab');
    await page.keyboard.press('Enter');
    await expect(flaggedItem(page)).toHaveAccessibleName(/^Flagged ?, 1 to review$/);
    await tabTo(page, flaggedItem(page));
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL('/flagged');
    await expectFocusOn(page, heading(page));
    await expect(heading(page)).toHaveText('Flagged notes');
    await expect(main(page).getByRole('link', { name: 'To review (1)', exact: true })).toBeVisible();
    await expectAxeKeepingFocus(page);

    // Ravi's note, with the Review panel under it; Mark reviewed, whose result is the focused record.
    await tabTo(page, flaggedRow(page, ravi.name));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expect(heading(page)).toContainText(ravi.name);
    await expectAxeKeepingFocus(page);
    await tabTo(page, main(page).getByRole('button', { name: /^(Mark reviewed|Marking reviewed…)$/ }));
    await page.keyboard.press('Enter');
    const record = main(page)
      .locator('section')
      .filter({ has: page.getByRole('heading', { level: 2, name: 'Review', exact: true }) })
      .locator('p[tabindex="-1"]');
    await expectFocusOn(page, record);
    await expect(record).toHaveText(/^Reviewed by .+ on .+$/);
    await expectAxeKeepingFocus(page);
    await expect(flaggedItem(page)).toHaveAccessibleName(/^Flagged$/);
  });

  await test.step('Lost phone', async () => {
    // The manager, once the worker says the phone is lost and who they are: Manage, Users, the worker, Reset sign-in.
    await tabTo(page, navItem(page, 'Manage'), 'Shift+Tab');
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, link(page, 'Users'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await tabTo(page, link(page, riley.displayName));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, heading(page));
    await expect(heading(page)).toHaveText(riley.displayName);
    await expectAxeKeepingFocus(page);
    await tabTo(page, button(page, 'Reset sign-in'));
    await page.keyboard.press('Enter');
    await expectFocusOn(page, dialogTitle(page));
    await expect(dialog(page)).toHaveAccessibleName(`Reset sign-in for ${riley.displayName}?`);
    await expectAxeKeepingFocus(page);
    await tabTo(page, dialogButton(page, `Reset sign-in for ${riley.displayName}`));
    await page.keyboard.press('Enter');

    // The result: the Status row, focused, now Invited. Asserted before anything else is done to the worker's account.
    const statusRow = main(page).locator('dl > div').filter({ has: page.locator('dt', { hasText: /^Status$/ }) });
    await expectFocusOn(page, statusRow);
    await expect(statusRow.getByRole('definition')).toHaveText('Invited');
    await expectAxeKeepingFocus(page);

    // The worker opens the link that the reset emailed, which a test cannot read. resetSignIn is the operator's own
    // command, so it resets once more and gives a newer link, which stands for the emailed one, as user-actions.spec.ts
    // accepts. The worker sets up again on a new phone, as the old one's passkey has been removed with the sign-in.
    await newPhone.goto(await resetSignIn(riley.email));
    await expect(heading(newPhone)).toBeVisible();
    await expectAxeToPass(newPhone);
    await tabTo(newPhone, button(newPhone, 'Use a passkey (recommended)'));
    await newPhone.keyboard.press('Enter');
    await expect(heading(newPhone)).toContainText('Today');
    await expectFocusOn(newPhone, heading(newPhone));
    expect(await thirdDevice.authenticator.credentials()).toHaveLength(1);
    await expectAxeKeepingFocus(newPhone);

    // The old phone, reloaded, is signed out.
    await phone.reload();
    await expectViewA(phone);
  });
});
