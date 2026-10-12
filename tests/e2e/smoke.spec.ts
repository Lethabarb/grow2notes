import { expectAxeToPass } from './axe.ts';
import { expect, test } from './fixtures.ts';
import { expectViewA } from './signedOut.ts';

// Signed out, the shell meets the app's real 401 from GET /api/auth/me and shows sign-in's view A in its signed-out
// frame, at the address that was opened (sign-in.md *Routes* and component 1). A nested path also proves that the
// published app's fallback serves index.html and that its asset URLs do not depend on the page's path.
for (const path of ['/', '/manage/anything']) {
  test(`signed out, ${path} shows view A in the signed-out frame, which passes the WCAG 2.2 AA checks`, async ({
    page,
  }) => {
    const me = page.waitForResponse((response) => new URL(response.url()).pathname === '/api/auth/me');
    const response = await page.goto(path);

    // The fixture's check of the console means something only if the page is under the policy.
    expect(await response?.headerValue('Content-Security-Policy')).toBeTruthy();
    expect((await me).status()).toBe(401);

    await expectViewA(page);
    expect(new URL(page.url()).pathname).toBe(path);

    await expectAxeToPass(page);
  });
}

test('signed out, view A has its status region and the primary button in the shared look', async ({ page }) => {
  await page.goto('/');

  // Chromium has WebAuthn on localhost, a secure context, so the button shows rather than the notice.
  const passkey = page.getByRole('button', { name: 'Sign in with a passkey' });
  await expect(passkey).toBeVisible();
  // Without the button's own stylesheet it would be grey, so this shows that it loaded under the policy: the primary
  // style's --colour-action, #00558b.
  await expect(passkey).toHaveCSS('background-color', 'rgb(0, 85, 139)');

  // PageStatus renders the page's one status region from its first render, empty until the page announces something
  // (app-shell.md component 3).
  const status = page.getByRole('status');
  await expect(status).toHaveCount(1);
  await expect(status).toBeEmpty();
});
