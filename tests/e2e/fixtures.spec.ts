import { test } from './fixtures.ts';

// Shows that the fixture fails a test whose page meets a Content Security Policy violation. Marked to fail, the test
// passes only if something fails it, and its own steps pass whenever the browser blocks the style, so what fails it is
// the fixture's check after it ends. Were the fixture to let the violation through, Playwright would fail the run.
test.fail('a Content Security Policy violation fails the test', async ({ page }) => {
  await page.goto('/');

  // Awaited through the page's own event rather than the console, so that the test still passes, and the run fails, if
  // Chromium stops reporting violations in the words the fixture looks for. Nothing else is asserted here: in a test
  // marked to fail, any failing assertion would count as the expected failure and hide a fixture that missed it.
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        document.addEventListener('securitypolicyviolation', () => resolve(), { once: true });
        const style = document.createElement('style');
        style.textContent = 'h1 { color: rgb(1, 2, 3); }';
        document.head.append(style);
      }),
  );
});
