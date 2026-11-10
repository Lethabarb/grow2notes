import type { PublicKeyCredentialCreationOptionsJSON } from '@simplewebauthn/browser';
import { api, ApiError, classify } from '../../api/api.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';

// Identity's options JSON, which startRegistration takes as it comes (design.md §6.2).
export const creationOptions = () =>
  api<PublicKeyCredentialCreationOptionsJSON>('/api/auth/setup/passkey/options', { method: 'POST' });

/**
 * Sends why the browser made no passkey to the server's log (D103), as nothing else records a prompt that failed on
 * the device. An `ApiError` is left out: the server gave it, so it has seen it. Nothing waits on it, and its own
 * failure is let go, as the alert says what the person needs either way.
 */
export function recordPasskeyFailure(error: unknown): void {
  if (error instanceof ApiError) {
    return;
  }

  const { name, code, message } = describeFailure(error);
  api('/api/auth/setup/passkey/failure', {
    method: 'POST',
    body: {
      name: name.slice(0, limits.passkeyFailureName),
      code: code?.slice(0, limits.passkeyFailureCode),
      message: message.slice(0, limits.passkeyFailureMessage),
    },
  }).catch(() => undefined);
}

// SimpleWebAuthn's WebAuthnError has the browser's error's name, a code of its own and its own reading as the message,
// and keeps the browser's error, a DOMException, as its cause, whose words are added where they differ.
function describeFailure(error: unknown): { name: string; code: string | undefined; message: string } {
  const { name, code, message = '', cause } = fieldsOf(error);
  const { name: causeName = 'Error', message: causeMessage } = fieldsOf(cause);
  return {
    name: name || 'Error',
    code,
    message:
      causeMessage === undefined || causeMessage === message ? message : `${message} (${causeName}: ${causeMessage})`,
  };
}

// Read as properties, not by class, as a DOMException's are getters and an error may come from another realm.
function fieldsOf(value: unknown): { name?: string; code?: string; message?: string; cause?: unknown } {
  if (typeof value !== 'object' || value === null) {
    return {};
  }

  const { name, code, message, cause } = value as Record<string, unknown>;
  const text = (field: unknown) => (typeof field === 'string' ? field : undefined);
  return { name: text(name), code: text(code), message: text(message), cause };
}

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
