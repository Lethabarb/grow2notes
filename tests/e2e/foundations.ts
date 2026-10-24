import { AxeBuilder } from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { expect } from './fixtures.ts';
import { ring, tabTo } from './focus.ts';
import type { Ring } from './focus.ts';
import { setTextTo200Percent, systemColour } from './layout.ts';

// What a browser can prove from foundations.md's manual test list, for any screen in any state, in one place, so that
// each screen's spec calls it once rather than carrying its own copy.

/**
 * Expects the text on the page as it is to reach 7:1 against its background (foundations.md's colour table), by axe's
 * AAA contrast rule, one target per line. That rule holds normal text to 7:1 and large text, 24 px or more, or 18.67 px
 * and bold, such as a heading or any text at 200%, to 4.5:1, so it proves 7:1 for normal text only; tokens.test.ts
 * holds every colour pair, headings included, to its ratio. It does not reach a placeholder, which is no text node,
 * or the pressed secondary label, which no static check holds pressed: tokens.test.ts holds those two pairs to 4.5:1.
 * If it ever flags an element painted with one of them, leave that element out by selector, with a comment naming the
 * pair, and never change a token to pass it.
 */
export async function expectTextAt7To1(page: Page, state?: string): Promise<void> {
  const { violations } = await new AxeBuilder({ page }).withRules(['color-contrast-enhanced']).analyze();
  const failing = violations.flatMap(({ nodes }) => nodes.map(({ target }) => target.join(' ')));
  expect(failing, state === undefined ? 'Text below 7:1' : `Text below 7:1, ${state}`).toEqual([]);
}

/**
 * Expects nothing on the page to move (SC 2.3.3): no element, and no `::before` or `::after`, with an animation, a
 * transition lasting longer than 0, or smooth scrolling. With reduced motion off, as Playwright has it by default,
 * because base.css's reduce rule shortens every duration to 0.01 ms, which would hide a transition.
 */
export async function expectNothingToMove(page: Page): Promise<void> {
  const moving = await page.evaluate(() =>
    [...document.querySelectorAll('*')].flatMap((element) =>
      [null, '::before', '::after'].flatMap((pseudo) => {
        const { animationName, transitionDuration, scrollBehavior } = getComputedStyle(element, pseudo);
        const moves =
          animationName !== 'none'
          || transitionDuration.split(', ').some((duration) => parseFloat(duration) > 0)
          || scrollBehavior === 'smooth';
        return moves ? [`<${element.localName}>${pseudo ?? ''}`] : [];
      }),
    ),
  );
  expect(moving, 'Elements with an animation, a transition or smooth scrolling').toEqual([]);
}

/**
 * WCAG's text spacing (SC 1.4.12), on every element, as its bookmarklet sets it: lines 1.5 times the text apart,
 * paragraphs 2 times, letters 0.12 times and words 0.16 times. Through a constructed stylesheet, which the page's
 * Content Security Policy allows, as it refuses a `<style>` element.
 */
export async function spaceTextOut(page: Page) {
  await page.evaluate(() => {
    const spacing = new CSSStyleSheet();
    spacing.replaceSync(
      '* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; }'
        + ' p { margin-block-end: 2em !important; }',
    );
    document.adoptedStyleSheets = [...document.adoptedStyleSheets, spacing];
  });
}

/**
 * Expects the page as it is to fit the window's width (SC 1.4.10): no sideways scroll; no element, and no line of an
 * element's own text, reaching past the window's sides or past its element's; no element cutting off what it holds,
 * but for a box that scrolls up and down, as the dialog may; and the field showing all its text, as it grows with it.
 * Words hidden on purpose by a clip, as the visually hidden ones and the skip link are, are left out.
 */
export async function expectToFit(page: Page, state: string) {
  const problems = await page.evaluate(() => {
    const windowWidth = document.documentElement.clientWidth;
    const found: string[] = [];
    if (document.documentElement.scrollWidth > windowWidth) {
      found.push('the page scrolls sideways');
    }

    const named = (element: Element) => `<${element.localName}> "${(element.textContent ?? '').trim().slice(0, 40)}"`;
    const clipped = (element: Element | null): boolean =>
      element !== null && (getComputedStyle(element).clipPath !== 'none' || clipped(element.parentElement));
    for (const element of document.body.querySelectorAll('*')) {
      const style = getComputedStyle(element);
      if (element.getClientRects().length === 0 || style.visibility !== 'visible' || clipped(element)) {
        continue;
      }

      const box = element.getBoundingClientRect();
      if (box.left < -1 || box.right > windowWidth + 1) {
        found.push(`${named(element)} reaches past the window`);
      }

      if (
        (style.overflowX !== 'visible' && element.scrollWidth > element.clientWidth + 1)
        || (['hidden', 'clip'].includes(style.overflowY) && element.scrollHeight > element.clientHeight + 1)
        || (element.localName === 'textarea' && element.scrollHeight > element.clientHeight + 1)
      ) {
        found.push(`${named(element)} cuts off what it holds`);
      }

      for (const node of element.childNodes) {
        if (node.nodeType !== Node.TEXT_NODE || (node.textContent ?? '').trim() === '') {
          continue;
        }

        const text = document.createRange();
        text.selectNodeContents(node);
        if ([...text.getClientRects()].some(({ left, right }) => left < box.left - 1 || right > box.right + 1)) {
          found.push(`the text of ${named(element)} reaches past it`);
        }
      }
    }

    return found;
  });
  expect(problems, state).toEqual([]);
}

