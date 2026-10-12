import { WebAuthnAbortService } from '@simplewebauthn/browser';
import { useState } from 'react';

// Inside the 5 minutes for which Identity keeps a passkey ceremony, in its two-factor cookie (D69), so that a prompt
// started with options this old still has time to finish (passkey-flows.md *Behaviour*).
const maxAgeMs = 4 * 60_000;

/**
 * One set of passkey options at a time (passkey-flows.md *Behaviour*): Identity keeps one ceremony, in one cookie, so
 * the answer to each request for options ends the ceremony of the options before it.
 */
export type OneOptions<T> = {
  /**
   * Fetches new options in place of those held, or gives the fetch already under way, so that no two are in flight
   * at once and the options held are always those of the ceremony the server keeps.
   */
  prefetch: () => Promise<T>;
  /** The options held, while they are under 4 minutes old, at once, so that a press can start the prompt with them. */
  take: () => T | undefined;
  /** Forgets the options held, once a credential made with them has gone to the server, which ends their ceremony. */
  spent: () => void;
  /**
   * Settles as the request for options on its way ends, however it ends, or at once when none is. It starts none.
   * The same cookie keeps a password sign-in's code step, which the answer to a request still on its way when the
   * password was accepted would end (S00.04.05's Notes).
   */
  idle: () => Promise<void>;
};

/** The view's one set of passkey options, the same object on every render, fetched by `fetchOptions`. */
export function useOneOptions<T>(fetchOptions: () => Promise<T>): OneOptions<T> {
  const [options] = useState(() => oneOptions(fetchOptions));
  return options;
}

function oneOptions<T>(fetchOptions: () => Promise<T>): OneOptions<T> {
  let held: { value: T; at: number } | undefined;
  let pending: Promise<T> | undefined;

  return {
    prefetch() {
      if (pending === undefined) {
        held = undefined;
        // Stamped as they are asked for, before the server starts their ceremony's 5 minutes.
        const at = Date.now();
        pending = fetchOptions()
          .then((value) => {
            held = { value, at };
            return value;
          })
          .finally(() => {
            pending = undefined;
          });
      }

      return pending;
    },
    take: () => (held !== undefined && Date.now() - held.at < maxAgeMs ? held.value : undefined),
    spent() {
      held = undefined;
    },
    async idle() {
      await pending?.catch(() => {});
    },
  };
}

/** Closes the passkey prompt under way, if any, as when the view that opened it goes. */
export function cancelPasskey(): void {
  WebAuthnAbortService.cancelCeremony();
}
