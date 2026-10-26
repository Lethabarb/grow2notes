import { Outlet } from 'react-router';
import { useMe } from '../api/me.ts';
import { Button } from '../components/Button.tsx';
import { PageHeading } from '../components/PageHeading.tsx';
import { PageStatus } from '../components/PageStatus.tsx';
import { pageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';
import { useFocusOnReplace } from './routeChange.ts';
import styles from './SupportGate.module.css';
import { useSignOut } from './useSignOut.ts';
import { useWholePageMessage } from './wholePageMessage.ts';

/**
 * The layout route around every signed-in screen, which shows a Support account the Support page in place of whatever
 * the address holds, so that the account sees no participants or notes and no screen asks the API for any
 * (mcp-server.md §2.2). Anyone else passes through.
 */
export function SupportGate() {
  return useMe().role === 'Support' ? <SupportPage /> : <Outlet />;
}

/**
 * The Support page (app-shell.md *Support page*): one line and Sign out, with the page's title and its `<h1>` focused
 * as for any whole-page message. A page of its own, so that a worker or a manager never runs its hooks.
 */
function SupportPage() {
  useWholePageMessage(pageName.connectAnAiAssistant);
  useFocusOnReplace();
  const { signOut, signingOut, failure } = useSignOut();

  return (
    <PageStatus>
      <PageHeading>{strings.supportPage}</PageHeading>
      {/* Its busy label is not announced: the status line says only why a sign-out failed. */}
      <Button busy={signingOut} busyLabel={strings.signingOut} announceBusy={false} onClick={() => void signOut()}>
        {strings.signOut}
      </Button>
      <p role="status" className={styles.statusLine}>{failure}</p>
    </PageStatus>
  );
}
