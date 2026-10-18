import type { ReactNode } from 'react';
import { focusInsteadOfHeading } from './focusPageHeading.ts';
import styles from './Notice.module.css';

type NoticeProps = {
  /** What it says, one or two short sentences, such as the archived participant's notice. */
  children: string;
  /** A control that acts on what it says, after the words, such as the archived notice's Restore with its alert. */
  action?: ReactNode;
  /**
   * Whether it takes focus as it appears: only when it appears because of a press whose button has gone, as the
   * archived notice does after Archive participant, never when it is there as the page opens.
   */
  takesFocus?: boolean;
  /**
   * `success` for the success banner (status-messages.md), Submit's, before Today's `<h1>`: a tick in place of the "i",
   * in the success colours, its sentence bold, and the screen's `[data-route-focus]`, which the shell focuses on every
   * arrival, a pop included, so that focus reads it out and it needs no role or live region.
   */
  tone?: 'notice' | 'success';
};

/**
 * A notice (status-messages.md): plain page content, read in its place, with a bar and an "i" in the action colour
 * on its tint, the words in the body's colour, and no role, title, close button or live region. Its bar and border are
 * real borders, so that forced colours draw them. Its action, if any, follows the words when there is room, and drops
 * under them otherwise.
 */
export function Notice({ children, action, takesFocus = false, tone = 'notice' }: NoticeProps) {
  const success = tone === 'success';
  return (
    <div
      className={success ? `${styles.notice} ${styles.success}` : styles.notice}
      tabIndex={takesFocus || success ? -1 : undefined}
      data-route-focus={success || undefined}
      ref={takesFocus ? focusAsItAppears : undefined}
    >
      <svg className={styles.icon} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
        <circle cx="10" cy="10" r="8.75" fill="none" stroke="currentColor" strokeWidth="1.5" />
        {success ? (
          <path d="M5.75 10.25 8.75 13.25 14.25 7" fill="none" stroke="currentColor" strokeWidth="2" />
        ) : (
          <>
            <circle cx="10" cy="6" r="1.25" fill="currentColor" />
            <path d="M10 9v6" fill="none" stroke="currentColor" strokeWidth="2" />
          </>
        )}
      </svg>
      <div className={styles.body}>
        <p className={styles.text}>{children}</p>
        {/* One item of the line, so that the action's alert stays above its button. */}
        {action !== undefined && <div>{action}</div>}
      </div>
    </div>
  );
}

/**
 * Focuses the notice as it appears, scrolling the page to it, in place of the `<h1>`, which then takes no focus that it
 * had pending, so that the words are read out and the action is the next Tab stop.
 */
function focusAsItAppears(notice: HTMLDivElement | null): void {
  if (notice !== null) {
    focusInsteadOfHeading(notice);
  }
}
