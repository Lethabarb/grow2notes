import { Fragment, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { classify } from '../api/api.ts';
import { msg } from '../copy/messages.ts';
import { strings, type Cause } from '../copy/strings.ts';
import { Button } from './Button.tsx';
import styles from './ChronoList.module.css';
import { LinkRowList } from './LinkRowList.tsx';
import { useDelayed } from './useDelayed.ts';

// A fast Show older shows nothing; one that takes longer says so in the status line (chronological-list.md *States*).
const loadingLineDelay = 1000;

/** The next page of a paged list, as TanStack's infinite query gives it. */
type Older = {
  /** The last page said `hasMore`. */
  hasNextPage: boolean;
  /** Asks for the next page, before the last row's cursor, and answers whether it failed, and how. */
  fetchNextPage: () => Promise<{ isError: boolean; error: unknown }>;
};

type ChronoListProps<T> = {
  /** The rows loaded so far, in the API's order, which the list never sorts again. */
  items: readonly T[];
  /** A row's stable key, such as a note's date. */
  getKey: (item: T) => string;
  /** A row: a `LinkRow`, whose one link is the row's. */
  renderRow: (item: T) => ReactNode;
  /** The screen's sentence for a list with no rows, such as "No notes yet for Jane Citizen.". */
  emptyText: string;
  older: Older;
};

/** Why the last Show older failed, and whether the one before it failed too, which says "still". */
type Failed = { cause: Cause; again: boolean };

/**
 * A paged list of notes by date (chronological-list.md), such as Past notes: an `<ol>` of `LinkRow`s, then Show older
 * while the last page said there are more, and its status line, there and empty from the first render; or the
 * screen's empty sentence alone, with no list or button. Nothing loads as the page scrolls, and nothing is counted.
 *
 * Show older adds the next page below the rows, which never move, and focuses the first new row's link, or, when no
 * row came and the button has gone, the last row's, so that focus never falls to the page. While it loads, the
 * button is `aria-disabled`, never `disabled`, a press does nothing, and the status line says "Loading older notes…"
 * once it has taken 1 s. A failure says why there, focus staying on the button, and "still" when the press before
 * failed too, words that a screen reader announces again; the next press clears it. A `401` says nothing, as the
 * session flow has it.
 */
export function ChronoList<T>({ items, getKey, renderRow, emptyText, older }: ChronoListProps<T>) {
  const list = useRef<HTMLDivElement>(null);
  // The key of the last row as Show older was pressed, until the rows after it are in the page.
  const focusAfter = useRef<string>(undefined);
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState<Failed>();
  // Whether the last Show older failed, which the next failure says with "still".
  const failedLast = useRef(false);
  const slow = useDelayed(busy, loadingLineDelay);

  // Before the browser paints the new rows, so that the first of them shows focused.
  useLayoutEffect(() => {
    const key = focusAfter.current;
    if (key === undefined) {
      return;
    }

    const last = items.findIndex((item) => getKey(item) === key);
    const rows = list.current?.querySelectorAll<HTMLElement>(':scope > ol > li') ?? [];
    let row: HTMLElement | undefined;
    if (last < items.length - 1) {
      row = rows[last + 1];
    } else if (!older.hasNextPage) {
      row = rows[last];
    } else {
      return;
    }

    focusAfter.current = undefined;
    row?.querySelector<HTMLElement>('a[href]')?.focus();
  }, [items, getKey, older.hasNextPage]);

  async function showOlder() {
    focusAfter.current = getKey(items[items.length - 1]);
    setBusy(true);
    setFailed(undefined);
    const result = await older.fetchNextPage();
    setBusy(false);
    if (!result.isError) {
      failedLast.current = false;
      return;
    }

    focusAfter.current = undefined;
    const cause = classify(result.error);
    if (cause !== 'signedOut') {
      setFailed({ cause, again: failedLast.current });
    }

    failedLast.current = true;
  }

  if (items.length === 0) {
    return <p className={styles.empty}>{emptyText}</p>;
  }

  let line = slow ? strings.showOlder.loading : '';
  if (failed !== undefined) {
    line = msg.loadFailed(strings.showOlder.olderNotes, failed.cause, failed.again);
  }

  return (
    <div ref={list}>
      <LinkRowList ordered>
        {items.map((item) => (
          <Fragment key={getKey(item)}>{renderRow(item)}</Fragment>
        ))}
      </LinkRowList>
      {older.hasNextPage && (
        <Button className={styles.older} busy={busy} onClick={() => void showOlder()}>
          {strings.showOlder.button}
        </Button>
      )}
      <p role="status" className={styles.status}>
        {line}
      </p>
    </div>
  );
}
