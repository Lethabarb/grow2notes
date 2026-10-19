import type { Page } from '@playwright/test';
import { authenticatorCode } from './authenticatorCode.ts';
import { expect, type Invite, type VirtualAuthenticator } from './fixtures.ts';

/** The label of the code step's field, at setup's view G and at sign-in's code step alike. */
export const codeLabel = 'Enter the 6-digit code from your authenticator app';

const main = (page: Page) => page.getByRole('main');

/**
 * Sets up the invited manager's account through setup with a passkey on the virtual authenticator, as setup.spec.ts
 * proves, ending on Today, signed in, with one passkey more on the authenticator than before.
 */
export async function signInThroughSetup(page: Page, invite: Invite, authenticator: VirtualAuthenticator) {
  const before = (await authenticator.credentials()).length;
  await page.goto(invite.setupLink);
  await page.getByRole('main').getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(before + 1);
}

/**
 * Sets `person`'s account up with a password and an authenticator app, as setup.spec.ts's password path does, ending on
 * Today, signed in, and gives the password and the setup key, from which `authenticatorCode` gives each code.
 */
export async function setUpWithPassword(page: Page, person: Invite) {
  const password = 'quietharbour';
  await page.goto(person.setupLink);
  await main(page).getByRole('link', { name: 'Use a password and authenticator app' }).click();
  await page.getByLabel('Create a password', { exact: true }).fill(password);
  await main(page).getByRole('button', { name: 'Continue' }).click();
  const shownKey = main(page).getByRole('definition');
  await expect(shownKey).toHaveText(/^[A-Z2-7]{32}$/);
  const key = await shownKey.innerText();
  const finished = page.waitForResponse((response) => new URL(response.url()).pathname === '/api/auth/setup/password');
  await page.getByLabel(codeLabel).fill(authenticatorCode(key));
  await main(page).getByRole('button', { name: 'Finish setup' }).click();
  expect((await finished).status()).toBe(200);
  await expect(main(page).getByRole('heading', { level: 1 })).toContainText('Today');
  return { password, key };
}
