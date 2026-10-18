import type { ReactNode } from 'react';
import styles from './LinkRowList.module.css';

type LinkRowListProps = {
  children: ReactNode;
  /** An `<ol>`, for rows whose order is their meaning, as a list of notes by date (chronological-list.md). */
  ordered?: boolean;
};

/**
 * A list of `LinkRow`s (participant-list-rows.md), with a divider above the first: a `<ul>`, or an `<ol>` when it is
 * `ordered`. `role="list"`, as Safari drops a list's semantics under `list-style: none`.
 */
export function LinkRowList({ children, ordered = false }: LinkRowListProps) {
  const List = ordered ? 'ol' : 'ul';
  return (
    <List role="list" className={styles.list}>
      {children}
    </List>
  );
}
