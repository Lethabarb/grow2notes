import { Fragment, useId, type ReactNode } from 'react';
import { Link } from 'react-router';
import { Pause } from '../copy/Pause.tsx';
import styles from './LinkRow.module.css';

type LinkRowProps = {
  /** The address the row opens, which holds IDs and dates only (design.md §4.0). */
  to: string;
  /** The link's text, and so its whole accessible name, such as a person's name, or a note's date in a `<time>`. */
  name: ReactNode;
  /**
   * What tells the row apart in its list, such as the person's ID, by which the shell finds it again on a pop back to
   * the list, to focus it (app-shell.md *The route-change rule (one owner)*, 3b).
   */
  returnKey: string;
  /** The lines under the name, which describe the link, each on a line of its own; none for a row with none. */
  detail?: readonly ReactNode[];
  /**
   * `'date'` for a row of a list of notes by date, such as Past notes (chronological-list.md), whose link is the
   * date, in the link colour and underlined, with no chevron (participant-notes.md component 5); a person's or a
   * user's row by default (participant-list-rows.md).
   */
  look?: 'name' | 'date';
};

/**
 * A row of a `LinkRowList` (participant-list-rows.md, chronological-list.md): one link, whose text is the name alone
 * and whose `::after` covers the whole row, so that a tap anywhere in it opens the same page, then the detail, which
 * describes the link, and, but on a row of notes by date, a decorative chevron. Nothing else in the row is
 * interactive. The link is the row's return point, by its `data-return-key`.
 */
export function LinkRow({ to, name, returnKey, detail = [], look = 'name' }: LinkRowProps) {
  const detailId = useId();
  const hasDetail = detail.length > 0;

  return (
    <li className={styles.row}>
      <div className={styles.text}>
        <Link
          to={to}
          className={look === 'name' ? `${styles.link} ${styles.name}` : styles.link}
          aria-describedby={hasDetail ? detailId : undefined}
          data-return-key={returnKey}
        >
          {name}
        </Link>
        {hasDetail && (
          <p id={detailId} className={styles.detail}>
            {detail.map((line, index) => (
              <Fragment key={index}>
                {/* Between the lines, outside both, as Sep puts its space, so that the description reads "a, b". */}
                {index > 0 && ' '}
                <span className={styles.line}>
                  {line}
                  {index < detail.length - 1 && <Pause />}
                </span>
              </Fragment>
            ))}
          </p>
        )}
      </div>
      {look === 'name' && (
        <svg className={styles.chevron} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
          <path d="M7.5 4.5 13 10l-5.5 5.5" fill="none" stroke="currentColor" strokeWidth="2" />
        </svg>
      )}
    </li>
  );
}
