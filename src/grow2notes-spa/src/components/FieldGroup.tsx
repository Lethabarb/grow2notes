import type { ReactNode } from 'react';
import { FieldError } from './FieldError.tsx';
import styles from './FieldGroup.module.css';

type FieldGroupProps = {
  /**
   * The control's id, or, for a group, the id its controls' ids start with, such as `details-dateOfBirth`. The hint's
   * id is `{fieldId}-hint` and the message's `{fieldId}-error`.
   */
  fieldId: string;
  /** The label's words, or a group's question, such as "Role". */
  label: ReactNode;
  /** The controls are a group, such as a set of radios or a three-part date: a `<fieldset>` whose legend is `label`. */
  group?: boolean;
  /** Under the label, such as "Up to 200 characters". */
  hint?: ReactNode;
  /** The field's error message once a submit has found one, in the same words as its link in the error summary. */
  error?: string;
  /** The control, whose id is `fieldId`, or a group's controls. */
  children: ReactNode;
};

/**
 * A field's label, hint, error message and control, in that order (form-validation.md *Anatomy*), so the message sits
 * above the control, where a phone's keyboard never hides it. While it holds an error, the group has a 4 px bar down
 * its side, and each control in it with `aria-invalid="true"` a 3 px border. The field component sets its control's
 * `aria-describedby` and `aria-invalid`; a group's `<fieldset>`, which only this renders, is described here.
 */
export function FieldGroup({ fieldId, label, group = false, hint, error, children }: FieldGroupProps) {
  const hintId = `${fieldId}-hint`;
  const invalid = error !== undefined;
  const parts = (
    <>
      {hint !== undefined && <p id={hintId} className={styles.hint}>{hint}</p>}
      {invalid && <FieldError fieldId={fieldId}>{error}</FieldError>}
      {children}
    </>
  );

  if (!group) {
    return (
      <div className={styles.group} data-invalid={invalid || undefined}>
        <label htmlFor={fieldId} className={styles.label}>{label}</label>
        {parts}
      </div>
    );
  }

  const description = [hint === undefined ? undefined : hintId, invalid ? `${fieldId}-error` : undefined].filter(
    (id) => id !== undefined,
  );
  // The bar is on a wrapper, as GOV.UK's is: on the fieldset, which draws its legend across its top edge, the bar would
  // start half-way down the legend.
  return (
    <div className={styles.group} data-invalid={invalid || undefined}>
      <fieldset
        className={styles.fieldset}
        aria-describedby={description.length > 0 ? description.join(' ') : undefined}
      >
        <legend className={styles.label}>{label}</legend>
        {parts}
      </fieldset>
    </div>
  );
}
