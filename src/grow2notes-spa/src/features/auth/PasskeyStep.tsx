import {
  browserSupportsWebAuthn,
  startAuthentication,
  type AuthenticationResponseJSON,
  type PublicKeyCredentialRequestOptionsJSON,
} from '@simplewebauthn/browser';
import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { api, signInTimeoutMs } from '../../api/api.ts';
import { setMe, type Me } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { pageHeadingId } from '../../components/focusPageHeading.ts';
import { PageHeading } from '../../components/PageHeading.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { strings } from '../../copy/strings.ts';
import { cancelPasskey, useOneOptions } from './passkey.ts';
import styles from './SignIn.module.css';
import { isDismissal, isRefusal, notSignedIn } from './signInFailure.ts';

// Identity's options JSON, which startAuthentication takes as it comes (design.md §6.2). Made for the account whose
// password was just accepted, so that only its passkeys are offered and a credential of another account is refused.
const requestOptions = () =>
  api<PublicKeyCredentialRequestOptionsJSON>('/api/auth/login/passkey/options', {
    method: 'POST',
    timeoutMs: signInTimeoutMs,
  });

type PasskeyStepProps = {
  /**
   * View C's `<h1>` and sentence, above the step, under which the step's heading is an `<h2>`; left out in view A's
   * place, whose `<h1>` is the step's heading.
   */
  heading?: ReactNode;
  /** For a refusal, 401 or 429, of the step's options or credential: view A shows again, with the A25 message. */
  onRefused: () => void;
  /** For Go back, which reaches view A's password and passkey button from every state of the step. */
  onBack: () => void;
};

/**
 * Sign-in's passkey step (sign-in.md; passkey-flows.md; D97), which `SignIn` shows in view A's place, at the same
 * address and with the same title, once the password of a person who chose a password and a passkey is accepted. Its
 * options are fetched as it shows, for that account only, and the button opens the device's prompt straight from the
 * press. A prompt that is dismissed leaves the step as it is, as the server still holds its ceremony; a refusal ends
 * the step, as the code step's expiry does, and a request that got no answer says so above the button.
 */
export function PasskeyStep({ heading, onRefused, onBack }: PasskeyStepProps) {
  const queryClient = useQueryClient();
  const supported = browserSupportsWebAuthn();
  const options = useOneOptions(requestOptions);
  const hintId = useId();
  const ownHeadingId = useId();
  const signInButton = useRef<HTMLButtonElement>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string>();
  // False once the view has gone, after which an answer that comes changes nothing.
  const mounted = useRef(false);
  // The newest onRefused, so that the options are asked for once, whatever the parent renders, as a second request
  // would be refused: the first has replaced the password step in Identity's cookie.
  const refused = useRef(onRefused);
  // Whether the step has been refused: StrictMode attaches two handlers to the options' one fetch, and it ends once.
  const ended = useRef(false);
  const headingId = heading === undefined ? pageHeadingId : ownHeadingId;

  useEffect(() => {
    refused.current = onRefused;
  });

  const refuse = useCallback(() => {
    if (!ended.current) {
      ended.current = true;
      refused.current();
    }
  }, []);

  useEffect(() => {
    mounted.current = true;
    if (supported) {
      // Any other failure says nothing yet: a press fetches the options again, and says so if that fails too.
      options.prefetch().catch((error: unknown) => {
        if (mounted.current && isRefusal(error)) {
          refuse();
        }
      });
      signInButton.current?.focus();
    } else {
      document.getElementById(headingId)?.focus();
    }

    return () => {
      mounted.current = false;
      cancelPasskey();
    };
  }, [supported, options, headingId, refuse]);

  // Everything before its first await runs in the press itself.
  async function signIn() {
    const fresh = options.take();
    // With fresh options, the prompt opens straight from the press, with no await before it, as older iPhones refuse
    // a prompt that an awaited fetch has parted from its tap (sign-in.md *Interactions* step 2).
    const started = fresh === undefined ? undefined : startAuthentication({ optionsJSON: fresh });
    setBusy(true);
    // Emptied, so that a failure that repeats is written into an empty region and announced again.
    setFailure(undefined);

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

      // Not spent: the step has one set of options, which a retry after a dismissed prompt uses again, until their age
      // runs out and the fetch that then follows is refused.
      // A direct call, not a mutation, so that the credential never sits in TanStack's mutation cache.
      const me = await api<Me>('/api/auth/passkey', {
        method: 'POST',
        body: { credentialJson: JSON.stringify(credential) },
        timeoutMs: signInTimeoutMs,
      });
      await setMe(queryClient, me);
    } catch (error) {
      if (!mounted.current) {
        return;
      }

      setBusy(false);
      if (isRefusal(error)) {
        refuse();
      } else if (!isDismissal(error)) {
        setFailure(notSignedIn(error));
      }
    }
  }

  const stepHeading = strings.signIn.passkeyStep.heading;

  return (
    <>
      {heading}
      {heading === undefined ? (
        <PageHeading>{stepHeading}</PageHeading>
      ) : (
        <h2 id={ownHeadingId} tabIndex={-1}>
          {stepHeading}
        </h2>
      )}
      <p id={hintId} className={styles.hint}>
        {strings.signIn.passkeyStep.hint}
      </p>
      {supported ? (
        <div className={styles.passkey}>
          {/* Empty until a request fails, directly above the button that sent it (component 2a). */}
          <RequestAlert>{failure}</RequestAlert>
          <Button
            ref={signInButton}
            variant="primary"
            busy={busy}
            busyLabel={strings.signIn.busy}
            aria-describedby={hintId}
            onClick={() => void signIn()}
          >
            {strings.signIn.passkey}
          </Button>
        </div>
      ) : (
        <p className={styles.passkey}>{strings.signIn.passkeysUnavailable}</p>
      )}
      <Button onClick={onBack}>{strings.goBack}</Button>
    </>
  );
}
