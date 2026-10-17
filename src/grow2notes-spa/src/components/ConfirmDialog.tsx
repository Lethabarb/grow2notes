import { useId, useLayoutEffect, useRef, type ReactNode } from 'react';
import { Button } from './Button.tsx';
import type { ButtonVariant } from './buttonClass.ts';
import styles from './ConfirmDialog.module.css';
import dialogStyles from './dialog.module.css';
import { trackOpenDialog } from './openDialogs.ts';
import { useDelayed } from './useDelayed.ts';

type DialogButton = 'primary' | 'secondary';

// The return value that only the confirming button closes the dialog with: any other close leaves it empty.
const confirmedValue = 'confirm';

// A dialog keeps its labels and says that it is still working in its status line (primary-actions.md §4).
const busyLineDelay = 1_000;

type ConfirmDialogProps = {
  /** The dialog's name, its `<h2>`: the question, such as "Leave without saving?". */
  title: string;
  /** Its description, one to three short `<p>`. */
  children: ReactNode;
  /** The top button's words, in the primary style. */
  primary: string;
  /** The bottom button's words, in the secondary style. */
  secondary: string;
  /** The button that confirms. The other is the safe choice, as is any other way of closing the dialog. */
  confirmButton: DialogButton;
  /**
   * What has focus as the dialog opens: a button, or the title, for a dialog whose confirming button no Enter may
   * press by accident (confirm-dialog.md *Behaviour* 2).
   */
  initialFocus: DialogButton | 'title';
  /** `warning` draws the confirming button red, for an action that cannot be undone (confirm-dialog.md). */
  tone?: 'standard' | 'warning';
  /**
   * Called as the confirming button is pressed, for a dialog whose confirmation sends a request: the dialog stays
   * open, shows how the request goes through `busy`, `error` and `canConfirm`, and is unmounted by the page once the
   * request has succeeded. Without it, the confirming button closes the dialog.
   */
  onConfirm?: () => void;
  /**
   * The request is out: both buttons are `aria-disabled`, keeping their labels, and presses, Escape and Back do
   * nothing.
   */
  busy?: boolean;
  /** The verb and "…", such as "Resetting sign-in…", that the status line below the buttons shows after 1 s busy. */
  busyLabel?: string;
  /** Why the request failed, in the alert line above the buttons. */
  error?: string;
  /** False once confirming is no longer possible: the confirming button goes, and the safe one takes focus. */
  canConfirm?: boolean;
  /** Called once the dialog has closed: `true` when its confirming button closed it, otherwise `false`. */
  onClose: (confirmed: boolean) => void;
};

/**
 * The confirmation dialogs' shell (confirm-dialog.md): a native `<dialog>`, which the page mounts to open it and
 * unmounts once it has closed, or once the request that it confirmed has succeeded. Escape, Android's back gesture and
 * a plain `close()`, with which `closeOpenDialogs()` closes it before the 28-minute warning and before sign-in's
 * view C, all mean the safe choice, and a tap on the backdrop does nothing, as `showModal()` gives no light dismiss.
 * Closing it gives focus back to what had it before it opened, the button or link that opened it. Its alert line and
 * its status line are in it, empty, from its first render, so that the words written into them later are announced.
 */
export function ConfirmDialog({
  title,
  children,
  primary,
  secondary,
  confirmButton,
  initialFocus,
  tone = 'standard',
  onConfirm,
  busy = false,
  busyLabel,
  error,
  canConfirm = true,
  onClose,
}: ConfirmDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const primaryRef = useRef<HTMLButtonElement>(null);
  const secondaryRef = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const bodyId = useId();
  const showsBusyLine = useDelayed(busy, busyLineDelay);
  const safeButton: DialogButton = confirmButton === 'primary' ? 'secondary' : 'primary';

  useLayoutEffect(() => {
    const target = { title: titleRef, primary: primaryRef, secondary: secondaryRef }[initialFocus].current;
    // So that showModal() itself focuses it, rather than the first button; set here, as React writes no autofocus
    // attribute (react#23301).
    target?.setAttribute('autofocus', '');
    const dialog = dialogRef.current;
    // Only if it is not open yet, as StrictMode runs this twice.
    if (dialog !== null && !dialog.open) {
      dialog.showModal();
    }

    // And directly, should showModal() not have moved focus (confirm-dialog.md *Implementation notes*).
    target?.focus();
  }, [initialFocus]);

  useLayoutEffect(() => {
    const dialog = dialogRef.current;
    return dialog === null ? undefined : trackOpenDialog(dialog);
  }, []);

  // The confirming button has gone, so the one that is left takes focus, which would otherwise fall to the page.
  useLayoutEffect(() => {
    if (!canConfirm) {
      (safeButton === 'primary' ? primaryRef : secondaryRef).current?.focus();
    }
  }, [canConfirm, safeButton]);

  function press(button: DialogButton) {
    if (button === confirmButton && onConfirm !== undefined) {
      onConfirm();
      return;
    }

    dialogRef.current?.close(button === confirmButton ? confirmedValue : undefined);
  }

  const variant = (button: DialogButton): ButtonVariant =>
    button === confirmButton && tone === 'warning' ? 'warning' : button;
  const shown = (button: DialogButton) => canConfirm || button === safeButton;

  return (
    <dialog
      ref={dialogRef}
      role="alertdialog"
      // Implicit with showModal(), but stated, as the role is overridden (APG alert dialog pattern).
      aria-modal="true"
      aria-labelledby={titleId}
      aria-describedby={bodyId}
      className={dialogStyles.dialog}
      // Escape and Android's back gesture, which a browser may still let through when they come again
      // (confirm-dialog.md *Behaviour* 7): the request then still ends, and the page shows how.
      onCancel={(event) => {
        if (busy) {
          event.preventDefault();
        }
      }}
      onClose={(event) => onClose(event.currentTarget.returnValue === confirmedValue)}
    >
      <h2 id={titleId} ref={titleRef} tabIndex={initialFocus === 'title' ? -1 : undefined} className={styles.title}>
        {title}
      </h2>
      <div id={bodyId} className={styles.body}>{children}</div>
      <p role="alert" className={styles.error}>
        {error}
      </p>
      <div className={styles.buttons}>
        {shown('primary') && (
          <Button
            ref={primaryRef}
            variant={variant('primary')}
            busy={busy}
            className={styles.button}
            onClick={() => press('primary')}
          >
            {primary}
          </Button>
        )}
        {shown('secondary') && (
          <Button
            ref={secondaryRef}
            variant={variant('secondary')}
            busy={busy}
            className={styles.button}
            onClick={() => press('secondary')}
          >
            {secondary}
          </Button>
        )}
      </div>
      <p role="status" className={styles.busyLine}>
        {showsBusyLine ? busyLabel : undefined}
      </p>
    </dialog>
  );
}
