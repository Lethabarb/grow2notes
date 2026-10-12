import { queryOptions, useQuery } from '@tanstack/react-query';
import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { api, classify } from '../../api/api.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { focusPageHeading } from '../../components/focusPageHeading.ts';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { strings } from '../../copy/strings.ts';
import styles from './AuthenticatorSetup.module.css';
import { NoLinkMessage, SetupMessage } from './SetupMessage.tsx';
import { SetupKey } from './SetupKey.tsx';
import { useSetup } from './setupContext.ts';

/**
 * The setup key, as `GET /api/auth/setup/authenticator` answers it (design.md §6.2), written by hand (D71), with its
 * `authenticatorUri` left out, as no screen shows it.
 */
type SetupAuthenticator = { sharedKey: string };

// The same key in a QR code, drawn by the server, which no QR service ever sees (totp-setup.md).
const qrCodePath = '/api/auth/setup/authenticator/qr.png';

// Fetched as view G shows, and held only while it shows (sign-in.md *Privacy and storage*): `gcTime: 0` drops the key
// once G has gone, so that G fetches it again when it shows again. The key does not change during setup, so nothing
// fetches it again while G shows, not even the person's return from their authenticator app, and a failure shows at
// once, with Try again.
const setupKeyQuery = queryOptions({
  queryKey: ['setup', 'authenticator'],
  queryFn: ({ signal }) => api<SetupAuthenticator>('/api/auth/setup/authenticator', { signal }),
  staleTime: Infinity,
  gcTime: 0,
  refetchOnWindowFocus: false,
  retry: false,
});

/**
 * `/setup/app`: view G once setup holds the account and the password that view F took, the timed-out page once a
 * setup call has answered `401`, and the no-link message with nothing in memory, as after a refresh (sign-in.md
 * *Setup*).
 */
export function AuthenticatorSetup() {
  const { account, password, drop } = useSetup();
  const [timedOut, setTimedOut] = useState(false);

  // A 401 from a setup call: the enrolment cookie has ended, and setup's memory goes with it (sign-in.md *Setup*).
  const timeOut = useCallback(() => {
    drop();
    setTimedOut(true);
  }, [drop]);

  let content: ReactNode;
  if (timedOut) {
    content = <SetupMessage {...strings.setup.timedOut} />;
  } else if (account === undefined || password === undefined) {
    content = <NoLinkMessage />;
  } else {
    content = <AuthenticatorApp onTimedOut={timeOut} />;
  }

  return <PageStatus>{content}</PageStatus>;
}

/**
 * View G's setup block (sign-in.md view G and component 16; totp-setup.md): what an authenticator app is, and the
 * steps that add Grow2Notes to one, with the QR code to scan and the setup key to type, both shown on every screen, as
 * a person setting up on their phone cannot scan its own screen. Its back link to view F pops, as G's entry always
 * follows F's while setup's memory lives, and the `h1` takes focus as G shows, as the shell's focus on a new screen is
 * S00.05.02's.
 */
function AuthenticatorApp({ onTimedOut }: { onTimedOut: () => void }) {
  const key = useQuery(setupKeyQuery);
  const [qrFailed, setQrFailed] = useState(false);
  // The key region says nothing for a 401, which is the page's to say.
  const signedOut = key.error !== null && classify(key.error) === 'signedOut';
  const words = strings.setup.authenticatorApp;

  useEffect(() => {
    focusPageHeading();
  }, []);

  useEffect(() => {
    if (signedOut) {
      onTimedOut();
    }
  }, [signedOut, onTimedOut]);

  return (
    <div className={styles.view}>
      <PageBar>
        <BackLink to="/setup/password" previousIsDestination>
          {strings.setup.createPassword}
        </BackLink>
      </PageBar>
      <PageHeading>{words.heading}</PageHeading>
      <p>{words.intro}</p>
      <ol>
        <li>
          <p>{words.getApp}</p>
        </li>
        <li>
          <p>{words.addGrow2Notes}</p>
          {qrFailed ? (
            <p>{words.qrNotLoaded}</p>
          ) : (
            <div className={styles.qrPanel}>
              <img
                src={qrCodePath}
                width={200}
                height={200}
                alt={words.qrAlt}
                className={styles.qr}
                onError={() => setQrFailed(true)}
              />
            </div>
          )}
          <p>{words.thisPhone}</p>
          <LoadRegion queries={[key]} loading={strings.loading} what={words.theSetupKey}>
            {/* Called only once the key has come. */}
            {() => <SetupKey sharedKey={key.data!.sharedKey} />}
          </LoadRegion>
        </li>
        <li>
          <p>{words.comeBack}</p>
        </li>
      </ol>
    </div>
  );
}
