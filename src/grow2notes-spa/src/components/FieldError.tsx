import type { ReactNode } from 'react';
import { strings } from '../copy/strings.ts';
import styles from './FieldError.module.css';

/**
 * A field's error message (form-validation.md *Anatomy*), which its control's `aria-describedby` names by its id,
 * `{fieldId}-error`. The hidden "Error:" says it is an error to a screen reader, which hears no colour or weight.
 */
export function FieldError({ fieldId, children }: { fieldId: string; children: ReactNode }) {
  // The space is outside the hidden text, as in GOV.UK's markup: at the end of text laid out on its own, it would be
  // dropped, and "Error:" would run into the message.
  return (
    <p id={`${fieldId}-error`} className={styles.message}>
      <span className="visually-hidden">{strings.errorPrefix}</span> {children}
    </p>
  );
}
