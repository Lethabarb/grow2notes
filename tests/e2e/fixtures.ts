import { type ConsoleMessage, expect, test as base } from '@playwright/test';

export { expect };

// Chromium logs each violation of the page's Content Security Policy as a console error that names the policy, such as
// "Applying inline style violates the following Content Security Policy directive 'style-src 'self''. ...". Other
// console errors pass: once the shell asks the API who is signed in, a signed-out page load logs one for the 401 it
// expects (S06.01.04's Notes).
const reportsCspViolation = (message: ConsoleMessage) =>
  message.type() === 'error' && message.text().includes('Content Security Policy');

// Every end-to-end test imports test from here, not from @playwright/test (tests/README.md), so a screen that breaks
// design.md §9.7's policy fails its test, whatever the test checks.
export const test = base.extend({
  page: async ({ page }, use) => {
    const violations: string[] = [];
    page.on('console', (message) => {
      if (reportsCspViolation(message)) violations.push(message.text());
    });

    await use(page);

    expect(violations, 'The browser console reported a Content Security Policy violation').toEqual([]);
  },
});
