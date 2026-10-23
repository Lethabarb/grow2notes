import type { ReactNode } from 'react';
import styles from './LinkRowList.module.css';

type LinkRowListProps = {
  children: ReactNode;
  /** An `<ol>`, for rows whose order is their meaning, as a list of notes by date (chronological-list.md). */
  ordered?: boolean;
  /** For an `ordered` list that runs from the highest number down, as the version list does: an `<ol reversed>`. */
  reversed?: boolean;
};

/**
 * A list of `LinkRow`s (participant-list-rows.md), with a divider above the first: a `<ul>`, or an `<ol>` when it is
 * `ordered`, which is `reversed` when its numbers count down. `role="list"`, as Safari drops a list's semantics under
 * `list-style: none`.
 */
export function LinkRowList({ children, ordered = false, reversed = false }: LinkRowListProps) {
  return ordered ? (
    <ol role="list" reversed={reversed} className={styles.list}>
      {children}
    </ol>
  ) : (
    <ul role="list" className={styles.list}>
      {children}
    </ul>
  );
}