/**
 * Expects nothing to be pinned to the window, fixed or sticky (foundations.md): none rendered, but for an
 * open modal dialog, which the browser fixes in its top layer, and, if `topBar`, the note form's top bar, which is
 * `#page-bar`'s first child.
 */
async function expectNothingPinned(page: Page, topBar: boolean, state: string) {
  const pinned = await page.evaluate((allowTopBar) => {
    const allowed = allowTopBar ? document.querySelector('#page-bar')?.firstElementChild : null;
    return [...document.querySelectorAll('*')]
      .filter(
        (element) =>
          element !== allowed
          && element.getClientRects().length > 0
          && !element.matches('dialog:modal')
          && ['fixed', 'sticky'].includes(getComputedStyle(element).position),
      )
      .map((element) => `<${element.localName}> "${(element.textContent ?? '').trim().slice(0, 40)}"`);
  }, topBar);
  expect(pinned, `Elements pinned to the window, ${state}`).toEqual([]);
}

/**
 * Expects a focus ring, as `expected` has it, on every Tab stop of the page as it is, and no trap: Tab to outside the
 * page, which is past the last stop, and then Shift+Tab to outside the page, which is before the first, whose pass
 * has to name at least one stop. The forward pass starts where focus is.
 */
async function expectRingOnEveryStop(page: Page, expected: Ring) {
  await tabTo(page, 'outside the page', 'Tab', expected);
  const backwards = await tabTo(page, 'outside the page', 'Shift+Tab', expected);
  expect(backwards, 'the Tab stops that Shift+Tab passed').not.toEqual([]);
}

// The windows that foundations.md's zoom items give at 1280x800: the narrowest phone, upright, and 400% and 200% zoom,
// which are 320x200 and 640x400 CSS pixels, both under base.css's (max-height: 30rem) layout.
const windows = [
  { width: 320, height: 800, name: '320x800, the narrowest phone' },
  { width: 320, height: 200, name: '320x200, 400% zoom' },
  { width: 640, height: 400, name: '640x400, 200% zoom' },
];

/** Expects the page to fit (`expectToFit`) at each of `windows`, with the text as it is, named `text`. */
async function expectToFitInEachWindow(page: Page, text: string) {
  for (const { name, ...size } of windows) {
    await page.setViewportSize(size);
    await expectToFit(page, `${name}, ${text}`);
  }
}

/**
 * Expects the page as it is to meet what a browser can prove of foundations.md's manual test list, in this order:
 * 1. the page's language is en-AU, at 1280x800;
 * 2. its text is 7:1 (`expectTextAt7To1`);
 * 3. nothing moves (`expectNothingToMove`);
 * 4. at 100% text, nothing is pinned to the window, at 1280x800, but for the note form's top bar if `topBar`, and at
 *    640x400, where the top bar is no longer sticky, nothing at all;
 * 5. every Tab stop shows the focus ring, and none traps focus, in the page's colours and then in forced colours,
 *    at 1280x800;
 * 6. it fits at 320x800, 320x200 and 640x400 (`expectToFit`), at 100% text, at 200% text and at 200% text with WCAG
 *    1.4.12's spacing.
 *
 * Text size and spacing cannot be undone on a page, so call it last in a test, in a state where nothing has yet
 * changed either: it leaves the page at 640x400 with 200% spaced text.
 */
export async function expectFoundationsToPass(page: Page, { topBar = false }: { topBar?: boolean } = {}) {
  await page.setViewportSize({ width: 1280, height: 800 });
  await expect(page.locator('html')).toHaveAttribute('lang', 'en-AU');
  await expectTextAt7To1(page);
  await expectNothingToMove(page);
  await expectNothingPinned(page, topBar, '1280x800');
  await page.setViewportSize({ width: 640, height: 400 });
  await expectNothingPinned(page, false, '640x400');

  await page.setViewportSize({ width: 1280, height: 800 });
  await expectRingOnEveryStop(page, ring);
  await page.emulateMedia({ forcedColors: 'active' });
  await expectRingOnEveryStop(page, { ...ring, colour: await systemColour(page, 'Highlight') });
  await page.emulateMedia({ forcedColors: 'none' });

  await expectToFitInEachWindow(page, '100% text');
  await setTextTo200Percent(page);
  await expectToFitInEachWindow(page, '200% text');
  await spaceTextOut(page);
  await expectToFitInEachWindow(page, '200% text with spacing');
}
