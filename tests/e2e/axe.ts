import { AxeBuilder } from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';

// Every WCAG A and AA rule axe has, up to WCAG 2.2 AA (design.md §1, A32). axe has no wcag22a tag.
const wcag22aa = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

/**
 * Expects axe's WCAG 2.2 AA rules to pass on the page as it is, at a phone's width, below the one breakpoint,
 * `(min-width: 40rem)`, and then at a laptop's, at which it leaves the page.
 */
export async function expectAxeToPass(page: Page): Promise<void> {
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
