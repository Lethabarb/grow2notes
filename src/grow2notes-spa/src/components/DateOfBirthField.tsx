import { strings } from '../copy/strings.ts';
import styles from './DateOfBirthField.module.css';
import { FieldGroup } from './FieldGroup.tsx';
import type { DobPart, DobParts } from './parseDob.ts';
import textInput from './textInput.module.css';

const parts: readonly { part: DobPart; label: string }[] = [
  { part: 'day', label: strings.dateOfBirth.day },
  { part: 'month', label: strings.dateOfBirth.month },
  { part: 'year', label: strings.dateOfBirth.year },
];

type DateOfBirthFieldProps = {
  /** The id that the parts' ids start with, such as `details-dateOfBirth`, then `-day`, `-month` and `-year`. */
  id: string;
  /** As typed: the form reads them with `parseDob` on Save, and nothing reformats them before then. */
  value: DobParts;
  onChange: (value: DobParts) => void;
  /** The group's message once Save has found one, in the same words as its link in the error summary. */
  error?: string;
  /** The parts that the message is about, which are marked invalid. */
  invalid: readonly DobPart[];
};

/**
 * A date of birth (text-and-date-inputs.md; GOV.UK's date input): a group, whose legend is "Date of birth", with its
 * hint, then three labelled text fields, Day, Month and Year, on one line that wraps, each with a phone's number pad,
 * though a month's name is typed too. Each is as wide as what it holds, and none has a `name`, `maxLength` or
 * `pattern`, or moves focus to the next by itself. As someone else's details, nothing is offered from or kept in the
 * browser's autofill (D21, D22).
 */
export function DateOfBirthField({ id, value, onChange, error, invalid }: DateOfBirthFieldProps) {
  return (
    <FieldGroup fieldId={id} label={strings.dateOfBirth.legend} group hint={strings.dateOfBirth.hint} error={error}>
      <div className={styles.parts}>
        {parts.map(({ part, label }) => (
          <div key={part} className={styles.part}>
            <label htmlFor={`${id}-${part}`} className={styles.label}>
              {label}
            </label>
            <input
              id={`${id}-${part}`}
              type="text"
              inputMode="numeric"
              autoComplete="off"
              spellCheck={false}
              className={`${textInput.input} ${styles[part]}`}
              aria-invalid={invalid.includes(part) || undefined}
              value={value[part]}
              onChange={(event) => onChange({ ...value, [part]: event.currentTarget.value })}
            />
          </div>
        ))}
      </div>
    </FieldGroup>
  );
}
