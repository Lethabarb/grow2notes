import { startRegistration, type RegistrationResponseJSON } from '@simplewebauthn/browser';
import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useId, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { api, ApiError, classify, signInTimeoutMs } from '../../api/api.ts';
import { setMe, type Me } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { useErrorTitle } from '../../components/useErrorTitle.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { cancelPasskey, useOneOptions } from '../../features/auth/passkey.ts';
import { PasswordInput } from '../../features/auth/PasswordInput.tsx';
import { limits } from '../../limits.ts';
import styles from './CreatePassword.module.css';
import { NoLinkMessage, SetupMessage } from './SetupMessage.tsx';
import { useSetup, type SetupAccount } from './setupContext.ts';
import { creationOptions, passkeyAlertFor, recordPasskeyFailure } from './setupPasskey.ts';

const fieldId = 'password';

/**
 * What is wrong with a password, by Identity's rule as design.md §8.4 sets it, or nothing. A password of only spaces
 * is empty, as `string.IsNullOrWhiteSpace` finds it on the server, and its length is counted as `RequiredLength`
 * counts it, in UTF-16 units.
 */
function passwordError(password: string): string | undefined {
  if (password.trim() === '') {
    return strings.setup.enterPassword;
  }

  return password.length < limits.passwordMinLength ? msg.passwordTooShort(limits.passwordMinLength) : undefined;
}

/**
 * `/setup/password`: view F once the link has named the account, the timed-out page once a setup call has answered
 * `401`, and the no-link message with nothing in memory, as after a refresh (sign-in.md *Setup*).
 */
export function CreatePassword() {
  const { account, drop } = useSetup();
  const [timedOut, setTimedOut] = useState(false);

  // A 401 from a setup call: the enrolment cookie has ended, and setup's memory goes with it (sign-in.md *Setup*).
  const timeOut = useCallback(() => {
    drop();
    setTimedOut(true);
  }, [drop]);

  let content: ReactNode;
  if (timedOut) {
    content = <SetupMessage {...strings.setup.timedOut} />;
  } else if (account === undefined) {
    content = <NoLinkMessage />;
  } else {
    content = <PasswordForm account={account} onTimedOut={timeOut} />;
  }

  return <PageStatus>{content}</PageStatus>;
}

type PasswordFormProps = {
  account: SetupAccount;
  /** Called for a `401` from a setup call, as the enrolment cookie has ended. The same function on every render. */
  onTimedOut: () => void;
};

/**
 * View F (sign-in.md, its *Setup* states and *Interactions* 6, 10, 14 and 16): one new password, checked here first.
 * For a password and an authenticator app, nothing is sent: Continue puts the password in setup's memory for view G
 * to send with the code. For a password only, Finish setup sends it, which finishes setup and signs the person in. For
 * a password and a passkey, Finish setup opens the device's passkey prompt straight from the press, with creation
 * options fetched as F showed, then sends the password with the credential in one request; a failure keeps the
 * password, so that the person tries again without retyping, with no half-made account. A hidden `username` field
 * holding the email tells a password manager whose password it is. Its back link to view E pops, as view E opened F,
 * and the shell focuses the `h1` as F shows. When view G has come back because the server refused the password, F
 * shows the message for it on the field instead, which then carries `data-route-focus`, as a one-field form's error
 * summary, so that the shell focuses it as F shows (*Password rejected by server*).
 */
