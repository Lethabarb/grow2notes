import type { Page } from '@playwright/test';
import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectStops } from './focus.ts';
import { breakfast, bus } from './goals.ts';
import { scrollsSideways } from './layout.ts';
import { serveMe, worker } from './me.ts';
import { notesStore, type NoteGroup } from './notes.ts';
import { jane, participants } from './participants.ts';
import { serveToday } from './today.ts';

// Section 2 of the note form, Common items and the picker (S02.05.02, S02.05.03; group-picker.md), as far as the form
// goes: the groups are the made-up ones of common-items.md's sketch, answered in the browser with the note, so that no
// request reaches the app, where no made-up person has a session.

const medication = { id: '1a4c7e90-2b5d-4f81-9c36-8e0a2d4f6b17', text: 'Medication prompted' };
const meal = { id: '2b5d8fa1-3c6e-4a92-8d47-9f1b3e5a7c28', text: 'Meal prepared' };
const trains = { id: '5e80bcd4-6f91-4dc5-9a7a-2c4e6b8dafb5', text: 'Travelled by bus or train' };
const purchases = { id: '6f91cde5-7aa2-4ed6-8b8b-3d5f7c9eb0c6', text: 'Paid for a ticket at the counter' };
const showered = { id: '8bc3efa7-9cd4-4a08-9dae-5f7b9ea03c18', text: 'Showered' };

const everyNote: NoteGroup = {
  id: '0e2a4c6f-1b3d-4e5f-8a7b-9c0d1e2f3a4b',
  name: 'Every note',
  isEveryNote: true,
  items: [medication, meal],
};
const outing: NoteGroup = {
  id: '4f6a8c0e-3d5f-4b7a-9c1e-2a4c6e8f0b3d',
  name: 'Community outing',
  items: [trains, purchases],
};
const personalCare: NoteGroup = {
  id: '7a9c1e3f-6b8d-4e0a-8c2e-5b7d9f1a3c4e',
  name: 'Personal care',
  items: [showered],
};

/** "Saved 4:12 pm", or the like, at whatever time the app saved. */
const savedAnyTime = /^Saved (\d{1,2}:\d{2}\s[ap]m|midday|midnight)$/;

const main = (page: Page) => page.getByRole('main');
const saveStatus = (page: Page) => page.locator('#save-status');
const box = (page: Page) => main(page).getByRole('textbox', { name: '3. Guided notes' });
const pickBox = (page: Page, name: string) => main(page).getByRole('checkbox', { name, exact: true });
const itemBox = (page: Page, text: string) => main(page).getByRole('checkbox', { name: text, exact: true });
const submitNote = (page: Page) => main(page).getByRole('button', { name: 'Submit note', exact: true });
const confirmation = (page: Page) => page.getByRole('alertdialog');

/** Jane Citizen's note, with two goals and the made-up groups, opened as the worker. */
async function openJanesNote(page: Page) {
  const store = notesStore({
    today: worker.today,
    participants,
    goals: { [jane.id]: [breakfast, bus] },
    commonItemGroups: [everyNote, outing, personalCare],
  });
  await serveMe(page, worker);
  await serveToday(page, worker.today, participants);
  await store.serve(page, worker);
  await page.goto(`/participants/${jane.id}/notes/${worker.today}`);
  await expect(main(page).getByRole('heading', { level: 2, name: '2. Common items' })).toBeVisible();
  return store;
}

