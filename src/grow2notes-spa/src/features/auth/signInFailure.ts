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
