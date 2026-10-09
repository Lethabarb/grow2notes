import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from './fixtures.ts';

// Every WCAG A and AA rule axe has, up to WCAG 2.2 AA (design.md §1, A32). axe has no wcag22a tag.
const wcag22aa = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

test('the home page shows Grow2Notes in the shared look and passes the WCAG 2.2 AA checks', async ({ page }) => {
  // A phone's width, below the one breakpoint, (min-width: 40rem).
  await page.setViewportSize({ width: 375, height: 812 });
  const response = await page.goto('/');

  // The fixture's check of the console means something only if the page is under the policy.
  expect(await response?.headerValue('Content-Security-Policy')).toBeTruthy();

  await expect(page).toHaveTitle('Grow2Notes');
  const heading = page.getByRole('heading', { level: 1, name: 'Grow2Notes' });
  await expect(heading).toBeVisible();

  // PageStatus renders the page's one status region from its first render, empty until the page announces something
  // (app-shell.md component 3).
  const status = page.getByRole('status');
  await expect(status).toHaveCount(1);
  await expect(status).toBeEmpty();

  // Without tokens.css and base.css the browser's defaults would give a 32 px <h1> at every width, 16 px body text and
  // a color-scheme of normal, so these show that the stylesheet loaded under the policy.
  await expect(heading).toHaveCSS('font-size', '28px');
  await expect(page.locator('body')).toHaveCSS('font-size', '18px');
  await expect(page.locator('html')).toHaveCSS('color-scheme', 'light');

  // A laptop's width, from the breakpoint on.
  await page.setViewportSize({ width: 1280, height: 720 });
  await expect(heading).toHaveCSS('font-size', '32px');

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
