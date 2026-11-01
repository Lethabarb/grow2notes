import type { PublicKeyCredentialCreationOptionsJSON } from '@simplewebauthn/browser';
import { api, ApiError, classify } from '../../api/api.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';

// Identity's options JSON, which startRegistration takes as it comes (design.md §6.2).
export const creationOptions = () =>
  api<PublicKeyCredentialCreationOptionsJSON>('/api/auth/setup/passkey/options', { method: 'POST' });

/** The alert's words for a passkey that failed to be set up, or `undefined` for a `401`. */
export function passkeyAlertFor(error: unknown): string | undefined {
  // `api()` fails with an ApiError, so anything else came from the prompt: the browser made no passkey, whatever the
  // reason, which WebAuthn keeps from the page, an abort that the view did not start among them. A 422 is the
  // server refusing the passkey that it made.
  if (!(error instanceof ApiError) || error.status === 422) {
    return strings.setup.passkeyNotSetUp;
  }

  if (error.status === 429) {
    return strings.setup.tooManyTries;
  }

  // A 401 from a setup call means that its enrolment cookie has ended.
  const cause = classify(error);
  return cause === 'signedOut' ? undefined : msg.notSetUp(cause);
}
