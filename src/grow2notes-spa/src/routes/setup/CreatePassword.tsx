import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { focusPageHeading } from '../../components/focusPageHeading.ts';
import { PageBar } from '../../components/PageBar.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { useErrorTitle } from '../../components/useErrorTitle.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { PasswordInput } from '../../features/auth/PasswordInput.tsx';
import { limits } from '../../limits.ts';
import styles from './CreatePassword.module.css';
import { NoLinkMessage } from './SetupMessage.tsx';
import { useSetup, type SetupAccount } from './setupContext.ts';

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
 * `/setup/password`: view F once the link has named the account, and the no-link message with nothing in memory, as
 * after a refresh (sign-in.md *Setup*).
 */
export function CreatePassword() {
  const { account } = useSetup();
  return <PageStatus>{account === undefined ? <NoLinkMessage /> : <PasswordForm account={account} />}</PageStatus>;
}

/**
 * View F (sign-in.md, its *Setup* states and *Interactions* 6, 10 and 14): one new password, checked here and sent
 * nowhere, which Continue puts in setup's memory for view G to send with the code. A hidden `username` field holding
 * the email tells a password manager whose password it is. Its back link to view E pops, as F's entry always follows
 * E's while setup's memory lives, and the `h1` takes focus as F shows, as the shell's focus on a new screen is
 * S00.05.02's.
 */
function PasswordForm({ account }: { account: SetupAccount }) {
  const { password: held, setPassword } = useSetup();
  const navigate = useNavigate();
  // The password in memory when the person comes back from view G.
  const [password, setValue] = useState(held ?? '');
  const [error, setError] = useState<string>();
  // The number of refused presses, so that each moves focus to the field, even with the same message.
  const [attempt, setAttempt] = useState(0);
  useErrorTitle(error !== undefined);

  useEffect(() => {
    focusPageHeading();
  }, []);

  // After the render that names the message in the field's description, so that a screen reader reads it with focus.
  useEffect(() => {
    if (attempt > 0) {
      document.getElementById(fieldId)?.focus();
    }
  }, [attempt]);

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const found = passwordError(password);
    setError(found);
    if (found !== undefined) {
      setAttempt((count) => count + 1);
      return;
    }

    setPassword(password);
    // Pushed, so that Back on view G returns here.
    void navigate('/setup/app');
  }

  return (
    <div className={styles.view}>
      <PageBar>
        <BackLink to="/setup" previousIsDestination>
          {pageName.setUpYourAccount}
        </BackLink>
      </PageBar>
      <form noValidate onSubmit={onSubmit}>
        <PasswordInput
          id={fieldId}
          label={strings.setup.createPassword}
          labelIsHeading
          hint={msg.passwordHint(limits.passwordMinLength)}
          error={error}
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
          }}
        />
        <input type="email" name="username" autoComplete="username" value={account.email} readOnly hidden />
        <Button type="submit" variant="primary" className={styles.continue}>
          {strings.continue}
        </Button>
      </form>
    </div>
  );
}
