import styles from './ChoiceRow.module.css';

type ChoiceRowProps = {
  /** The radio's id; its hint's is `{id}-hint`. */
  id: string;
  /** The `name` of every radio in the set. */
  name: string;
  /** The choice, which names the radio. */
  label: string;
  /** Under the row, which describes the radio. */
  hint: string;
  checked: boolean;
  /** Called as the radio is chosen, by a press, a tap on the row or an arrow key. */
  onChange: () => void;
};

/**
 * One radio of a set, the app's one radio (form-validation.md, users.md component B item 6): the native radio, drawn
 * as a 40 px circle, in a row at least 56 px tall that is all its `<label>`, so that a tap anywhere on it chooses it.
 * Its hint is outside the label, so that it describes the radio rather than joining its name. Choosing it changes
 * nothing else until the form is sent (SC 3.2.2).
 */
export function ChoiceRow({ id, name, label, hint, checked, onChange }: ChoiceRowProps) {
  const hintId = `${id}-hint`;
  return (
    <div className={styles.choice}>
      <label className={styles.row}>
        <input
          type="radio"
          id={id}
          name={name}
          className={styles.radio}
          checked={checked}
          aria-describedby={hintId}
          onChange={onChange}
        />
        <span className={styles.text}>{label}</span>
      </label>
      <p id={hintId} className={styles.hint}>
        {hint}
      </p>
    </div>
  );
}
