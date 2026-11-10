import { msg } from '../copy/messages.ts';
import { Pause } from '../copy/Pause.tsx';
import { strings } from '../copy/strings.ts';
import styles from './TickList.module.css';

/**
 * An item of a saved note's tick list: its wording as the version holds it, whether it is ticked, and, for a ticked
 * common item that asked for details, the details written (A151).
 */
export type TickedItem = { text: string; isTicked: boolean; detail?: string };

type TickListReadProps = {
  /**
   * The list's heading, the form's own for it, such as "Short term goals" or, among the common items, a group's
   * name.
   */
  heading: string;
  /** In place of the list when it has no items, such as "No goals set". */
  emptyText?: string;
  /** In the snapshot's order, which the list keeps. */
  items: readonly TickedItem[];
};

/**
 * A read-only tick list (checkbox-list.md `TickListRead`), for a note that is shown, not edited: the list's heading,
 * then each item as a read-only tick; or, with no items, the heading and the sentence alone. Keyed by place, as the API
 * gives the items no ID and the snapshot's order never changes.
 */
export function TickListRead({ heading, emptyText, items }: TickListReadProps) {
  return (
    <div className={styles.group}>
      <h2 className={styles.heading}>{heading}</h2>
      {items.length === 0 ? (
        emptyText !== undefined && <p className={styles.empty}>{emptyText}</p>
      ) : (
        // Safari drops a list's role once its bullets are gone, unless the role is given (checkbox-list.md).
        <ul role="list" className={styles.readList}>
          {items.map((item, index) => (
            <li key={index} className={styles.readItem}>
              <Tick {...item} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/**
 * One read-only tick on a line of its own, outside any list, such as the flag of a draft that a manager reads
 * (participant-notes.md *Someone else's draft, manager*), drawn and read as an item of `TickListRead` is.
 */
export function TickRowRead(item: TickedItem) {
  return (
    <p className={`${styles.readItem} ${styles.readRow}`}>
      <Tick {...item} />
    </p>
  );
}

/**
 * A read-only tick: its wording as text, never a form control, after one icon, a box drawn the same way whether ticked
 * or not, with a tick inside when it is, and followed, for screen readers, by a pause and "ticked" or "not ticked";
 * then, for an item with details, "Details: …" on a line of its own.
 */
function Tick({ text, isTicked, detail }: TickedItem) {
  return (
    <>
      <svg className={styles.icon} viewBox="0 0 24 24" aria-hidden="true" focusable="false">
        <rect x="2" y="2" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" />
        {isTicked && <path d="M6 12.5l4 4 8-9" fill="none" stroke="currentColor" strokeWidth="3" />}
      </svg>
      <span className={styles.readText}>
        {text}
        <Pause />
        <span className="visually-hidden">
          {' '}
          {isTicked ? strings.tickState.ticked : strings.tickState.notTicked}
        </span>
        {detail !== undefined && (
          <>
            <Pause />
            <span className={styles.detail}>{msg.itemDetails(detail)}</span>
          </>
        )}
      </span>
    </>
  );
}
