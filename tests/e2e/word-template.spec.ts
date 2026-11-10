import { randomUUID } from 'node:crypto';
import type { Page } from '@playwright/test';
import { addJaneWithTwoGoals, janeCitizen } from './addParticipant.ts';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { breakfast, bus } from './goals.ts';
import { signInThroughSetup } from './setupSignIn.ts';
import { inviteThroughThePage } from './users.ts';

// The Word template's additions (D104), signed in for real, with nothing answered in the browser: a manager sets a
// goal to Medium or long term, marks a common item as one that asks for details, and switches on a participant's
// Medication box; a worker writes a note with Late Notice, Medication and the item's details, is stopped by the missing
// details, and submits; the read view shows all of it; and the manager downloads the day's report for Week A, which the
// file's name and the request show.
test.use({ viewport: { width: 1280, height: 800 } });

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
const navItem = (page: Page, name: string) => nav(page).getByRole('link', { name, exact: true });
const link = (page: Page, name: string) => main(page).getByRole('link', { name, exact: true });
const button = (page: Page, name: string) => main(page).getByRole('button', { name, exact: true });
const region = (page: Page, name: string) => main(page).getByRole('region', { name, exact: true });
const todayRow = (page: Page, name: string) =>
  main(page).locator('a[data-return-key]:not([data-return-key*="/"])').filter({ hasText: name });
const saveStatus = (page: Page) => page.locator('#save-status');
const savedAnyTime = /^Saved (\d{1,2}:\d{2}\s[ap]m|midday|midnight)$/;
const dialog = (page: Page) => page.getByRole('alertdialog');

// The item that asks for details, as the Word template words its Other items, and what the worker writes.
const otherItem = 'Other (staff to complete if different from above)';
const otherDetails = 'Had a haircut at the shopping centre';
const medicationText = '8 am: two tablets with breakfast.\n2 pm: one tablet.';
const narrative = 'Jane arrived late and then went out for lunch.';

const weekdays = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;

/** A day as the report's file name has it, such as `Thursday 01.10.2026`, in English whatever the device's language. */
function fileDay(isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  const name = weekdays[new Date(Date.UTC(year, month - 1, day)).getUTCDay()];
  return `${name} ${String(day).padStart(2, '0')}.${String(month).padStart(2, '0')}.${year}`;
}

/** The manager's set-up: Jane's first goal Medium or long term, her Medication box on, and the Other item asking. */
async function setUp(page: Page) {
  await navItem(page, 'Manage').click();
  await link(page, 'Participants').click();
  await link(page, janeCitizen.name).click();
  await expect(heading(page)).toHaveText(janeCitizen.name);

  // A goal's Term, chosen in its Edit form and sent with Save; the list then says so under the goal.
  const goals = region(page, 'Goals');
  await goals.getByRole('button', { name: `Edit , ${breakfast.text}`, exact: true }).click();
  await expect(goals.getByRole('group', { name: 'Term', exact: true })).toBeVisible();
  await expect(goals.getByRole('radio', { name: 'Short term', exact: true })).toBeChecked();
  await goals.getByRole('radio', { name: 'Medium or long term', exact: true }).check();
  const reworded = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' && new URL(response.url()).pathname.includes('/api/admin/goals/'),
  );
  await goals.getByRole('button', { name: 'Save', exact: true }).click();
  expect((await reworded).status()).toBe(200);
  await expect(goals.getByText('Medium or long term', { exact: true })).toBeVisible();
  await expect(goals.getByRole('group', { name: 'Term', exact: true })).toHaveCount(0);

  // The Medication box switch, saved with the Details.
  const details = region(page, 'Details');
  const medicationSwitch = details.getByRole('checkbox', { name: 'Medication box on notes', exact: true });
  await expect(medicationSwitch).not.toBeChecked();
  await expect(medicationSwitch).toHaveAccessibleDescription('Shows a Medication box on notes started from now on.');
  await medicationSwitch.check();
  const corrected = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' && new URL(response.url()).pathname.startsWith('/api/admin/participants/'),
  );
  await details.getByRole('button', { name: 'Save', exact: true }).click();
  expect((await corrected).status()).toBe(200);
  await expect(medicationSwitch).toBeChecked();

  // The item that asks for details, in Every note: added, then its Edit form's Ask for details when ticked.
  await navItem(page, 'Manage').click();
  await link(page, 'Common items').click();
  await expect(heading(page)).toHaveText('Common items');
  const everyNote = region(page, 'Every note');
  await everyNote.getByRole('textbox', { name: 'New item in Every note', exact: true }).fill(otherItem);
  await everyNote.getByRole('button', { name: 'Add item', exact: true }).click();
  await expect(everyNote.getByText(otherItem, { exact: true })).toBeVisible();
  await everyNote.getByRole('button', { name: `Edit , ${otherItem}`, exact: true }).click();
  const asks = everyNote.getByRole('checkbox', { name: 'Ask for details when ticked', exact: true });
  await expect(asks).not.toBeChecked();
  await asks.check();
  const itemSaved = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' && new URL(response.url()).pathname.includes('/api/admin/common-items/'),
  );
  await everyNote.getByRole('button', { name: 'Save', exact: true }).click();
  expect((await itemSaved).status()).toBe(200);
  await expect(everyNote.getByText('Asks for details', { exact: true })).toBeVisible();
}

