import styles from './RequestAlert.module.css';

/**
 * Where a form says that its request failed (primary-actions.md §4 and §7), directly above the form's `ButtonGroup`,
 * so that the words show where the person pressed, and focus stays on the button for them to press again. Rendered,
 * empty, with its form, as a screen reader may not announce text written into a live region that was added with it.
 * A user's page has one above its actions too (users.md component C item 5). The `id` is for a screen whose spec names
 * its alert, such as Export record's `export-alert`.
 *
 * The form gives it the text, from the mutation's error: `msg.notDone()` for no connection and a server failure,
 * nothing for a `401`, which the session flow has, and its own sentence for a `412` (form-validation.md *Server
 * responses*). The text then empties when the button is pressed again, so a failure that repeats is written into an
 * empty region and announced again.
 */
export function RequestAlert({ id, children }: { id?: string; children?: string }) {
  return (
    <div id={id} role="alert" className={styles.alert}>
      {children}
    </div>
  );
}