test('picking a group shows its items, unticked, and ticking one sends the picks with it', async ({ page }) => {
  const store = await openJanesNote(page);
  await expect(itemBox(page, medication.text)).not.toBeChecked();
  await expect(pickBox(page, outing.name)).not.toBeChecked();
  await expect(pickBox(page, outing.name)).toHaveAttribute('aria-expanded', 'false');
  await expect(itemBox(page, trains.text)).toHaveCount(0);

  await pickBox(page, outing.name).check();
  await expect(pickBox(page, outing.name)).toHaveAttribute('aria-expanded', 'true');
  await expect(pickBox(page, outing.name)).toBeFocused();
  await expect(itemBox(page, trains.text)).toBeVisible();
  await expect(itemBox(page, trains.text)).not.toBeChecked();
  await expect(itemBox(page, purchases.text)).not.toBeChecked();
  await expect(itemBox(page, showered.text)).toHaveCount(0);
  await itemBox(page, trains.text).check();

  await expect.poll(() => store.saves.at(-1)?.tickedCommonItemIds, { timeout: 15_000 }).toEqual([trains.id]);
  expect(store.saves.at(-1)).toMatchObject({ tickedGoalIds: [], pickedGroupIds: [outing.id] });
});

test('unpicking a group hides it with its ticks, asking nothing, and the next save lacks it', async ({ page }) => {
  const store = await openJanesNote(page);
  await pickBox(page, outing.name).check();
  await itemBox(page, trains.text).check();
  await itemBox(page, medication.text).check();
  await expect.poll(() => store.saves.at(-1)?.tickedCommonItemIds.length, { timeout: 15_000 }).toBe(2);
  const before = store.saves.length;

  await pickBox(page, outing.name).uncheck();
  await expect(itemBox(page, trains.text)).toHaveCount(0);
  await expect(pickBox(page, outing.name)).toHaveAttribute('aria-expanded', 'false');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect.poll(() => store.saves.length, { timeout: 15_000 }).toBeGreaterThan(before);
  expect(store.saves.at(-1)).toMatchObject({ pickedGroupIds: [], tickedCommonItemIds: [medication.id] });

  await pickBox(page, outing.name).check();
  await expect(itemBox(page, trains.text)).not.toBeChecked();
});

test('Submit with nothing picked passes the confirmation, which says nothing of groups', async ({ page }) => {
  const store = await openJanesNote(page);
  await itemBox(page, meal.text).check();
  await box(page).fill('Jane made toast.');
  await expect(saveStatus(page)).toHaveText(savedAnyTime);

  await submitNote(page).click();
  await expect(confirmation(page)).toBeVisible();
  await expect(confirmation(page)).not.toContainText(/common|outing|personal care|picked/i);
  await confirmation(page).getByRole('button', { name: 'Submit note for Jane Citizen', exact: true }).click();

  await expect.poll(() => store.submits.length, { timeout: 15_000 }).toBe(1);
  expect(store.submits[0].body).toMatchObject({ pickedGroupIds: [], tickedCommonItemIds: [meal.id] });
});

test('the form passes axe with a group picked', async ({ page }) => {
  await openJanesNote(page);
  await pickBox(page, outing.name).check();
  await itemBox(page, trains.text).check();

  await expectAxeToPass(page);
});

test('the form does not scroll sideways at 375 px, with both groups picked', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await openJanesNote(page);
  await pickBox(page, outing.name).check();
  await pickBox(page, personalCare.name).check();

  await expect(itemBox(page, purchases.text)).toBeVisible();
  expect(await scrollsSideways(page), 'Common items with both groups picked').toBe(false);
});

test('Tab goes through Every note, the picker, picked items, then Guided notes; Space ticks a box and stays', async ({
  page,
}) => {
  await openJanesNote(page);
  await pickBox(page, outing.name).check();
  await main(page).getByRole('link', { name: 'Past notes', exact: true }).focus();

  await expectStops(page, 'Tab', [
    breakfast.text,
    bus.text,
    medication.text,
    meal.text,
    outing.name,
    personalCare.name,
    trains.text,
    purchases.text,
    '3. Guided notes',
  ]);

  await pickBox(page, personalCare.name).focus();
  await page.keyboard.press('Space');
  await expect(pickBox(page, personalCare.name)).toBeChecked();
  await expect(pickBox(page, personalCare.name)).toBeFocused();
  await expect(pickBox(page, personalCare.name)).toHaveAttribute('aria-expanded', 'true');
  await expectStops(page, 'Tab', [trains.text, purchases.text, showered.text, '3. Guided notes']);
});
