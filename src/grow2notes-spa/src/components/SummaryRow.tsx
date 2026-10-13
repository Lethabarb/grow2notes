import type { ReactNode } from 'react';
import styles from './SummaryRow.module.css';

type SummaryRowProps = {
  /** The fact's name, such as "Email address". */
  term: string;
  /** The fact, as stored, never truncated. */
  children: ReactNode;
};

/**
 * A row of a `SummaryList`: one `<div>` holding the `<dt>` and its `<dd>`, which HTML allows in a `<dl>` and which
 * the row's border needs (users.md component C item 4); the term above the fact on a phone, and beside it from 40rem.
 */
export function SummaryRow({ term, children }: SummaryRowProps) {
  return (
    <div className={styles.row}>
      <dt className={styles.term}>{term}</dt>
      <dd className={styles.value}>{children}</dd>
    </div>
  );
}
