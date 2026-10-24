import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';
import { breakfast, bus } from './goals.ts';

/** The participant whom the tests that sign in for real add, and the wording of their two goals. */
export const janeCitizen = { givenName: 'Jane', familyName: 'Citizen', name: 'Jane Citizen' };
export const twoGoalTexts = [breakfast.text, bus.text];

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
const rows = (page: Page) => main(page).locator('a[data-return-key]');

/**
 * On Add participant, fills in `name` and a made-up birth date and saves, as a manager does, then adds each of `goals`
 * on the page that follows, one after another. Ends on the new participant's page once the goal list holds them all.
 * Gives their ID.
 */
export async function addParticipantWithGoals(
  page: Page,
  name: { givenName: string; familyName: string },
  goals: readonly string[],
): Promise<string> {
  await expect(heading(page)).toHaveText('Add participant');
  await main(page).getByRole('textbox', { name: 'Given name', exact: true }).fill(name.givenName);
  await main(page).getByRole('textbox', { name: 'Family name', exact: true }).fill(name.familyName);
  await main(page).getByRole('textbox', { name: 'Day', exact: true }).fill('7');
  await main(page).getByRole('textbox', { name: 'Month', exact: true }).fill('3');
  await main(page).getByRole('textbox', { name: 'Year', exact: true }).fill('1987');
  const added = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/admin/participants',
  );
  await main(page).getByRole('region', { name: 'Details' }).getByRole('button', { name: 'Save', exact: true }).click();
  const addAnswer = await added;
  expect(addAnswer.status()).toBe(201);
  const { id } = (await addAnswer.json()) as { id: string };
  await expect(heading(page)).toHaveText(`${name.givenName} ${name.familyName}`);

  const goalsSection = main(page).getByRole('region', { name: 'Goals' });
  for (const text of goals) {
    await goalsSection.getByRole('textbox', { name: 'New goal', exact: true }).fill(text);
    const posted = page.waitForResponse(
      (response) =>
        response.request().method() === 'POST'
        && new URL(response.url()).pathname === `/api/admin/participants/${id}/goals`,
    );
    await goalsSection.getByRole('button', { name: 'Add goal', exact: true }).click();
    expect((await posted).status()).toBe(201);
  }

  await expect(goalsSection.locator('ol > li')).toHaveCount(goals.length);
  return id;
}

/**
 * From Today with no participants, adds Jane Citizen through the empty state's Add participant, as a manager does, and
 * two goals on her page, then goes back to Today through the nav. Gives her ID.
 */
export async function addJaneWithTwoGoals(page: Page): Promise<string> {
  await expect(main(page).getByText('No participants yet.', { exact: true })).toBeVisible();
  await main(page).getByRole('link', { name: 'Add participant', exact: true }).click();
  const id = await addParticipantWithGoals(page, janeCitizen, twoGoalTexts);
  await nav(page).getByRole('link', { name: 'Today', exact: true }).click();
  await expect(rows(page)).toHaveText([janeCitizen.name]);
  return id;
}
