import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';

// Signed out, the shell meets the app's real 401 from GET /api/auth/me and shows its signed-out frame (sign-in.md
// component 1; S00.05.01's Notes). A nested path also proves that the published app's fallback serves index.html and
// that its asset URLs do not depend on the page's path.
for (const path of ['/', '/manage/anything']) {
  test(`signed out, ${path} shows the signed-out frame, which passes the WCAG 2.2 AA checks`, async ({ page }) => {
    const me = page.waitForResponse((response) => new URL(response.url()).pathname === '/api/auth/me');
    const response = await page.goto(path);

    // The fixture's check of the console means something only if the page is under the policy.
    expect(await response?.headerValue('Content-Security-Policy')).toBeTruthy();
    expect((await me).status()).toBe(401);
    // The frame before /me answers looks the same as the one after its 401, so nothing on the page shows that the 401
    // has been handled. Let the answer arrive whole and React draw what it gives first, so that a signed-in control
    // the 401 brought in would fail the checks below every time rather than by chance.
    await (await me).finished();
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

    await expect(page).toHaveTitle('Grow2Notes');
    // The wordmark, as text.
    const header = page.getByRole('banner');
    await expect(header).toHaveText('Grow2Notes');
    await expect(header.getByRole('link')).toHaveCount(0);
    await expect(page.locator('nav')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Account' })).toHaveCount(0);
    await expect(page.locator('main')).toHaveCount(1);

    await expectAxeToPass(page);
  });
}
