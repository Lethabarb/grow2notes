import type { Page } from '@playwright/test';

/** Where focus is: outside the page, on the page itself, or on the element that has it, by its text. */
export const whereFocusIs = (page: Page) =>
  page.evaluate(() => {
    const element = document.activeElement;
    if (!document.hasFocus()) {
      return 'outside the page';
    }

    return element === null || element === document.body ? 'the page itself' : (element.textContent ?? '');
  });
