import { FieldGroup } from '../../components/FieldGroup.tsx';
import styles from './OneTimeCodeField.module.css';

type OneTimeCodeFieldProps = {
  /** The field's id and name, which stay the same across releases, so that password managers keep filling them. */
  id: string;
  label: string;
  /** The label is the page's `<h1>`, as on sign-in's code step, whose one question is this field (view B). */
  labelIsHeading?: boolean;
  hint: string;
  /** The field's message once a submit has found one, or the server has refused the code. */
  error?: string;
  /** As typed: the form reads the code with `codeFormatError` on submit, and nothing reformats it before then. */
  value: string;
  onChange: (value: string) => void;
};

/**
 * The one field for an authenticator app's 6-digit code (sign-in.md component 10; totp-setup.md), which setup's view G
 * and sign-in's code step share: one text input, so that a pasted or filled-in code goes in whole (WCAG 3.3.8), with
 * a phone's number pad. It has no `maxLength`, which would cut a pasted "123 456" short, no `pattern`, which would
 * refuse its space, and no placeholder, which could pass for a code, and its form never submits by itself.
 */
export function OneTimeCodeField({ id, label, labelIsHeading, hint, error, value, onChange }: OneTimeCodeFieldProps) {
  return (
    <FieldGroup fieldId={id} label={label} labelIsHeading={labelIsHeading} hint={hint} error={error}>
      <input
        id={id}
        name={id}
        type="text"
        className={styles.input}
        inputMode="numeric"
        autoComplete="one-time-code"
        enterKeyHint="go"
        spellCheck={false}
        autoCapitalize="off"
        autoCorrect="off"
        aria-describedby={error === undefined ? `${id}-hint` : `${id}-hint ${id}-error`}
        aria-invalid={error === undefined ? undefined : true}
        value={value}
        onChange={(event) => onChange(event.currentTarget.value)}
      />
    </FieldGroup>
  );
}
