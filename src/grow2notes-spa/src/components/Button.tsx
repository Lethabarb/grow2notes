import { useEffect, useState, type ComponentPropsWithRef, type MouseEvent } from 'react';
import styles from './Button.module.css';
import { buttonClass, type ButtonVariant } from './buttonClass.ts';
import { useAnnounce } from './pageStatusContext.ts';

// A fast request shows nothing; one that takes longer shows the busy label (primary-actions.md §4).
const busyLabelDelay = 400;

// No `disabled`: it would take the button out of the tab order and swallow the press without saying why.
type ButtonProps = Omit<ComponentPropsWithRef<'button'>, 'disabled'> & {
  variant?: ButtonVariant;
  /** design.md's "disabled": the button stays focusable, and a press calls `onUnavailablePress`, not `onClick`. */
  unavailable?: boolean;
  /** The id of the visible text that says why the button is unavailable. */
  unavailableReasonId?: string;
  /** Explains or fixes the reason, as a press never does nothing (primary-actions.md §5.1). */
  onUnavailablePress?: () => void;
  /** A request is in flight: a press does nothing. */
  busy?: boolean;
  /** The button's own verb and "…", such as "Saving changes…", shown once the button has been busy for 400 ms. */
  busyLabel?: string;
  /** Writes the busy label to the page's status region; false on a screen with a status line of its own. */
  announceBusy?: boolean;
};

/** The one button of the app (primary-actions.md §8), never rendered with `disabled`. */
export function Button({
  variant = 'secondary',
  unavailable = false,
  unavailableReasonId,
  onUnavailablePress,
  busy = false,
  busyLabel,
  announceBusy = true,
  type = 'button',
  className,
  'aria-describedby': describedBy,
  onClick,
  children,
  ...rest
}: ButtonProps) {
  const announce = useAnnounce();
  // Whether the request in flight has taken 400 ms. Set only by the timer, and reset here during render, as an
  // effect that set it would render twice; the next request then waits its 400 ms again.
  const [slow, setSlow] = useState(false);
  if (slow && !busy) {
    setSlow(false);
  }

  useEffect(() => {
    if (!busy) {
      return;
    }

    const timer = window.setTimeout(() => {
      setSlow(true);
      if (announceBusy && busyLabel !== undefined) {
        announce?.(busyLabel);
      }
    }, busyLabelDelay);
    return () => window.clearTimeout(timer);
  }, [busy, busyLabel, announceBusy, announce]);

  const showsReason = unavailable && !busy;

  function handleClick(event: MouseEvent<HTMLButtonElement>) {
    if (unavailable || busy) {
      // This also stops the button submitting its form, whether pressed or reached by Enter in one of its fields.
      event.preventDefault();
      if (showsReason) {
        onUnavailablePress?.();
      }

      return;
    }

    onClick?.(event);
  }

  const description = [showsReason ? unavailableReasonId : undefined, describedBy].filter((id) => id !== undefined);

  return (
    <button
      {...rest}
      type={type}
      className={className === undefined ? buttonClass(variant) : `${buttonClass(variant)} ${className}`}
      aria-disabled={unavailable || busy || undefined}
      aria-describedby={description.length > 0 ? description.join(' ') : undefined}
      data-unavailable={showsReason || undefined}
      data-busy={(slow && busyLabel !== undefined) || undefined}
      onClick={handleClick}
    >
      <span className={styles.label}>{children}</span>
      {busyLabel !== undefined && <span className={styles.busyLabel}>{busyLabel}</span>}
    </button>
  );
}
