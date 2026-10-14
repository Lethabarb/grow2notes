import { useId, useLayoutEffect, useRef, type ReactNode } from 'react';
import { Button } from './Button.tsx';
import styles from './ConfirmDialog.module.css';
import dialogStyles from './dialog.module.css';

type DialogButton = 'primary' | 'secondary';

// The return value that only the confirming button closes the dialog with: any other close leaves it empty.
const confirmedValue = 'confirm';

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
  /** The button that has focus as the dialog opens. */
  initialFocus: DialogButton;
  /** Called once the dialog has closed: `true` when its confirming button closed it, otherwise `false`. */
  onClose: (confirmed: boolean) => void;
};

/**
 * The confirmation dialogs' shell (confirm-dialog.md): a native `<dialog>`, which the page mounts to open it and
 * unmounts once it has closed. Escape, Android's back gesture and a plain `close()`, with which the shell closes any
 * open dialog before its 28-minute warning (app-shell.md *What happens on each action*), all mean the safe choice,
 * and a tap on the backdrop does nothing, as `showModal()` gives no light dismiss. Closing it gives focus back to what
 * had it before it opened, the button or link that opened it.
 */
export function ConfirmDialog({
  title,
  children,
  primary,
  secondary,
  confirmButton,
  initialFocus,
  onClose,
}: ConfirmDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const primaryRef = useRef<HTMLButtonElement>(null);
  const secondaryRef = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const bodyId = useId();

  useLayoutEffect(() => {
    const dialog = dialogRef.current;
    // Only if it is not open yet, as StrictMode runs this twice.
    if (dialog !== null && !dialog.open) {
      dialog.showModal();
    }

    // Set here, as showModal() would focus the first button, and React's autoFocus does nothing in a dialog that is
    // still closed as it mounts (react#23301).
    (initialFocus === 'primary' ? primaryRef : secondaryRef).current?.focus();
  }, [initialFocus]);

  function close(button: DialogButton) {
    dialogRef.current?.close(button === confirmButton ? confirmedValue : undefined);
  }

  return (
    <dialog
      ref={dialogRef}
      role="alertdialog"
      // Implicit with showModal(), but stated, as the role is overridden (APG alert dialog pattern).
      aria-modal="true"
      aria-labelledby={titleId}
      aria-describedby={bodyId}
      className={dialogStyles.dialog}
      onClose={(event) => onClose(event.currentTarget.returnValue === confirmedValue)}
    >
      <h2 id={titleId} className={styles.title}>{title}</h2>
      <div id={bodyId} className={styles.body}>{children}</div>
      <div className={styles.buttons}>
        <Button ref={primaryRef} variant="primary" className={styles.button} onClick={() => close('primary')}>
          {primary}
        </Button>
        <Button ref={secondaryRef} variant="secondary" className={styles.button} onClick={() => close('secondary')}>
          {secondary}
        </Button>
      </div>
    </dialog>
  );
}
