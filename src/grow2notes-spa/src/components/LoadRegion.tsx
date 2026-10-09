import type { UseQueryResult } from '@tanstack/react-query';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { classify, type Failure } from '../api/api.ts';
import { msg } from '../copy/messages.ts';
import { strings } from '../copy/strings.ts';
import { Button } from './Button.tsx';
import { focusPageHeading } from './focusPageHeading.ts';
import styles from './LoadRegion.module.css';
import { PageHeading } from './PageHeading.tsx';
import { useDelayed } from './useDelayed.ts';

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

/** A failed first load, kept through Try again. */
type Shown = {
  failure: Failure;
  /** After a Try again that failed. */
  again: boolean;
  /** When the queries last failed, as it was shown, which a refetch keeps and a new failure makes later. */
  latest: number;
};

/**
 * A region of a screen that loads (empty-loading-error.md): nothing for 1 s, then the screen's loading line, then the
 * content; or, when the first load fails, the failure sentence with Try again, but nothing for a `401`, which the
 * session flow has. A failed refetch in the background leaves the content as it was.
 */
export function LoadRegion({ queries, loading, what, fallbackHeading, children }: LoadRegionProps) {
  // TanStack re-renders a screen only for the fields of its queries that have been read, so each is read every time.
  const results = queries.map(({ data, error, errorUpdatedAt, isFetching, isLoadingError }) => ({
    hasData: data !== undefined,
    loadError: isLoadingError ? error : undefined,
    errorUpdatedAt,
    isFetching,
  }));
  const ready = results.every((result) => result.hasData);
  const fetching = results.some((result) => result.isFetching);
  const loadError = results.find((result) => result.loadError !== undefined)?.loadError;
  const latest = Math.max(0, ...results.map((result) => result.errorUpdatedAt));

  // TanStack puts a query with no data back to pending, with no error, when it fetches again, so the failure is kept
  // here until the queries load or fail again. The queries passed in can still be those of the render before Try again
  // was pressed, so a failure is new only when the time the queries last failed is not the one shown. A query keeps
  // that time through a refetch, so an earlier one means other queries, as when a screen's query key changes
  // (date-navigation.md's `?date=`), and they load as a first load does. Set during render, as an effect that set them
  // would render twice.
  const [shown, setShown] = useState<Shown>();
  const [retrying, setRetrying] = useState(false);
  if (ready) {
    if (shown !== undefined) {
      setShown(undefined);
      setRetrying(false);
    }
  } else if (loadError !== undefined && !fetching && latest !== shown?.latest) {
    setShown({ failure: classify(loadError), again: retrying, latest });
    setRetrying(false);
  } else if (shown !== undefined && latest < shown.latest) {
    setShown(undefined);
    setRetrying(false);
  }

  const sentence =
    shown === undefined || shown.failure === 'signedOut'
      ? undefined
      : msg.loadFailed(what, shown.failure, shown.again);
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

  function tryAgain() {
    focusOnLoad.current = true;
    setRetrying(true);
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
