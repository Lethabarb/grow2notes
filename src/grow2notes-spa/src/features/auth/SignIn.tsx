import { browserSupportsWebAuthn } from '@simplewebauthn/browser';
import { Button } from '../../components/Button.tsx';
import { ErrorSummary } from '../../components/ErrorSummary.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import styles from './SignIn.module.css';

/**
 * Sign-in's view A, its passkey part (sign-in.md components 1 to 4, 9 and 18), which the shell shows in `<main>` at
 * the address that was opened when nobody is signed in, and titles "Grow2Notes – Sign in". Nothing takes focus as it
 * renders: an `autoFocus` would open a phone's keyboard over the passkey button.
 */
export function SignIn() {
  return (
    <PageStatus>
      <div className={styles.view}>
        <ErrorSummary errors={[]} attempt={0} />
        <PageHeading>{pageName.signIn}</PageHeading>
        <p className={styles.hint}>{strings.signIn.hint}</p>
        {browserSupportsWebAuthn() ? (
          <div className={styles.passkey}>
            {/* Empty until a request fails, directly above the button that sent it (component 2a). */}
            <RequestAlert />
            <Button variant="primary">{strings.signIn.passkey}</Button>
          </div>
        ) : (
          <p className={styles.passkey}>{strings.signIn.passkeysUnavailable}</p>
        )}
        <p className={styles.help}>{strings.signIn.help}</p>
      </div>
    </PageStatus>
  );
}