test('a manager sets up the additions, a worker writes a note with them, and the report is for Week A', async ({
  page,
  invite,
  authenticator,
  resetSignIn,
  secondDevice,
  browserName,
}) => {
  test.setTimeout(6 * 60_000);
  const phone = secondDevice.page;
  await signInThroughSetup(page, invite, authenticator);
  await addJaneWithTwoGoals(page);
  const reportAddress = await navItem(page, 'Report').getAttribute('href');
  const today = (reportAddress ?? '').replace('/reports/daily/', '');
  expect(today).toMatch(/^\d{4}-\d{2}-\d{2}$/);

  await setUp(page);

  // A worker, invited through Users, who sets up on a phone of their own from the link that the operator's command
  // gives, which stands for the emailed one.
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

  await test.step('The worker writes the note', async () => {
    await todayRow(phone, janeCitizen.name).click();
    await expect(heading(phone)).toContainText(janeCitizen.name);

    // Attendance first, then the goals under their terms, Medication, the Every note item and the notes.
    await expect(main(phone).getByRole('heading', { level: 2 })).toHaveText([
      'Attendance',
      'Short term goals',
      'Medium or Long term goal',
      'Medication',
      'Every note',
      'Daily Support Notes',
    ]);
    const attendance = main(phone).getByRole('group', { name: 'Attendance', exact: true });
    await expect(attendance.getByRole('checkbox')).toHaveCount(3);
    await attendance.getByRole('checkbox', { name: 'Late Notice', exact: true }).check();
    await main(phone).getByRole('checkbox', { name: breakfast.text, exact: true }).check();
    await main(phone).getByRole('textbox', { name: 'Medication', exact: true }).fill(medicationText);
    await main(phone).getByRole('checkbox', { name: otherItem, exact: true }).check();
    const detailsBox = main(phone).getByRole('textbox', { name: `Details , ${otherItem}`, exact: true });
    await expect(detailsBox).toBeVisible();
    await main(phone).getByRole('textbox', { name: 'Daily Support Notes', exact: true }).fill(narrative);
    await expect(saveStatus(phone)).toHaveText(savedAnyTime, { timeout: 15_000 });
    await expectAxeToPass(phone);

    // Stopped: the Details box is blank, so Submit refuses, inline and in the summary, and opens no confirmation.
    await button(phone, 'Submit note').click();
    const message = `Enter the details for ${otherItem}`;
    await expect(main(phone).getByRole('link', { name: message, exact: true })).toBeVisible();
    await expect(detailsBox).toHaveAttribute('aria-invalid', 'true');
    await expect(dialog(phone)).toHaveCount(0);

    // Filled in, and submitted.
    await detailsBox.fill(otherDetails);
    await expect(detailsBox).not.toHaveAttribute('aria-invalid');
    await expect(saveStatus(phone)).toHaveText(savedAnyTime, { timeout: 15_000 });
    await button(phone, 'Submit note').click();
    await expect(dialog(phone)).toBeVisible();
    await dialog(phone).getByRole('button', { name: `Submit note for ${janeCitizen.name}`, exact: true }).click();
    await expect(phone).toHaveURL('/');
  });

  await test.step('The read view shows it all', async () => {
    await todayRow(phone, janeCitizen.name).click();
    await expect(heading(phone)).toContainText(janeCitizen.name);
    await expect(main(phone).getByRole('heading', { level: 2 })).toHaveText([
      'Attendance',
      'Short term goals',
      'Medium or Long term goal',
      'Medication',
      'Every note',
      'Daily Support Notes',
    ]);
    const ticks = (name: string) => main(phone).getByRole('listitem').filter({ hasText: name });
    await expect(ticks('Late Notice')).toHaveText('Late Notice, ticked');
    await expect(ticks('Not In')).toHaveText('Not In, not ticked');
    await expect(ticks(breakfast.text)).toHaveText(`${breakfast.text}, ticked`);
    await expect(ticks(bus.text)).toHaveText(`${bus.text}, not ticked`);
    await expect(main(phone).getByText(`Details: ${otherDetails}`, { exact: true })).toBeVisible();
    expect(await main(phone).getByText('8 am: two tablets with breakfast.', { exact: false }).textContent()).toBe(
      medicationText,
    );
    await expectAxeToPass(phone);
  });

  await test.step('The manager downloads the report for Week A', async () => {
    await navItem(page, 'Report').click();
    await expect(page).toHaveURL(`/reports/daily/${today}`);
    await expect(heading(page)).toHaveText('Daily report');
    await expect(button(page, 'Download Word')).not.toHaveAttribute('aria-disabled');
    const week = main(page).getByRole('group', { name: 'Week', exact: true });
    await expect(week.getByRole('radio', { name: 'None', exact: true })).toBeChecked();
    await week.getByRole('radio', { name: 'Week A', exact: true }).check();
    await expectAxeToPass(page);

    const requested = page.waitForRequest(
      (request) => new URL(request.url()).pathname === `/api/reports/daily/${today}/export`,
    );
    const download = page.waitForEvent('download');
    await button(page, 'Download Word').click();

    expect(new URL((await requested).url()).search).toBe('?format=docx&week=A');
    // WebKit swaps the spaces in a suggested name for underscores.
    const suggested = (await download).suggestedFilename();
    expect(browserName === 'webkit' ? suggested.replace(/_/g, ' ') : suggested).toBe(`${fileDay(today)}.docx`);
    await expect(
      main(page).getByRole('status').filter({ hasText: 'is ready. Look in your downloads.' }),
    ).toContainText(`${fileDay(today)}.docx is ready.`);
  });
});
