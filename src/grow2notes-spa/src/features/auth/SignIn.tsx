import {
  browserSupportsWebAuthn,
  startAuthentication,
  type AuthenticationResponseJSON,
  type PublicKeyCredentialRequestOptionsJSON,
} from '@simplewebauthn/browser';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { cancelPasskey, useOneOptions } from './passkey.ts';
import styles from './SignIn.module.css';

// Identity's options JSON, which startAuthentication takes as it comes (design.md §6.2).
const requestOptions = () =>
  api<PublicKeyCredentialRequestOptionsJSON>('/api/auth/passkey/options', { method: 'POST' });

// The A25 message as the summary's one item, with no link, as it marks no field (sign-in.md component 2).
const signInFailed: readonly FormError[] = [{ message: strings.signIn.failed }];
const noErrors: readonly FormError[] = [];

// A cancel, a timeout or no passkey for the app on the device, which the browser makes look the same (WebAuthn §14.5).
// The prompt has said all there is to say, so the view says nothing (sign-in.md *Passkey prompt dismissed*).
function isDismissal(error: unknown): boolean {
  return error instanceof Error && error.name === 'NotAllowedError';
}

/**
 * Sign-in's view A, its passkey part (sign-in.md components 1 to 4, 9 and 18), which the shell shows in `<main>` at
 * the address that was opened when nobody is signed in, and titles "Grow2Notes – Sign in". Nothing takes focus as it
 * renders: an `autoFocus` would open a phone's keyboard over the passkey button.
 *
 * It fetches the passkey options as it renders, so that a press can open the prompt at once (sign-in.md
 * *Interactions* step 2). A sign-in puts its `Me` in the `['me']` query, and the shell then shows the app in its
 * place, at the same address. A sign-in that the server refuses shows the A25 message in the summary, which takes
 * focus; a request that got no answer, or a prompt that failed, says so above the button, where focus stays for
 * another press.
 */
export function SignIn() {
  const queryClient = useQueryClient();
  const supported = browserSupportsWebAuthn();
  const options = useOneOptions(requestOptions);
  const [busy, setBusy] = useState(false);
  const [requestFailure, setRequestFailure] = useState<string>();
  const [failed, setFailed] = useState(false);
  // The number of refused sign-ins, so that the summary takes focus on each, even with the same words.
  const [attempt, setAttempt] = useState(0);
  // False once the view has gone, after which nothing that ends, its prompt among them, says or fetches anything.
  const mounted = useRef(false);

  useEffect(() => {
    mounted.current = true;
    if (supported) {
      // A failure says nothing yet: a press fetches the options again, and says so if that fails too.
      options.prefetch().catch(() => {});
    }

    return () => {
      mounted.current = false;
      cancelPasskey();
    };
  }, [supported, options]);

  // Everything before its first await runs in the press itself.
  async function signIn() {
    const fresh = options.take();
    // With fresh options, the prompt opens straight from the press, with no await before it, as older iPhones refuse
    // a prompt that an awaited fetch has parted from its tap (sign-in.md *Interactions* step 2).
    const started = fresh === undefined ? undefined : startAuthentication({ optionsJSON: fresh });
    setBusy(true);
    setFailed(false);
    // Emptied, so that a failure that repeats is written into an empty region and announced again.
    setRequestFailure(undefined);

    try {
      let credential: AuthenticationResponseJSON;
      if (started === undefined) {
        const optionsJSON = await options.prefetch();
        if (!mounted.current) {
          return;
        }

        credential = await startAuthentication({ optionsJSON });
      } else {
        credential = await started;
      }

      options.spent();
      // A direct call, not a mutation, so that the credential never sits in TanStack's mutation cache.
      const me = await api<Me>('/api/auth/passkey', {
        method: 'POST',
        body: { credentialJson: JSON.stringify(credential) },
      });
      queryClient.setQueryData(meQuery.queryKey, me);
    } catch (error) {
      if (!mounted.current) {
        return;
      }

      setBusy(false);
      // The server's refusal, or its rate limiter's on any of the sign-in's requests (design.md §9.9): A25's message,
      // whichever it was (sign-in.md *Sign-in failed*).
      if (error instanceof ApiError && (error.status === 401 || error.status === 429)) {
        setFailed(true);
        setAttempt((count) => count + 1);
      } else if (!isDismissal(error)) {
        const cause = error instanceof ApiError && error.status === 0 ? 'offline' : 'server';
        setRequestFailure(msg.notDone(strings.signIn.notDone, cause));
      }

      // The server may have ended the ceremony, and the next press should still open the prompt at once.
      options.prefetch().catch(() => {});
    }
  }

  return (
    <PageStatus>
      <div className={styles.view}>
        <ErrorSummary errors={failed ? signInFailed : noErrors} attempt={attempt} />
        <PageHeading>{pageName.signIn}</PageHeading>
        <p className={styles.hint}>{strings.signIn.hint}</p>
        {supported ? (
          <div className={styles.passkey}>
            {/* Empty until a request fails, directly above the button that sent it (component 2a). */}
            <RequestAlert>{requestFailure}</RequestAlert>
            <Button variant="primary" busy={busy} busyLabel={strings.signIn.busy} onClick={() => void signIn()}>
              {strings.signIn.passkey}
            </Button>
          </div>
        ) : (
          <p className={styles.passkey}>{strings.signIn.passkeysUnavailable}</p>
        )}
        <p className={styles.help}>{strings.signIn.help}</p>
      </div>
    </PageStatus>
  );
}
