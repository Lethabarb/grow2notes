import type { ReactNode } from 'react';
import styles from './LinkRowList.module.css';

/**
 * A list of `LinkRow`s (participant-list-rows.md), with a divider above the first. `role="list"`, as Safari drops a
 * list's semantics under `list-style: none`.
 */
export function LinkRowList({ children }: { children: ReactNode }) {
  return (
    <ul role="list" className={styles.list}>
      {children}
    </ul>
  );
}
