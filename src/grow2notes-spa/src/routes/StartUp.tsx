import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { meQuery, refreshMe } from '../api/me.ts';
import { Button } from '../components/Button.tsx';
// LoadRegion's look, for the same line and Try again (empty-loading-error.md *Visual specification*).
import styles from '../components/LoadRegion.module.css';
import { PageHeading } from '../components/PageHeading.tsx';
import { useDelayed } from '../components/useDelayed.ts';
import { useKeptFailure } from '../components/useKeptFailure.ts';
import { msg } from '../copy/messages.ts';
import { pageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';

// A fast start-up shows nothing; one that takes longer shows the loading line (empty-loading-error.md *Timing*).
const loadingLineDelay = 1000;

type StartUpProps = {
  /**
   * Called as Try again takes focus or is pressed, so that the shell can hand focus to the `<h1>` of what the answer
   * brings, as the button goes with the message.
   */
  onTryAgainUsed: () => void;
};

/**
 * The shell's `<main>` while `GET /api/auth/me` has given neither an answer nor a `401` (app-shell.md component 4):
 * a status line that says "Loading…" from 1 s, then, when the request has failed after its one silent retry, the
 * message for its cause, with Try again, which asks again through `refreshMe()` and says "still did not load" if that
 * fails too. Built from `LoadRegion`'s parts, but with its line as its one status region and no `PageStatus`, so Try
 * again's busy label is shown but not announced (app-shell.md *Live regions*). A first failure moves no focus.
 */
export function StartUp({ onTryAgainUsed }: StartUpProps) {
  const queryClient = useQueryClient();
  const me = useQuery(meQuery);
  const { failure, again, retrying, retry } = useKeptFailure([me]);
  // A `401` never shows here, as the shell shows sign-in for it.
  const cause = failure === 'signedOut' ? undefined : failure;
  // From the first render, as the shell sends its request as it mounts.
  const slow = useDelayed(cause === undefined, loadingLineDelay);

  // In a `useEffect`, after the shell's title, which it sets in a layout effect, as a whole-page message sets its own
  // (app-shell-nav.md *Titles*). The shell sets the next page's as the message goes.
  useEffect(() => {
    if (cause !== undefined) {
      document.title = msg.pageTitle(cause === 'offline' ? pageName.couldNotConnect : pageName.thereIsAProblem);
    }
  }, [cause]);

  function tryAgain() {
    onTryAgainUsed();
    retry();
    // A failure stays in the query, from which the message reads it.
    refreshMe(queryClient).catch(() => {});
  }

  let line = slow ? strings.loading : '';
  if (cause !== undefined) {
    // Emptied while Try again runs, so that the page visibly answers the press.
    line = retrying ? '' : msg.loadFailed(strings.appName, cause, again);
  }

  return (
    <>
      {cause !== undefined && (
        <PageHeading>{cause === 'offline' ? strings.couldNotConnect : strings.thereIsAProblem}</PageHeading>
      )}
      {/* Rendered every time, so that it is in the page before any words are written into it. */}
      <p role="status" className={styles.line}>{line}</p>
      {cause !== undefined && (
        <Button
          className={styles.tryAgain}
          busy={retrying}
          busyLabel={strings.loading}
          onFocus={onTryAgainUsed}
          onClick={tryAgain}
        >
          {strings.tryAgain}
        </Button>
      )}
    </>
  );
}
