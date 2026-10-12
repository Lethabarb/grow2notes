import {
  browserSupportsWebAuthn,
  startAuthentication,
  type AuthenticationResponseJSON,
  type PublicKeyCredentialRequestOptionsJSON,
} from '@simplewebauthn/browser';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import textInput from '../../components/textInput.module.css';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { CodeStep } from './CodeStep.tsx';
import { cancelPasskey, useOneOptions } from './passkey.ts';
import { PasswordInput } from './PasswordInput.tsx';
import styles from './SignIn.module.css';
import { isRefusal, notSignedIn } from './signInFailure.ts';

// Identity's options JSON, which startAuthentication takes as it comes (design.md §6.2).
const requestOptions = () =>
  api<PublicKeyCredentialRequestOptionsJSON>('/api/auth/passkey/options', { method: 'POST' });

// The fields' ids and names, by which password managers find them, so they stay the same across releases.
const emailId = 'email';
const passwordId = 'password';

/** The form's messages, one for each field in error. */
type FieldErrors = { email?: string; password?: string };

/**
 * What is wrong with the email, trimmed, as it is sent, or nothing: it is empty, or it is not text, one "@" and text,
 * form-validation.md's loose check, which leaves the rest to the server.
 */
function emailError(email: string): string | undefined {
  if (email === '') {
    return strings.signIn.enterEmail;
  }

  return /^[^@]+@[^@]+$/.test(email) ? undefined : strings.signIn.emailFormat;
}

/**
 * What is wrong with the password, or nothing. It is never trimmed, but one of only whitespace is empty, as the
 * server's `[Required]` finds it, which would refuse it with a `422` that no field here shows.
 */
function passwordError(password: string): string | undefined {
  return password.trim() === '' ? strings.signIn.enterPassword : undefined;
}

/**
 * The summary's items: the A25 message, with no link, as it marks no field (sign-in.md component 2), then each field's
 * message, linked to its field, in the fields' order.
 */
function summaryItems(failed: boolean, errors: FieldErrors): FormError[] {
  const items: FormError[] = failed ? [{ message: strings.signIn.failed }] : [];
  if (errors.email !== undefined) {
    items.push({ message: errors.email, fieldId: emailId });
  }

  if (errors.password !== undefined) {
    items.push({ message: errors.password, fieldId: passwordId });
  }

  return items;
}

// A cancel, a timeout or no passkey for the app on the device, which the browser makes look the same (WebAuthn §14.5).
// The prompt has said all there is to say, so the view says nothing (sign-in.md *Passkey prompt dismissed*).
function isDismissal(error: unknown): boolean {
  return error instanceof Error && error.name === 'NotAllowedError';
}

/**
 * Sign-in's views A and B (sign-in.md components 1 to 11 and 18), which the shell shows in `<main>` at the address
 * that was opened when nobody is signed in, and titles "Grow2Notes – Sign in". Nothing takes focus as A renders: an
 * `autoFocus` would open a phone's keyboard over the passkey button.
 *
 * A fetches the passkey options as it renders, so that a press can open the prompt at once (sign-in.md *Interactions*
 * step 2). A sign-in puts its `Me` in the `['me']` query, and the shell then shows the app in its place, at the same
 * address. Under the passkey button, its own form takes an email address and a password, which Continue checks before
 * it sends anything (*Interactions* step 3). Once the password is accepted, the code step, view B, shows in A's place
 * (step 4), until it signs the person in, or a refusal comes after its 5 minutes, which shows A again. A form with a
 * message to fix, or a sign-in that the server refuses, shows the summary, which takes focus; a request that got no
 * answer, or a prompt that failed, says so above the button that was pressed, where focus stays for another press.
 *
 * Identity keeps a passkey ceremony in the cookie that keeps the code step (S00.04.05's Notes), so the answer to a
 * request for passkey options must never come after a password is accepted: Continue sends the password once no such
 * request is on its way, none starts while the password is out, and none while B shows.
 */
