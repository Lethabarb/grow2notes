import { useEffect, useRef } from 'react';
import { strings } from '../copy/strings.ts';
import styles from './ErrorSummary.module.css';

export type FormError = {
  /** The same words as the field's own message. */
  message: string;
  /**
   * The id of the control to go to, for a group its first control in error. Left out for an error in no field, which
   * is listed with no link: sign-in's "Sign-in failed" (form-validation.md *Anatomy*).
   */
  fieldId?: string;
};

type ErrorSummaryProps = {
  /** In the order of the fields on the page. */
  errors: readonly FormError[];
  /** The number of times the form has been submitted, so that focus moves on each new attempt, never while typing. */
  attempt: number;
};

/**
 * Scrolls the field's label into view, or its group's legend for a field in a group, then focuses the field without
 * scrolling again, as govuk-frontend's summary does, so that the words above the field, its message among them, stay
 * on screen.
 */
function goToField(fieldId: string) {
  const field = document.getElementById(fieldId);
  if (field === null) {
    return;
  }

  const legend = field.closest('fieldset')?.querySelector(':scope > legend');
  const label = document.querySelector(`label[for="${fieldId}"]`);
  (legend ?? label ?? field).scrollIntoView({ block: 'start' });
  field.focus({ preventScroll: true });
}

/**
 * The list of a form's errors (form-validation.md), first in `<main>`, above the `<h1>`. It takes focus on each
 * failed submit, even with the same errors, so a screen reader reads it and its links, and puts "Error: " in front of
 * the page title while it shows any errors. With none it renders nothing.
 */
export function ErrorSummary({ errors, attempt }: ErrorSummaryProps) {
  const summary = useRef<HTMLDivElement>(null);
  const hasErrors = errors.length > 0;

  // The last attempt seen, so that errors that change as the person types, with no new attempt, move no focus.
  const seenAttempt = useRef<number>(undefined);
  useEffect(() => {
    if (attempt === seenAttempt.current) {
      return;
    }

    seenAttempt.current = attempt;
    if (hasErrors) {
      summary.current?.focus();
    }
  }, [attempt, hasErrors]);

  useEffect(() => {
    if (!hasErrors) {
      return;
    }

    const prefix = `${strings.errorPrefix} `;
    document.title = prefix + document.title;
    return () => {
      if (document.title.startsWith(prefix)) {
        document.title = document.title.slice(prefix.length);
      }
    };
  }, [hasErrors]);

  if (!hasErrors) {
    return null;
  }

  return (
    <div ref={summary} tabIndex={-1} className={styles.summary}>
      <h2>{strings.errorSummaryHeading}</h2>
      {/* VoiceOver in Safari reads a list drawn with no bullets as no list, so the role says it is one. */}
      <ul role="list" className={styles.list}>
        {errors.map(({ message, fieldId }) => (
          <li key={fieldId ?? message}>
            {fieldId === undefined ? (
              message
            ) : (
              <a
                href={`#${fieldId}`}
                onClick={(event) => {
                  // No hash in the URL and no entry in the history (design.md §4.0 keeps URLs to ids and dates).
                  event.preventDefault();
                  goToField(fieldId);
                }}
              >
                {message}
              </a>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
