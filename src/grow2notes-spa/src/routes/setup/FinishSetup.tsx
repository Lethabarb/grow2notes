import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { api, ApiError, classify } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { useErrorTitle } from '../../components/useErrorTitle.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { codeFormatError, normaliseCode } from '../../features/auth/code.ts';
import { OneTimeCodeField } from '../../features/auth/OneTimeCodeField.tsx';
import styles from './FinishSetup.module.css';
import { useSetup } from './setupContext.ts';

const fieldId = 'code';

type FinishSetupProps = {
  /** The password that view F took, which goes with the code. */
  password: string;
  /** Called for a `401`, as the enrolment cookie has ended. */
  onTimedOut: () => void;
};

/**
 * View G's code step (sign-in.md view G, components 2a, 10 and 17, *Interactions* 4 and 12; totp-setup.md): the code
 * from the person's authenticator app, checked here first, so that a badly formed one is never sent, then sent with
 * the password in one `POST /api/auth/setup/password`, which finishes setup and signs the person in. A refused code
 * shows on the field, which takes focus, and a request that failed shows in the alert above Finish setup, where
 * focus stays for another press. The field takes no focus as G shows: it is at the bottom of the steps, which the
 * person reads first.
 */
export function FinishSetup({ password, onTimedOut }: FinishSetupProps) {
  const { refusePassword, finish } = useSetup();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const words = strings.setup.authenticatorApp;
  const [code, setCode] = useState('');
  const [error, setError] = useState<string>();
  // The number of refused codes, so that each moves focus to the field, even with the same message.
  const [attempt, setAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  // The alert's words while it holds a failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  useErrorTitle(error !== undefined || failure !== undefined);
  // False once the view has gone, after which an answer that comes changes nothing on the page that is now shown.
  const mounted = useRef(false);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  // After the render that names the message in the field's description, so that a screen reader reads it with focus.
  useEffect(() => {
    if (attempt > 0) {
      document.getElementById(fieldId)?.focus();
    }
  }, [attempt]);

  function refuse(message: string) {
    setError(message);
    setAttempt((count) => count + 1);
  }

  async function finishSetup(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied, so that a failure that repeats is written into an empty region and announced again.
    setFailure(undefined);
    const formatError = codeFormatError(code);
    if (formatError !== undefined) {
      refuse(formatError);
      return;
    }

    setError(undefined);
    setBusy(true);
    try {
      // A direct call, not a mutation, so that the password never sits in TanStack's mutation cache.
      const me = await api<Me>('/api/auth/setup/password', {
        method: 'POST',
        body: { password, totpCode: normaliseCode(code) },
      });
      // The shell keeps the `401` that its `/me` got before setup and never asks again by itself, so it is given the
      // signed-in user, which Today then shows with no request of its own (sign-in.md *Interactions* 12).
      queryClient.setQueryData(meQuery.queryKey, me);
      finish();
      // Back to `/setup`'s entry, two before G's, which the setup layout replaces with Today's.
      if (mounted.current) {
        void navigate(-2);
      }
    } catch (failed) {
      if (!mounted.current) {
        return;
      }

      setBusy(false);
      answerFailure(failed);
    }
  }

  function answerFailure(failed: unknown) {
    const fields = failed instanceof ApiError ? failed.errors : undefined;
    if (fields?.totpCode !== undefined) {
      // No longer valid, so cleared (form-validation.md rule 5); the password stays for a new code.
      setCode('');
      refuse(words.codeRejected);
    } else if (fields?.password !== undefined) {
      // Back to view F, whose entry is the one before G's, to show it there.
      refusePassword();
      void navigate(-1);
    } else if (failed instanceof ApiError && failed.status === 429) {
      setFailure(strings.setup.tooManyTries);
    } else {
      // A 401 from a setup call means that its enrolment cookie has ended.
      const cause = classify(failed);
      if (cause === 'signedOut') {
        onTimedOut();
      } else {
        setFailure(msg.notSetUp(cause));
      }
    }
  }

  return (
    <form noValidate onSubmit={(event) => void finishSetup(event)}>
      <OneTimeCodeField
        id={fieldId}
        label={strings.oneTimeCode.label}
        hint={words.codeHint}
        error={error}
        value={code}
        onChange={(typed) => {
          setCode(typed);
          // Once a message shows, each change checks again, and the message goes as soon as the code reads as six
          // digits (form-validation.md, rule 2).
          if (error !== undefined) {
            setError(codeFormatError(typed));
          }
        }}
      />
      <div className={styles.actions}>
        {/* Empty until a request fails, directly above the button that sent it (component 2a). */}
        <RequestAlert>{failure}</RequestAlert>
        <Button type="submit" variant="primary" busy={busy} busyLabel={words.finishingSetup}>
          {words.finishSetup}
        </Button>
      </div>
    </form>
  );
}
