import { time } from '../copy/format.ts';
import { strings } from '../copy/strings.ts';
import styles from './SaveStatus.module.css';

/**
 * What the save status says: nothing; "Saving…", hidden, once a save has waited as long as Save's busy label does;
 * or "Saved", with the `Date` of the save's answer in UTC, or `undefined` when the answer had none.
 */
export type SaveState = 'none' | 'saving' | { savedAtUtc: string | undefined };

/**
 * The status line beside a page's Save (status-messages.md `SaveStatus`): "Saved 4:12 pm" once a save is done, which
 * the page empties at its next press and its next keystroke, so that it never says "Saved" over changes that are not.
 * Rendered, empty, with the page's form, as a screen reader may not announce words written into a live region that was
 * added with them. The time is the server's, from the answer's `Date`, never the device's clock.
 */
export function SaveStatus({ state }: { state: SaveState }) {
  return (
    <p role="status" className={styles.status}>
      {state === 'saving' && <span className="visually-hidden">{strings.saving}</span>}
      {typeof state === 'object' && <Saved atUtc={state.savedAtUtc} />}
    </p>
  );
}

function Saved({ atUtc }: { atUtc: string | undefined }) {
  if (atUtc === undefined) {
    return strings.saved;
  }

  return (
    <>
      {strings.saved} <time dateTime={atUtc}>{time(atUtc)}</time>
    </>
  );
}
