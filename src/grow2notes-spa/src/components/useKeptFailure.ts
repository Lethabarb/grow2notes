import type { UseQueryResult } from '@tanstack/react-query';
import { useState } from 'react';
import { classify, type Failure } from '../api/api.ts';

/** A failed first load, as it is shown. */
type Shown = {
  /** What the queries failed with. */
  error: unknown;
  /** After a Try again that failed. */
  again: boolean;
  /** When the queries last failed, as it was shown, which a refetch keeps and a new failure makes later. */
  latest: number;
};

/** The state of a first load of some queries, shown together or not at all, with its failure kept through Try again. */
type KeptLoad = {
  /** Every query has data. */
  ready: boolean;
  /** A query is fetching. */
  fetching: boolean;
  /** Why the first load failed, kept until the queries load or a Try again fails; `undefined` while none has failed. */
  failure: Failure | undefined;
  /** What the queries failed with, as the failure kept, which `LoadRegion` throws for Page not found. */
  error: unknown;
  /** The failure came from a Try again, which says "still did not load". */
  again: boolean;
  /** A Try again is running: from its press until the queries load or fail again. */
  retrying: boolean;
  /** For Try again to call as it sends the failed requests again. */
  retry: () => void;
};

/**
 * The first load of `queries` and its failure, which the failure sentence and Try again show (empty-loading-error.md
 * *Load failed and Try again*): `LoadRegion`'s, and the shell's start-up message's. A failed refetch in the
 * background, which leaves its data, is no failure here, and one that no Try again sent leaves a failure shown as it
 * was.
 */
export function useKeptFailure(queries: readonly UseQueryResult<unknown>[]): KeptLoad {
  // TanStack re-renders a component only for the fields of its queries that have been read, so each is read every
  // time.
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
    // A failure of requests that no Try again sent, such as the shell's refresh on a window focus, leaves the one
    // shown as it was, its cause and "still" with it, so that nothing the person did not ask for changes or is
    // announced (app-shell.md *States*: "keep everything as it was").
    const fresh = shown === undefined || retrying;
    setShown(fresh ? { error: loadError, again: retrying, latest } : { ...shown, latest });
    setRetrying(false);
  } else if (shown !== undefined && latest < shown.latest) {
    setShown(undefined);
    setRetrying(false);
  }

  return {
    ready,
    fetching,
    failure: shown === undefined ? undefined : classify(shown.error),
    error: shown?.error,
    again: shown?.again ?? false,
    retrying,
    retry: () => {
      setRetrying(true);
    },
  };
}
