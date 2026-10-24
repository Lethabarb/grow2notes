import type { Locator, Page } from '@playwright/test';
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
function focusStop(page: Page): Promise<{ name: string; ring?: Ring; dateField?: boolean }> {
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
      dateField: element instanceof HTMLInputElement && element.type === 'date',
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

/** More presses than any screen has stops, so that `tabTo` calls a trap after this many. */
const maxPresses = 200;

/**
 * Presses `key` at least once, then until focus is on `stop`, which is a Locator, to be the one element that is the
 * page's `document.activeElement` (Playwright's strictness fails a Locator that finds more than one), or the browser
 * itself, `'outside the page'`, where Tab takes focus past the page's last stop and Shift+Tab before its first. Every
 * stop that it passes, and `stop` itself, must show `expected`'s ring, as `focusStop` reads it, but for a date field's
 * calendar button, which the browser rings; outside the page there is none. Returns the names of the stops that it
 * passed, without `stop`, in order. It never wraps: the caller chooses the key, and focus leaving the page before a
 * Locator stop, or no stop after 200 presses, which is more stops than any screen has, fails the test, as a trap or a
 * stop that the key never reaches does.
 */
export async function tabTo(
  page: Page,
  stop: Locator | 'outside the page',
  key: 'Tab' | 'Shift+Tab' = 'Tab',
  expected = ring,
): Promise<string[]> {
  const target = stop === 'outside the page' ? stop : stop.toString();
  if (typeof stop !== 'string') {
    await expect(stop).toBeAttached();
  }

  const passed: string[] = [];
  for (let press = 1; press <= maxPresses; press++) {
    await page.keyboard.press(key);
    const reached = await focusStop(page);
    if (reached.name === 'outside the page') {
      expect(target, `${key} number ${press} took focus out of the page, before ${target}`).toBe('outside the page');
      return passed;
    }

    // A native date field has a stop for each part of its date and one for its calendar button, which the browser rings
    // in its own style: the field does not match `:focus-visible` there, so the page's ring is not drawn.
    const onCalendarButton = reached.dateField === true && reached.ring?.focusVisible === false;
    if (!onCalendarButton) {
      expect(reached.ring, `${key} number ${press}, to ${reached.name}: the focus ring on it`).toEqual(expected);
    }

    if (typeof stop !== 'string' && (await stop.evaluate((element) => element === document.activeElement))) {
      return passed;
    }

    passed.push(reached.name);
  }

  throw new Error(`${key} ${maxPresses} times did not reach ${target}; after ${passed.slice(-3).join(', ')}.`);
}
