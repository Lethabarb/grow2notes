import {
  browserSupportsWebAuthn,
  startRegistration,
  type PublicKeyCredentialCreationOptionsJSON,
  type RegistrationResponseJSON,
} from '@simplewebauthn/browser';
import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { api, ApiError, classify } from '../../api/api.ts';
import { setMe, type Me } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { buttonClass } from '../../components/buttonClass.ts';
import { PageHeading } from '../../components/PageHeading.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { useErrorTitle } from '../../components/useErrorTitle.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { cancelPasskey, useOneOptions } from '../../features/auth/passkey.ts';
import styles from './ChooseMethod.module.css';
import type { SetupAccount } from './setupContext.ts';

// Identity's options JSON, which startRegistration takes as it comes (design.md §6.2).
const creationOptions = () =>
  api<PublicKeyCredentialCreationOptionsJSON>('/api/auth/setup/passkey/options', { method: 'POST' });

// Sent with every passkey, as design.md §6.2's body has it, and never shown (D70); the server stores each under this
// name whatever it gets.
const passkeyName = 'Grow2Notes';

type ChooseMethodProps = {
  /** The account the link named, which setup holds in memory. */
  account: SetupAccount;
  /**
   * Called for a `401` from a setup call, as the enrolment cookie has ended. The same function on every render, as a
   * new one would cancel the prompt and prefetch the options again.
   */
  onTimedOut: () => void;
};

/**
 * View E (sign-in.md, its *Setup* states and *Interactions* 8): the account the link named, and the choice between a
 * passkey and a password with an authenticator app. In a browser with WebAuthn, the passkey's creation options are
 * fetched as the view shows, so that a press can open the device's prompt at once, and its passkey finishes setup and
 * goes to Today; the prompt opens only on a press, never as the page loads. In one without, a notice says to open the
 * link in the phone's own browser, and the password becomes the primary choice.
 */
export function ChooseMethod({ account, onTimedOut }: ChooseMethodProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const passkeyHintId = useId();
  const passwordHintId = useId();
  const supported = browserSupportsWebAuthn();
  const options = useOneOptions(creationOptions);
  const [busy, setBusy] = useState(false);
  // The alert's words while it holds a failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  useErrorTitle(failure !== undefined);
  // False once the view has gone, after which nothing that ends, its prompt among them, says or fetches anything.
  const mounted = useRef(false);

  // Options for the next press. A failure says nothing, as the press asks again, but for a `401`: the enrolment cookie
  // has ended, and no press can get past it.
  const prefetch = useCallback(() => {
    options.prefetch().catch((error: unknown) => {
      if (mounted.current && classify(error) === 'signedOut') {
        onTimedOut();
      }
    });
  }, [options, onTimedOut]);

  useEffect(() => {
    mounted.current = true;
    if (supported) {
      prefetch();
    }

    return () => {
      mounted.current = false;
      cancelPasskey();
    };
  }, [supported, prefetch]);

  // Everything before its first await runs in the press itself.
  async function setUp() {
    const fresh = options.take();
    // With fresh options, the prompt opens straight from the press, with no await before it, as older iPhones refuse
    // a prompt that an awaited fetch has parted from its tap (sign-in.md *Interactions* 2 and 8).
    const started = fresh === undefined ? undefined : startRegistration({ optionsJSON: fresh });
    setBusy(true);
    setFailure(undefined);

    try {
      let credential: RegistrationResponseJSON;
      if (started === undefined) {
        // The prefetch still on its way, if any, rather than a second request, whose ceremony would replace its own.
        const optionsJSON = await options.prefetch();
        if (!mounted.current) {
          return;
        }

        credential = await startRegistration({ optionsJSON });
      } else {
        credential = await started;
      }

      options.spent();
      const me = await api<Me>('/api/auth/setup/passkey', {
        method: 'POST',
        body: { credentialJson: JSON.stringify(credential), name: passkeyName },
      });
      // Into `['me']` before the page goes to `/`, so that Today shows at once for the person now signed in, not what
      // the shell's `/me` got before setup, a `401` and so sign-in, until the new location's refresh answers
      // (sign-in.md *Interactions* 5).
      await setMe(queryClient, me);
      // Replaced, so that Back from Today does not come back to setup. Leaving the setup layout drops its memory.
      void navigate('/', { replace: true });
    } catch (error) {
      // The view has gone, and its prompt with it.
      if (!mounted.current) {
        return;
      }

      const words = alertFor(error);
      if (words === undefined) {
        onTimedOut();
        return;
      }

      // Focus stays on the button, so that pressing it again tries again, with new options for a prompt at once.
      setBusy(false);
      setFailure(words);
      prefetch();
    }
  }

  return (
    <div className={styles.view}>
      <PageHeading>{pageName.setUpYourAccount}</PageHeading>
      <dl className={styles.account}>
        <dt>{strings.name}</dt>
        <dd>{account.displayName}</dd>
        <dt>{strings.emailAddress}</dt>
        <dd>{account.email}</dd>
      </dl>
      <p className={styles.hint}>{strings.setup.accountHint}</p>
      <h2 className={styles.choose}>{strings.setup.chooseMethod}</h2>
      {supported ? (
        <>
          <RequestAlert>{failure}</RequestAlert>
          <Button
            variant="primary"
            busy={busy}
            busyLabel={strings.setup.settingUp}
            aria-describedby={passkeyHintId}
            onClick={() => void setUp()}
          >
            {strings.setup.passkey}
          </Button>
          <p id={passkeyHintId} className={styles.hint}>{strings.setup.passkeyHint}</p>
        </>
      ) : (
        // Plain text, read in order: it says how things are when the view shows, so nothing announces it.
        <div className={styles.notice}>
          <p className={styles.noticeHeading}>{strings.setup.inAppBrowser.heading}</p>
          <p>{strings.setup.inAppBrowser.body}</p>
        </div>
      )}
      <p className={styles.or}>{strings.or}</p>
      <Link
        to="/setup/password"
        className={buttonClass(supported ? 'secondary' : 'primary')}
        aria-describedby={passwordHintId}
      >
        {strings.setup.password}
      </Link>
      <p id={passwordHintId} className={styles.hint}>{strings.setup.passwordHint}</p>
    </div>
  );
}

/** The alert's words for a press that failed, or `undefined` for a `401`. */
function alertFor(error: unknown): string | undefined {
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
