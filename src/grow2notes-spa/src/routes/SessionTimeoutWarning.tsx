import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { api, classify } from '../api/api.ts';
import { getPhase, isWarning, subscribe, type WarningPhase } from '../api/session.ts';
import { Button } from '../components/Button.tsx';
import dialogStyles from '../components/dialog.module.css';
import { closeOpenDialogs } from '../components/openDialogs.ts';
import { useDelayed } from '../components/useDelayed.ts';
import { strings } from '../copy/strings.ts';
import styles from './SessionTimeoutWarning.module.css';

const headingId = 'session-timeout-heading';

// The heading at each step of the countdown, which changes at these marks only (app-shell.md component 6).
const countdown: Readonly<Record<WarningPhase, string>> = {
  'warn-2m': strings.sessionWarning.twoMinutes,
  'warn-1m': strings.sessionWarning.oneMinute,
  'warn-40s': strings.sessionWarning.fortySeconds,
  'warn-20s': strings.sessionWarning.twentySeconds,
};

// A dialog keeps its labels and says it is still working in its status line (primary-actions.md §4).
const connectingDelay = 1_000;

type SessionTimeoutWarningProps = {
  /**
   * Whether the shell shows a signed-in screen, over which alone the warning opens: never over sign-in's view A, the
   * start-up message or setup, whose requests move the session clock all the same.
   */
  signedIn: boolean;
};

/**
 * The warning before the idle sign-out (app-shell.md component 6; session-timeout.md *The warning*): one native
 * `<dialog>`, always in the DOM, so that its live regions are there before their words, opened with `showModal()` as
 * the session clock enters a warning phase and closed with `close()` as it leaves one, which gives focus back where it
 * was. Stay signed in sends `POST /api/auth/ping`, and the dialog stays open until it has an answer, as the clock is
 * held while the request is out: any answer but a `401` starts the clock afresh and a `401` ends it, either of which
 * closes the dialog, while no connection leaves it open, saying so, with the countdown going on.
 */
export function SessionTimeoutWarning({ signedIn }: SessionTimeoutWarningProps) {
  const phase = useSyncExternalStore(subscribe, getPhase);
  const open = signedIn && isWarning(phase);
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [pinging, setPinging] = useState(false);
  const [failed, setFailed] = useState(false);
  const connecting = useDelayed(pinging, connectingDelay);
  // A failure is said in the warning it came in: once the dialog has closed, its line is empty for the next. Reset
  // during the render, as an effect that reset it would render twice.
  const [wasOpen, setWasOpen] = useState(open);
  if (wasOpen !== open) {
    setWasOpen(open);
    setFailed(false);
  }

  useEffect(() => {
    const dialog = dialogRef.current;
    if (dialog === null) {
      return;
    }

    // The button is the only thing in it that takes focus, so showModal() focuses it. An open confirmation closes
    // first, as Go back, so that the warning gives focus back to what opened it (app-shell.md *28:00 idle on a visible
    // page*).
    if (open && !dialog.open) {
      closeOpenDialogs();
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  async function staySignedIn() {
    if (pinging) {
      return;
    }

    // Emptied, so that a failure that repeats is written into an empty line and announced again.
    setFailed(false);
    setPinging(true);
    try {
      await api('/api/auth/ping', { method: 'POST' });
    } catch (error) {
      if (classify(error) === 'offline') {
        setFailed(true);
        // If the browser closed it, as Escape or back can with no `cancel`, it opens again while the time still gives a
        // warning: the clock, held until the request settled, has already taken the step that the time gives.
        const dialog = dialogRef.current;
        if (dialog !== null && !dialog.open && isWarning(getPhase())) {
          closeOpenDialogs();
          dialog.showModal();
        }
      }
    } finally {
      setPinging(false);
    }
  }

  const line = connecting ? strings.sessionWarning.connecting : failed ? strings.sessionWarning.noConnection : '';

  return (
    <dialog
      ref={dialogRef}
      className={dialogStyles.dialog}
      aria-labelledby={headingId}
      // Escape and Android back are Stay signed in.
      onCancel={(event) => {
        event.preventDefault();
        void staySignedIn();
      }}
      // Chromium may close it with no `cancel` when there has been no user activation since it opened. The event comes
      // a frame or more after the close, so one that finds the dialog open again is from an earlier close.
      onClose={(event) => {
        if (open && !event.currentTarget.open) {
          void staySignedIn();
        }
      }}
    >
      {/* Closed, it holds the first step's sentence, so that opening it changes no words in its live region. */}
      <h2 id={headingId} className={styles.heading} aria-live="polite" aria-atomic="true">
        {countdown[isWarning(phase) ? phase : 'warn-2m']}
      </h2>
      <p role="status" className={styles.statusLine}>{line}</p>
      <Button variant="primary" busy={pinging} className={styles.staySignedIn} onClick={() => void staySignedIn()}>
        {strings.sessionWarning.staySignedIn}
      </Button>
    </dialog>
  );
}
