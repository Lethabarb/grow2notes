import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

// Every WCAG A and AA rule axe has, up to WCAG 2.2 AA (design.md §1, A32). axe has no wcag22a tag.
const wcag22aa = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

test('the home page shows Grow2Notes and passes the WCAG 2.2 AA checks', async ({ page }) => {
  await page.goto('/');

  await expect(page).toHaveTitle('Grow2Notes');
  await expect(page.getByRole('heading', { level: 1, name: 'Grow2Notes' })).toBeVisible();

  const { violations } = await new AxeBuilder({ page }).withTags(wcag22aa).analyze();
  // One entry per rule, naming each element that fails it, so a failure reads as a list of problems rather than a dump
  // of axe's result objects.
  const problems = violations.map(
    ({ id, impact, help, helpUrl, nodes }) =>
      `${id} (${impact ?? 'no impact given'}): ${help}. ${helpUrl}\n` +
      nodes.map(({ target }) => `    at ${target.join(' ')}`).join('\n'),
  );
  expect(problems).toEqual([]);
});

// A nested path proves the published app's fallback serves index.html and that its asset URLs do not depend on the
// page's path.
test('a client-side route gets the same page', async ({ page }) => {
  await page.goto('/manage/anything');

  await expect(page).toHaveTitle('Grow2Notes');
  await expect(page.getByRole('heading', { level: 1, name: 'Grow2Notes' })).toBeVisible();
});
