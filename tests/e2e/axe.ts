import { AxeBuilder } from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';
import { scrollsSideways } from './layout.ts';

// Every WCAG A and AA rule axe has, up to WCAG 2.2 AA (design.md §1, A32). axe has no wcag22a tag.
const wcag22aa = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

/**
 * Expects the page as it is to reflow at the narrowest phone's width, 320 px, with no sideways scroll (SC 1.4.10),
 * and axe's WCAG 2.2 AA rules to pass on it at a phone's width, 375 px, below the one breakpoint, `(min-width: 40rem)`,
 * and then at a laptop's, 1280 px, at which it leaves the page, 800 px high, the size the key flows use.
 *
 * Every state checked by axe must also reflow at 320 px, so the reflow check is made here, once, rather than in each
 * spec. It checks the page's own width only. foundations.ts fits the real zoom sizes, which are shorter than this
 * (320x200 and 640x400), and looks inside the page as well.
 */
export async function expectAxeToPass(page: Page): Promise<void> {
  // The 320 px check is made in passing: the window and the scroll go back as the test left them, so that axe checks the
  // page the test built and not one that the reflow has scrolled, which axe's target-size rule found, under the note
  // form's sticky bar.
  const before = page.viewportSize();
  const scrolledBy = await page.evaluate(() => globalThis.scrollY);
  await page.setViewportSize({ width: 320, height: 800 });
  expect(await scrollsSideways(page), 'sideways scroll at 320 px wide (WCAG 1.4.10)').toBe(false);
  if (before !== null) {
    await page.setViewportSize(before);
  }

  await page.evaluate((top) => globalThis.scrollTo(0, top), scrolledBy);

  for (const width of [375, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    const { violations } = await new AxeBuilder({ page }).withTags(wcag22aa).analyze();
    // One entry per rule, naming each element that fails it, so a failure reads as a list of problems rather than a
    // dump of axe's result objects.
    const problems = violations.map(
      ({ id, impact, help, helpUrl, nodes }) =>
        `${id} (${impact ?? 'no impact given'}): ${help}. ${helpUrl}\n` +
        nodes.map(({ target }) => `    at ${target.join(' ')}`).join('\n'),
    );
    expect(problems, `axe at ${width} px`).toEqual([]);
  }
}
