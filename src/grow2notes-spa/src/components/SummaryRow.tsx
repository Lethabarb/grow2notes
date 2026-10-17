import type { ReactNode, Ref } from 'react';
import styles from './SummaryRow.module.css';

type SummaryRowProps = {
  /** The fact's name, such as "Email address". */
  term: string;
  /** The fact, as stored, never truncated. */
  children: ReactNode;
  /** The row's `<div>`, for a row that script focuses. */
  ref?: Ref<HTMLDivElement>;
  /**
   * `-1` for a row that script focuses, out of the tab order, such as a user's Status, which takes focus when an action
   * changes it and so takes its own button away (users.md component C item 2).
   */
  tabIndex?: -1;
};

/**
 * A row of a `SummaryList`: one `<div>` holding the `<dt>` and its `<dd>`, which HTML allows in a `<dl>` and which
 * the row's border needs (users.md component C item 4); the term above the fact on a phone, and beside it from 40rem.
 * Focused, it is read as its term and fact, such as "Status, Invited", and draws base.css's ring.
 */
export function SummaryRow({ term, children, ref, tabIndex }: SummaryRowProps) {
  return (
    <div ref={ref} tabIndex={tabIndex} className={styles.row}>
      <dt className={styles.term}>{term}</dt>
      <dd className={styles.value}>{children}</dd>
    </div>
  );
}
