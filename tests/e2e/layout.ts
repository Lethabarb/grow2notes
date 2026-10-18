import type { Locator, Page } from '@playwright/test';
import { expect } from './fixtures.ts';

/** Where the element shows on the page. */
export async function boxOf(locator: Locator) {
  const box = await locator.boundingBox();
  if (box === null) {
    throw new Error(`${locator.toString()} is not shown.`);
  }

  return box;
}

/**
 * Text at 200%: Chromium's default text size at 32 px, where it is 16, which makes the body's 18 px 36 (S00.05.01's
 * Notes).
 */
export async function setTextTo200Percent(page: Page) {
  const devTools = await page.context().newCDPSession(page);
  await devTools.send('Page.setFontSizes', { fontSizes: { standard: 32 } });
  await expect(page.locator('body')).toHaveCSS('font-size', '36px');
}

/** Whether the page scrolls sideways. */
export const scrollsSideways = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);

/** Expects each element that the locator finds, once one shows, to be at least `width` by `height` CSS pixels. */
export async function expectAtLeast(locator: Locator, width: number, height: number) {
  await expect(locator.first()).toBeVisible();
  for (const target of await locator.all()) {
    const box = await boxOf(target);
    const name = `${(await target.textContent()) ?? ''} ${await target.evaluate((element) => element.localName)}`;
    expect(Math.round(box.width), `the width of ${name}`).toBeGreaterThanOrEqual(width);
    expect(Math.round(box.height), `the height of ${name}`).toBeGreaterThanOrEqual(height);
  }
}

/**
 * Expects each button that the locator finds, once one shows, to take its label's width, at least 44 px wide and 48 px
 * high (common-items.md *Conflicts resolved* 6): as wide as it is when sized to what it holds, with the 44 px minimum
 * alone, so that it is neither stretched across its row, as the shared button is on a phone, nor held to the shared
 * button's 8rem minimum on a laptop.
 */
export async function expectLabelWidth(buttons: Locator) {
  await expectAtLeast(buttons, 44, 48);
  for (const button of await buttons.all()) {
    const [laidOut, sizedToLabel] = await button.evaluate((element: HTMLElement) => {
      const width = () => element.getBoundingClientRect().width;
      const asLaidOut = width();
      element.style.inlineSize = 'max-content';
      element.style.minInlineSize = 'var(--target-min)';
      const asLabel = width();
      element.style.removeProperty('inline-size');
      element.style.removeProperty('min-inline-size');
      return [asLaidOut, asLabel];
    });
    expect(Math.round(laidOut), `the width of ${(await button.textContent()) ?? ''}`).toBe(Math.round(sizedToLabel));
  }
}

/** The colour that a system colour, such as `Highlight`, takes under the forced colours that the page emulates. */
export function systemColour(page: Page, keyword: string): Promise<string> {
  return page.evaluate((colour) => {
    const probe = document.createElement('span');
    probe.style.color = colour;
    document.body.append(probe);
    const { color } = getComputedStyle(probe);
    probe.remove();
    return color;
  }, keyword);
}
