import type { Page } from '@playwright/test';
import { expect, type Invite, type VirtualAuthenticator } from './fixtures.ts';

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
