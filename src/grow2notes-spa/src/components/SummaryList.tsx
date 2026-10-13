import type { ReactNode } from 'react';
import styles from './SummaryList.module.css';

/**
 * A summary list (GOV.UK's pattern, users.md component C item 4): a record's key facts, read-only, as a `<dl>` of
 * `SummaryRow`s, with a rule above the first, in the reading column.
 */
export function SummaryList({ children }: { children: ReactNode }) {
  return <dl className={styles.list}>{children}</dl>;
}