export function SignIn() {
  const queryClient = useQueryClient();
  const supported = browserSupportsWebAuthn();
  const options = useOneOptions(requestOptions);
  const [passkeyBusy, setPasskeyBusy] = useState(false);
  const [passkeyFailure, setPasskeyFailure] = useState<string>();
  // The A25 message, after a refused sign-in by either way in.
  const [failed, setFailed] = useState(false);
  // The number of failed presses, so that the summary takes focus on each, even with the same words.
  const [attempt, setAttempt] = useState(0);
  // As typed, in this view's memory only (sign-in.md *Privacy and storage*).
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [checking, setChecking] = useState(false);
  const [passwordFailure, setPasswordFailure] = useState<string>();
  // When the password was accepted, in memory only, while the code step shows in A's place.
  const [passwordAcceptedAt, setPasswordAcceptedAt] = useState<number>();
  // The same, for what ends after an await: whether the code step shows.
  const codeStepShows = useRef(false);
  // The password step, from Continue's press until its answer has been shown.
  const passwordOut = useRef<Promise<void>>(undefined);
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

  /**
   * New passkey options, asked for once no password is out and only while A shows, or nothing once the view has gone
   * or the code step shows. The check and the request come together, with no await between them, so that no password
   * goes out in between.
   */
  async function optionsWhileAShows(): Promise<PublicKeyCredentialRequestOptionsJSON | undefined> {
    while (passwordOut.current !== undefined) {
      await passwordOut.current;
    }

    return mounted.current && !codeStepShows.current ? options.prefetch() : undefined;
  }

  // Everything before its first await runs in the press itself.
  async function signIn() {
    const fresh = options.take();
    // With fresh options, the prompt opens straight from the press, with no await before it, as older iPhones refuse
    // a prompt that an awaited fetch has parted from its tap (sign-in.md *Interactions* step 2).
    const started = fresh === undefined ? undefined : startAuthentication({ optionsJSON: fresh });
    setPasskeyBusy(true);
    setFailed(false);
    // Emptied, so that a failure that repeats is written into an empty region and announced again.
    setPasskeyFailure(undefined);

    try {
      let credential: AuthenticationResponseJSON;
      if (started === undefined) {
        const optionsJSON = await optionsWhileAShows();
        if (!mounted.current) {
          return;
        }

        if (optionsJSON === undefined) {
          // The password was accepted while this press waited for its answer, and the code step now shows.
          setPasskeyBusy(false);
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

      setPasskeyBusy(false);
      // The code step cancelled the prompt as it showed, or now holds the cookie that the sign-in needed: this attempt
      // ends with no word, and asks for no options.
      if (codeStepShows.current) {
        return;
      }

      if (isRefusal(error)) {
        setFailed(true);
        setAttempt((count) => count + 1);
      } else if (!isDismissal(error)) {
        setPasskeyFailure(notSignedIn(error));
      }

      // The server may have ended the ceremony, and the next press should still open the prompt at once.
      optionsWhileAShows().catch(() => {});
    }
  }

  // Continue, pressed or reached by Enter in either field. While a request is out, the busy button stops another.
  function continueWithPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const sentEmail = email.trim();
    const errors = { email: emailError(sentEmail), password: passwordError(password) };
    setFieldErrors(errors);
    setFailed(false);
    // Emptied, so that a failure that repeats is written into an empty region and announced again.
    setPasswordFailure(undefined);
    if (errors.email !== undefined || errors.password !== undefined) {
      setAttempt((count) => count + 1);
      return;
    }

    setChecking(true);
    passwordOut.current = sendPassword(sentEmail).finally(() => {
      passwordOut.current = undefined;
    });
  }

  // The password step, sent once no request for passkey options is on its way, whose answer it then shows.
  async function sendPassword(sentEmail: string) {
    await options.idle();
    if (!mounted.current) {
      return;
    }

    try {
      // A direct call, not a mutation, so that the password never sits in TanStack's mutation cache.
      await api<{ next: 'totp' }>('/api/auth/login', { method: 'POST', body: { email: sentEmail, password } });
    } catch (error) {
      if (!mounted.current) {
        return;
      }

      setChecking(false);
      if (isRefusal(error)) {
        // Emptied, with the email kept, as GOV.UK's password input has it (sign-in.md *Conflicts resolved* 1).
        setPassword('');
        setFailed(true);
        setAttempt((count) => count + 1);
      } else {
        setPasswordFailure(notSignedIn(error));
      }

      return;
    }

    if (!mounted.current) {
      return;
    }

    // The code step's 5 minutes run from here (D69). A prompt still open would sign a ceremony that the step has
    // ended, so it closes, and what A showed goes with A, the password among it, which nothing needs again.
    codeStepShows.current = true;
    cancelPasskey();
    setChecking(false);
    setPassword('');
    setPasskeyFailure(undefined);
    setPasswordAcceptedAt(Date.now());
  }

  // A refused code once the code step has run out: A again, with the email kept and the password to type again.
  function codeStepExpired() {
    codeStepShows.current = false;
    setPasswordAcceptedAt(undefined);
    setFailed(true);
    setAttempt((count) => count + 1);
    if (supported) {
      // As on A's first render: the code step has ended the ceremony of any options held.
      options.prefetch().catch(() => {});
    }
  }

  return (
    <PageStatus>
      <div className={styles.view}>
        {passwordAcceptedAt === undefined ? (
          <>
            <ErrorSummary errors={summaryItems(failed, fieldErrors)} attempt={attempt} />
            <PageHeading>{pageName.signIn}</PageHeading>
            <p className={styles.hint}>{strings.signIn.hint}</p>
            {supported ? (
              <>
                <div className={styles.passkey}>
                  {/* Empty until a request fails, directly above the button that sent it (component 2a). */}
                  <RequestAlert>{passkeyFailure}</RequestAlert>
                  <Button
                    variant="primary"
                    busy={passkeyBusy}
                    busyLabel={strings.signIn.busy}
                    onClick={() => void signIn()}
                  >
                    {strings.signIn.passkey}
                  </Button>
                </div>
                <p className={styles.or}>{strings.or}</p>
              </>
            ) : (
              <p className={styles.passkey}>{strings.signIn.passkeysUnavailable}</p>
            )}
            {/* The passkey button is outside it, so that Enter in a field sends the form, never the prompt. */}
            <form noValidate className={styles.form} onSubmit={continueWithPassword}>
              <div className={styles.fields}>
                <FieldGroup fieldId={emailId} label={strings.emailAddress} error={fieldErrors.email}>
                  <input
                    id={emailId}
                    name={emailId}
                    type="email"
                    className={textInput.input}
                    autoComplete="username"
                    spellCheck={false}
                    autoCapitalize="none"
                    autoCorrect="off"
                    aria-describedby={fieldErrors.email === undefined ? undefined : `${emailId}-error`}
                    aria-invalid={fieldErrors.email === undefined ? undefined : true}
                    value={email}
                    onChange={(event) => {
                      const typed = event.currentTarget.value;
                      setEmail(typed);
                      // Once a message shows, each change checks again, and the message goes as soon as the field
                      // passes (form-validation.md rule 2).
                      if (fieldErrors.email !== undefined) {
                        setFieldErrors((shown) => ({ ...shown, email: emailError(typed.trim()) }));
                      }
                    }}
                  />
                </FieldGroup>
                <PasswordInput
                  id={passwordId}
                  label={strings.signIn.password}
                  error={fieldErrors.password}
                  autoComplete="current-password"
                  value={password}
                  onChange={(typed) => {
                    setPassword(typed);
                    if (fieldErrors.password !== undefined) {
                      setFieldErrors((shown) => ({ ...shown, password: passwordError(typed) }));
                    }
                  }}
                />
              </div>
              <RequestAlert>{passwordFailure}</RequestAlert>
              <Button type="submit" busy={checking} busyLabel={strings.checking}>
                {strings.continue}
              </Button>
            </form>
          </>
        ) : (
          <CodeStep passwordAcceptedAt={passwordAcceptedAt} onExpired={codeStepExpired} />
        )}
        <p className={styles.help}>{strings.signIn.help}</p>
      </div>
    </PageStatus>
  );
}
