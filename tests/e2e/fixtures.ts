import { execFile } from 'node:child_process';
import { generateKeyPairSync, randomBytes, randomUUID } from 'node:crypto';
import { promisify } from 'node:util';
import { type Browser, type ConsoleMessage, type Page, expect, test as base } from '@playwright/test';

export { expect };

// Chromium logs each violation of the page's Content Security Policy as a console error that names the policy, such as
// "Applying inline style violates the following Content Security Policy directive 'style-src 'self''. ...". Other
// console errors pass: once the shell asks the API who is signed in, a signed-out page load logs one for the 401 it
// expects (S06.01.04's Notes).
const reportsCspViolation = (message: ConsoleMessage) =>
  message.type() === 'error' && message.text().includes('Content Security Policy');

/** A first manager whom the published app's `admin bootstrap` invited, with the setup link that it printed. */
export type Invite = {
  displayName: string;
  email: string;
  /** The link as the command printed it, `…/setup#u=…&t=…`. */
  setupLink: string;
};

// The published app's folder, which the operator commands run from, and the two settings that they read from the
// environment, which the SSH console has from the app (tests/README.md).
const commandSettings = ['E2E_APP_DIR', 'ConnectionStrings__Grow2Notes', 'App__Origin'];

/**
 * Runs one of the published app's own operator commands that print a setup link (ops/break-glass.md) as the SSH
 * console runs it: from the app's folder, with the settings of the app under test, so that the link opens there.
 * Gives the link, which the command prints and nothing else.
 */
