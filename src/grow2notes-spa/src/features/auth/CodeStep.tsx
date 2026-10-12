import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api } from '../../api/api.ts';
import { setMe, type Me } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { ErrorSummary } from '../../components/ErrorSummary.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { useErrorTitle } from '../../components/useErrorTitle.ts';
import { strings } from '../../copy/strings.ts';
import { codeFormatError, normaliseCode } from './code.ts';
import { OneTimeCodeField } from './OneTimeCodeField.tsx';
import styles from './SignIn.module.css';
import { isRefusal, notSignedIn } from './signInFailure.ts';

// The field's id and name, by which a password manager finds it, so they stay the same across releases.
const codeId = 'code';

// The code step lasts 5 minutes from the accepted password (D69). A refusal this long after it is taken as the step's
// end, with 10 seconds' margin for a code sent just before it (sign-in.md *Conflicts resolved* 12).
const codeStepEndsAfterMs = 4 * 60_000 + 50_000;

type CodeStepProps = {
  /** When the password was accepted, by `Date.now()`, from which the code step's 5 minutes run. */
  passwordAcceptedAt: number;
  /** For a refused code once the step has run out: view A shows again, with the A25 message. */
  onExpired: () => void;
};

/**
 * Sign-in's view B, the code step (sign-in.md view B, components 2, 2a, 10 and 11; totp-setup.md), which `SignIn`
 * shows in view A's place, at the same address and with the same title, once the password is accepted. Its `<h1>` is
 * the code field's label, and the field takes focus as it shows. Sign in checks the code before it sends anything,
 * and a badly formed one shows its message on the field, which takes focus again; nothing is ever sent by itself. A
 * code that the server refuses shows the A25 message in the summary, which takes focus, and a request that got no
 * answer says so above Sign in, where focus stays for another press.
 */
export function CodeStep({ passwordAcceptedAt, onExpired }: CodeStepProps) {
  const queryClient = useQueryClient();
  // As typed, in this view's memory only (sign-in.md *Privacy and storage*).
  const [code, setCode] = useState('');
  const [formatError, setFormatError] = useState<string>();
  // The number of presses with a badly formed code, so that each moves focus to the field, even with the same message.
  const [formatErrors, setFormatErrors] = useState(0);
  const [busy, setBusy] = useState(false);
  // The A25 message, after a refused code, and the number of refusals, so that the summary takes focus on each.
  const [refused, setRefused] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [failure, setFailure] = useState<string>();
  // The field's message shows on it only, with no summary (form-validation.md rule 6), so the title says so here.
  useErrorTitle(formatError !== undefined);
  // False once the view has gone, after which an answer that comes changes nothing.
  const mounted = useRef(false);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  // As the step shows, as the person pressed Continue, and after each badly formed code, once the field's description
  // names its message, so that a screen reader reads it with focus (sign-in.md *Where focus goes*).
  useEffect(() => {
    document.getElementById(codeId)?.focus();
  }, [formatErrors]);

  // Sign in, pressed or reached by Enter in the field. While the code is out, the busy button stops another.
  async function signIn(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setRefused(false);
    // Emptied, so that a failure that repeats is written into an empty region and announced again.
    setFailure(undefined);
    const wrongFormat = codeFormatError(code);
    setFormatError(wrongFormat);
    if (wrongFormat !== undefined) {
      setFormatErrors((count) => count + 1);
      return;
    }

    setBusy(true);
    try {
      // A direct call, not a mutation, so that the code never sits in TanStack's mutation cache, and one request per
      // press, never retried, as each wrong code counts towards the lockout (sign-in.md *Interactions* 4).
      const me = await api<Me>('/api/auth/login/totp', { method: 'POST', body: { code: normaliseCode(code) } });
      await setMe(queryClient, me);
    } catch (error) {
      if (!mounted.current) {
        return;
      }

      setBusy(false);
      if (!isRefusal(error)) {
        setFailure(notSignedIn(error));
      } else if (Date.now() - passwordAcceptedAt >= codeStepEndsAfterMs) {
        // Every code now gets the same refusal, so the person starts again (sign-in.md *Code step expired*).
        onExpired();
      } else {
        // No longer valid, so emptied (form-validation.md rule 5), and marked on no field, which would say which part
        // was wrong (A25).
        setCode('');
        setRefused(true);
        setAttempt((count) => count + 1);
      }
    }
  }

  return (
    <>
      <ErrorSummary errors={refused ? [{ message: strings.signIn.failed }] : []} attempt={attempt} />
      <form noValidate className={styles.form} onSubmit={(event) => void signIn(event)}>
        <div className={styles.fields}>
          <OneTimeCodeField
            id={codeId}
            label={strings.oneTimeCode.label}
            labelIsHeading
            hint={strings.signIn.codeHint}
            error={formatError}
            value={code}
            onChange={(typed) => {
              setCode(typed);
              // Once a message shows, each change checks again, and the message goes as soon as the code reads as six
              // digits (form-validation.md rule 2).
              if (formatError !== undefined) {
                setFormatError(codeFormatError(typed));
              }
            }}
          />
        </div>
        {/* Empty until a request fails, directly above the button that sent it (component 2a). */}
        <RequestAlert>{failure}</RequestAlert>
        {/* Directly under the field, as a phone's number pad may have no Return key (totp-setup.md). */}
        <Button type="submit" variant="primary" busy={busy} busyLabel={strings.signIn.busy}>
          {strings.signIn.signIn}
        </Button>
      </form>
    </>
  );
}
