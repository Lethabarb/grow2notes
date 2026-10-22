import type { UseQueryResult } from '@tanstack/react-query';
import { useEffect, useRef, type ReactNode } from 'react';
import { isNotEditable, isNotFound } from '../api/api.ts';
import { msg } from '../copy/messages.ts';
import { strings } from '../copy/strings.ts';
import { Button } from './Button.tsx';
import { focusPageHeading } from './focusPageHeading.ts';
import styles from './LoadRegion.module.css';
import { PageHeading } from './PageHeading.tsx';
import { useDelayed } from './useDelayed.ts';
import { useKeptFailure } from './useKeptFailure.ts';

// A fast load shows nothing; one that takes longer shows the loading line (empty-loading-error.md *Timing*).
const loadingLineDelay = 1000;

type LoadRegionProps = {
  /** The region's queries, such as Today's list and drafts, shown together or not at all. */
  queries: readonly UseQueryResult<unknown>[];
  /** The screen's loading line, such as "Loading participants…". */
  loading: string;
  /** What did not load, as the failure sentence starts, such as "The participant list". */
  what: string;
  /** On a screen whose `<h1>` is data, the page's name, such as "Participant", shown as its `<h1>` if loading fails. */
  fallbackHeading?: string;
  /** The content, rendered only once every query has data. */
  children: () => ReactNode;
};

/**
 * A region of a screen that loads (empty-loading-error.md): nothing for 1 s, then the screen's loading line, then the
 * content; or, when the first load fails, the failure sentence with Try again, but nothing for a `401`, which the
 * session flow has, and Page not found in place of the whole screen for a `404` or a manager's `403`, or "You cannot
 * edit this note" for a refused edit. A failed refetch in the background leaves the content as it was.
 */
export function LoadRegion({ queries, loading, what, fallbackHeading, children }: LoadRegionProps) {
  const { ready, fetching, failure, error, again, retrying, retry } = useKeptFailure(queries);
  const sentence =
    failure === undefined || failure === 'signedOut' ? undefined : msg.loadFailed(what, failure, again);
  const failed = sentence !== undefined;
  const slow = useDelayed(!ready && fetching && !failed, loadingLineDelay);

  // Set by Try again, which goes when the queries load.
  const focusOnLoad = useRef(false);
  useEffect(() => {
    // After the commit, so that the heading focused is the content's own, not the fallback heading that has gone. Only
    // when focus has fallen to the page with the button: after a Try again that failed, the person may have moved on
    // before a refetch of TanStack's own loads the region.
    if (ready && focusOnLoad.current) {
      focusOnLoad.current = false;
      if (document.activeElement === document.body || document.activeElement === null) {
        focusPageHeading();
      }
    }
  }, [ready]);

  // Thrown for the screens' route error boundary, which shows Page not found, or the cannot-edit page, in place of the
  // whole screen, so that a screen's <h1> shown at once does not stay beside theirs. Thrown where the failure sentence
  // would show, once none of the queries is still fetching, so that a `404` waits for the others as the sentence does.
  if (isNotFound(error) || isNotEditable(error)) {
    throw error;
  }

  function tryAgain() {
    focusOnLoad.current = true;
    retry();
    for (const query of queries) {
      if (query.isLoadingError) {
        void query.refetch();
      }
    }
  }

  let line = slow ? loading : '';
  if (failed) {
    // Emptied while Try again runs, so that the screen visibly answers the press.
    line = retrying ? '' : sentence;
  }

  return (
    <>
      {ready && children()}
      {failed && fallbackHeading !== undefined && <PageHeading>{fallbackHeading}</PageHeading>}
      {/* Rendered every time, so that it is in the page before any words are written into it. */}
      <p role="status" className={styles.line}>{line}</p>
      {failed && (
        <Button className={styles.tryAgain} busy={retrying} busyLabel={strings.loading} onClick={tryAgain}>
          {strings.tryAgain}
        </Button>
      )}
    </>
  );
}