async function adminCommand(baseURL: string | undefined, args: readonly string[]): Promise<string> {
  const missing = commandSettings.filter((name) => !process.env[name]);
  if (missing.length > 0) {
    throw new Error(`admin ${args[0]} needs these settings, which tests/README.md describes: ${missing.join(', ')}.`);
  }

  const { stdout } = await promisify(execFile)(
    'dotnet',
    ['Grow2Notes.Web.dll', 'admin', ...args],
    // Production, as the app runs in CI and in App Service, so that no appsettings.Development.json is read.
    { cwd: process.env.E2E_APP_DIR, env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production' } },
  );

  const setupLink = stdout.trim();
  expect(new URL(setupLink).origin, 'App__Origin is the address that the tests open').toBe(
    new URL(baseURL ?? '').origin,
  );
  return setupLink;
}

/**
 * Runs the published app's own `admin bootstrap` for a made-up manager of a made-up organisation, called `firstName`
 * Sample, with an address that is new each time, as one address has one account across the app (A27, A35).
 */
async function adminBootstrap(baseURL: string | undefined, firstName: string): Promise<Invite> {
  const manager = {
    displayName: `${firstName} Sample`,
    email: `${firstName.toLowerCase()}.sample.${randomUUID()}@example.org`,
  };
  const setupLink = await adminCommand(baseURL, [
    'bootstrap',
    '--organisation', 'Sample Support Services',
    '--manager-name', manager.displayName,
    '--manager-email', manager.email,
  ]);
  return { ...manager, setupLink };
}

/**
 * Chromium's virtual authenticator, through the DevTools protocol, in place of a phone (passkey-flows.md *Tests*): one
 * built into the device, which makes discoverable passkeys and verifies the person, as a face, fingerprint or PIN does,
 * and answers each prompt at once. With no passkey for the domain that a prompt names, the prompt fails at once with
 * `NotAllowedError`, as a dismissed one does.
 */
async function addVirtualAuthenticator(page: Page) {
  const devTools = await page.context().newCDPSession(page);
  await devTools.send('WebAuthn.enable');
  const { authenticatorId } = await devTools.send('WebAuthn.addVirtualAuthenticator', {
    options: {
      protocol: 'ctap2',
      transport: 'internal',
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
    },
  });

  return {
    /** Whether the person passes verification at the next prompt; with false, as when they fail, no passkey is made. */
    setUserVerified: async (isUserVerified: boolean) => {
      await devTools.send('WebAuthn.setUserVerified', { authenticatorId, isUserVerified });
    },
    /** The passkeys that it holds. */
    credentials: async () => (await devTools.send('WebAuthn.getCredentials', { authenticatorId })).credentials,
    /**
     * Adds a passkey for the domain `rpId` that no account holds, with a P-256 key, a credential ID and a user handle
     * made here. The app's sign-in options name no credentials, so the next prompt for that domain signs with it.
     */
    addPasskey: async (rpId: string) => {
      const { privateKey } = generateKeyPairSync('ec', { namedCurve: 'P-256' });
      // The protocol takes binary values as base64.
      await devTools.send('WebAuthn.addCredential', {
        authenticatorId,
        credential: {
          credentialId: randomBytes(16).toString('base64'),
          isResidentCredential: true,
          rpId,
          privateKey: privateKey.export({ format: 'der', type: 'pkcs8' }).toString('base64'),
          userHandle: randomBytes(16).toString('base64'),
          signCount: 0,
        },
      });
    },
  };
}

export type VirtualAuthenticator = Awaited<ReturnType<typeof addVirtualAuthenticator>>;

/** A second device: a page in a browser context of its own, with no cookies of the test's page, and its own phone. */
export type Device = { page: Page; authenticator: VirtualAuthenticator };

/**
 * Notes each violation of the page's Content Security Policy that its console reports from now on. Gives a function
 * that expects there to have been none.
 */
function watchForCspViolations(page: Page): () => void {
  const violations: string[] = [];
  page.on('console', (message) => {
    if (reportsCspViolation(message)) violations.push(message.text());
  });
  return () => {
    expect(violations, 'The browser console reported a Content Security Policy violation').toEqual([]);
  };
}

/**
 * Gives `use` a device of its own: a page in a new browser context, which takes the test's options, its base URL and
 * headers among them, with none of the test page's cookies, under the same check of the policy, and its own phone.
 */
async function useDevice(browser: Browser, use: (device: Device) => Promise<void>) {
  const context = await browser.newContext();
  const page = await context.newPage();
  const expectNoViolations = watchForCspViolations(page);
  await use({ page, authenticator: await addVirtualAuthenticator(page) });
  await context.close();
  expectNoViolations();
}

// Every end-to-end test imports test from here, not from @playwright/test (tests/README.md), so a screen that breaks
// design.md §9.7's policy fails its test, whatever the test checks. A test that names invite, secondInvite,
// resetSignIn, authenticator, secondDevice or thirdDevice gets them; the others never run a command or add an
// authenticator.
export const test = base.extend<{
  invite: Invite;
  secondInvite: Invite;
  resetSignIn: (email: string) => Promise<string>;
  authenticator: VirtualAuthenticator;
  secondDevice: Device;
  thirdDevice: Device;
}>({
  page: async ({ page }, use) => {
    const expectNoViolations = watchForCspViolations(page);
    await use(page);
    expectNoViolations();
  },
  invite: async ({ baseURL }, use) => {
    await use(await adminBootstrap(baseURL, 'Alex'));
  },
  // Another person, of an organisation of their own, for a test of two people on one browser.
  secondInvite: async ({ baseURL }, use) => {
    await use(await adminBootstrap(baseURL, 'Jordan'));
  },
  // The published app's own admin reset-signin for an address, which gives the new link that it prints: for someone
  // invited from the Users screen, whose emailed link the test cannot read, the way to a link of their own.
  resetSignIn: async ({ baseURL }, use) => {
    await use((email) => adminCommand(baseURL, ['reset-signin', email]));
  },
  authenticator: async ({ page }, use) => {
    await use(await addVirtualAuthenticator(page));
  },
  secondDevice: async ({ browser }, use) => {
    await useDevice(browser, use);
  },
  // A third person's, such as a second manager beside a worker and the first.
  thirdDevice: async ({ browser }, use) => {
    await useDevice(browser, use);
  },
});
