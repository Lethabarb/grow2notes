import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';

/** Where focus is: outside the page, on the page itself, or on the element that has it, by its text. */
export const whereFocusIs = (page: Page) =>
  page.evaluate(() => {
    const element = document.activeElement;
    if (!document.hasFocus()) {
      return 'outside the page';
    }

    return element === null || element === document.body ? 'the page itself' : (element.textContent ?? '');
  });

/** The focus ring as it shows (foundations.md): whether the element matches `:focus-visible`, and its outline. */
export type Ring = { focusVisible: boolean; style: string; width: string; colour: string; inViewport: boolean };

/** base.css's ring, 3 px of --colour-focus, on an element on screen. */
export const ring: Ring = {
  focusVisible: true,
  style: 'solid',
  width: '3px',
  colour: 'rgb(11, 12, 12)',
  inViewport: true,
};

/**
 * Where focus is, as `whereFocusIs` says, but by the text that shows, so that a button's busy label, hidden until it is
 * busy, is left out, and naming a field, a text box and a tick box included, by its label; with the ring that shows on
 * the element or, for a row's link, on the `::after` that covers the row (LinkRow.module.css); no ring outside the
 * page or on it.
 */
function focusStop(page: Page): Promise<{ name: string; ring?: Ring }> {
  return page.evaluate(() => {
    const element = document.activeElement;
    if (!document.hasFocus()) {
      return { name: 'outside the page' };
    }

    if (!(element instanceof HTMLElement) || element === document.body) {
      return { name: 'the page itself' };
    }

    const text =
      element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement
        ? element.labels?.[0]?.innerText
        : element.innerText;
    const own = getComputedStyle(element);
    const drawn = own.outlineStyle === 'none' ? getComputedStyle(element, '::after') : own;
    const box = element.getBoundingClientRect();
    return {
      name: (text ?? '').replace(/\s+/g, ' ').trim(),
      ring: {
        focusVisible: element.matches(':focus-visible'),
        style: drawn.outlineStyle,
        width: drawn.outlineWidth,
        colour: drawn.outlineColor,
        inViewport: box.bottom > 0 && box.top < innerHeight && box.right > 0 && box.left < innerWidth,
      },
    };
  });
}

/** Expects focus on `stop`, as `focusStop` reads it, with `expected`'s ring showing, `step` naming how it got there. */
async function expectReached(page: Page, stop: string, expected: Ring, step: string) {
  const reached = await focusStop(page);
  expect(reached.name, step).toBe(stop);
  if (reached.ring !== undefined) {
    expect(reached.ring, `the focus ring on ${stop}`).toEqual(expected);
  }
}

/**
 * Expects focus on `stop`, read as `expectStops` reads each, with `expected`'s ring showing: for an element that the
 * page focused by script after a key, such as the list conflict, which no Tab reaches.
 */
export async function expectFocusAt(page: Page, stop: string, expected = ring) {
  await expectReached(page, stop, expected, `focus on ${stop}`);
}

/**
 * Presses `key` once for each of `stops`, expecting focus to land on each in turn with `expected`'s ring showing
 * (backlog README §9's keyboard-only check). Past the page's last stop, Tab takes focus out of the page, to the
 * browser, and the next Shift+Tab brings it back to that stop. In a set of radios, one stop for Tab, an arrow key
 * moves to the next radio or the one before, and chooses it.
 */
export async function expectStops(
  page: Page,
  key: 'Tab' | 'Shift+Tab' | 'ArrowDown' | 'ArrowUp',
  stops: readonly string[],
  expected = ring,
) {
  for (const [index, stop] of stops.entries()) {
    await page.keyboard.press(key);
    await expectReached(page, stop, expected, `${key} number ${index + 1}, to ${stop}`);
  }
}
