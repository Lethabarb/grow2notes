import { useState } from 'react';
import { CharacterCount } from '../../components/CharacterCount.tsx';
import { FieldError } from '../../components/FieldError.tsx';
import textBox from '../../components/TextBox.module.css';
import tickList from '../../components/TickList.module.css';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import guidedNotes from './GuidedNotes.module.css';
import styles from './MedicationField.module.css';

/** The box's id, which its label, its hint, its message and the error summary's link name. */
export const medicationId = 'medication';

type MedicationFieldProps = {
  /** The text as typed, which the form owns. */
  value: string;
  /** Each input into the box, with the whole text, a change for autosave. */
  onChange: (text: string) => void;
  /** Submit's message for the box, once a press has found one, until the input that fixes it. */
  error?: string;
};

/**
 * The Medication box of a note for a participant whose Medication line is on (note-form.md component 8b), after the
 * goals: its `<h2>`, which is the box's label, the hint "What was taken, and when.", then the box, optional and free
 * text (D14), and its count. Nothing stops the typing: there is no `maxlength`, and the count says how many characters
 * are left, or how many too many, counted as the server counts them. After a press of Submit that found the text too
 * long, its message sits between the hint and the box, with a 4 px bar down the section (form-validation.md).
 */
export function MedicationField({ value, onChange, error }: MedicationFieldProps) {
  // The box's inputs, after which the count's twin speaks once typing has stopped.
  const [inputs, setInputs] = useState(0);
  const hintId = `${medicationId}-hint`;
  return (
    <div className={guidedNotes.section} data-invalid={error !== undefined || undefined}>
      <h2>
        <label htmlFor={medicationId}>{strings.noteWords.medication}</label>
      </h2>
      <p id={hintId} className={tickList.hint}>
        {strings.note.medicationHint}
      </p>
      {error !== undefined && <FieldError fieldId={medicationId}>{error}</FieldError>}
      <textarea
        id={medicationId}
        name="medication"
        rows={4}
        className={`${textBox.box} ${styles.field}`}
        value={value}
        aria-invalid={error !== undefined || undefined}
        aria-describedby={error === undefined ? hintId : `${medicationId}-error ${hintId}`}
        data-over-limit={value.length > limits.medication || undefined}
        spellCheck
        autoComplete="off"
        autoCapitalize="sentences"
        onChange={(event) => {
          setInputs((count) => count + 1);
          onChange(event.currentTarget.value);
        }}
      />
      <CharacterCount length={value.length} limit={limits.medication} inputs={inputs} />
    </div>
  );
}
