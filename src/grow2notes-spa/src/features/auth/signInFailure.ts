import { ApiError } from '../../api/api.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';

/**
 * The server's refusal, or its rate limiter's on any of the sign-in's requests (design.md §9.9): A25's message,
 * whichever it was (sign-in.md *Sign-in failed*).
 */
export function isRefusal(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 401 || error.status === 429);
}

/** The alert line's words for a sign-in request that got no answer, or a failure that is not a refusal. */
export function notSignedIn(error: unknown): string {
  return msg.notDone(strings.signIn.notDone, error instanceof ApiError && error.status === 0 ? 'offline' : 'server');
}

// A cancel, a timeout or no passkey for the app on the device, which the browser makes look the same (WebAuthn §14.5).
// The prompt has said all there is to say, so the view says nothing (sign-in.md *Passkey prompt dismissed*).
export function isDismissal(error: unknown): boolean {
  return error instanceof Error && error.name === 'NotAllowedError';
}