function PasswordForm({ account, onTimedOut }: PasswordFormProps) {
  const { password: held, setPassword, passwordRefused, passwordChoice, finish } = useSetup();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const hintId = useId();
  const withApp = passwordChoice === 'authenticatorApp';
  const withPasskey = passwordChoice === 'passwordAndPasskey';
  const options = useOneOptions(creationOptions);
  // The password in memory when the person comes back from view G.
  const [password, setValue] = useState(held ?? '');
  // With design.md §8.4's options, Identity's one refusal is a password too short, worded as F's own check words it.
  const [error, setError] = useState(() =>
    passwordRefused ? msg.passwordTooShort(limits.passwordMinLength) : undefined,
  );
  // The number of presses that F's own check refused, so that each moves focus to the field, even with the same
  // message.
  const [attempt, setAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  // The alert's words while it holds a failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  useErrorTitle(error !== undefined || failure !== undefined);
  // False once the view has gone, after which nothing that ends, its prompt among them, says or fetches anything.
  const mounted = useRef(false);

  // Options for the next press. A failure says nothing, as the press asks again, but for a `401`: the enrolment cookie
  // has ended, and no press can get past it.
  const prefetch = useCallback(() => {
    options.prefetch().catch((failed: unknown) => {
      if (mounted.current && classify(failed) === 'signedOut') {
        onTimedOut();
      }
    });
  }, [options, onTimedOut]);

  useEffect(() => {
    mounted.current = true;
    if (withPasskey) {
      prefetch();
    }

    return () => {
      mounted.current = false;
      if (withPasskey) {
        cancelPasskey();
      }
    };
  }, [withPasskey, prefetch]);

  // After the render that names the message in the field's description, so that a screen reader reads it with focus.
  useEffect(() => {
    if (attempt > 0) {
      document.getElementById(fieldId)?.focus();
    }
  }, [attempt]);

  function refuseLength() {
    setError(msg.passwordTooShort(limits.passwordMinLength));
    setAttempt((count) => count + 1);
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const found = passwordError(password);
    setError(found);
    if (found !== undefined) {
      setAttempt((count) => count + 1);
      return;
    }

    if (withApp) {
      setPassword(password);
      // Pushed, so that Back on view G returns here.
      void navigate('/setup/app');
      return;
    }

    void finishSetup();
  }

  // Everything before its first await runs in the press itself.
  async function finishSetup() {
    const fresh = withPasskey ? options.take() : undefined;
    // With fresh options, the prompt opens straight from the press, with no await before it, as older iPhones refuse
    // a prompt that an awaited fetch has parted from its tap (sign-in.md *Interactions* 2 and 16).
    const started = fresh === undefined ? undefined : startRegistration({ optionsJSON: fresh });
    // Set at once, so that a second submit before the next render finds the button busy and sends nothing.
    setBusy(true);
    setFailure(undefined);

    try {
      // Direct calls, not mutations, so that the password never sits in TanStack's mutation cache.
      let me: Me;
      if (withPasskey) {
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
        me = await api<Me>('/api/auth/setup/password-and-passkey', {
          method: 'POST',
          body: { password, credentialJson: JSON.stringify(credential) },
          timeoutMs: signInTimeoutMs,
        });
      } else {
        me = await api<Me>('/api/auth/setup/password-only', {
          method: 'POST',
          body: { password },
          timeoutMs: signInTimeoutMs,
        });
      }

      // Into `['me']` before the page goes to `/`, so that Today shows at once for the person now signed in, not what
      // the shell's `/me` got before setup, a `401` and so sign-in, until the new location's refresh answers
      // (sign-in.md *Interactions* 12).
      await setMe(queryClient, me);
      finish();
      // Back to `/setup`'s entry, one before F's, which the setup layout replaces with Today's.
      if (mounted.current) {
        void navigate(-1);
      }
    } catch (failed) {
      // The view has gone, and its prompt with it.
      if (!mounted.current) {
        return;
      }

      if (withPasskey) {
        recordPasskeyFailure(failed);
      }

      setBusy(false);
      answerFailure(failed);
    }
  }

  function answerFailure(failed: unknown) {
    if (failed instanceof ApiError && failed.errors?.password !== undefined) {
      refuseLength();
      return;
    }

    // For a password only, a prompt error and a `422` for a credential cannot come, so only the rest of it applies.
    const words = passkeyAlertFor(failed);
    if (words === undefined) {
      onTimedOut();
      return;
    }

    // Focus stays on the button, so that pressing it again tries again, with new options for a prompt at once.
    setFailure(words);
    if (withPasskey) {
      prefetch();
    }
  }

  return (
    <div className={styles.view}>
      <PageBar>
        <BackLink to="/setup">{pageName.setUpYourAccount}</BackLink>
      </PageBar>
      <form noValidate onSubmit={onSubmit}>
        <PasswordInput
          id={fieldId}
          label={strings.setup.createPassword}
          labelIsHeading
          hint={msg.passwordHint(limits.passwordMinLength)}
          error={error}
          routeFocus={passwordRefused}
          autoComplete="new-password"
          minLength={limits.passwordMinLength}
          toggle
          value={password}
          onChange={(next) => {
            setValue(next);
            // Once a message shows, each change checks again, and the message goes as soon as the password passes
            // (form-validation.md, rule 2).
            if (error !== undefined) {
              setError(passwordError(next));
            }

            // So that the options are fresh at the press, after however long typing takes. Not while a press is on
            // its way, whose ceremony new options would replace.
            if (withPasskey && !busy && options.take() === undefined) {
              prefetch();
            }
          }}
        />
        <input type="email" name="username" autoComplete="username" value={account.email} readOnly hidden />
        {withPasskey && <p id={hintId} className={styles.hint}>{strings.setup.passkeyNext}</p>}
        {/* Empty until a request fails, directly above the button that sent it (component 2a); Continue sends none. */}
        {!withApp && <RequestAlert>{failure}</RequestAlert>}
        <Button
          type="submit"
          variant="primary"
          className={styles.continue}
          busy={busy}
          busyLabel={strings.setup.finishingSetup}
          aria-describedby={withPasskey ? hintId : undefined}
        >
          {withApp ? strings.continue : strings.setup.finishSetup}
        </Button>
      </form>
    </div>
  );
}
