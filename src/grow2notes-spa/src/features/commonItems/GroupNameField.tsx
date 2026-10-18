import type { RefObject } from 'react';
import { CharacterCount } from '../../components/CharacterCount.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { cleanText } from '../../components/listWrite.ts';
import textInput from '../../components/textInput.module.css';
import { msg } from '../../copy/messages.ts';
import { limits } from '../../limits.ts';
import styles from './GroupNameField.module.css';

type GroupNameFieldProps = {
  fieldId: string;
  label: string;
  name: string;
  /** The message after a press that found the name refused, until the name passes. */
  error: string | undefined;
  /** The field's inputs, after which the count's twin speaks. */
  inputs: number;
  /** While a rename's Save is out, so that what was sent is what shows (common-items.md *Group actions*). */
  readOnly?: boolean;
  fieldRef: RefObject<HTMLInputElement | null>;
  onChange: (name: string) => void;
};

/**
 * A group's name field, New group's or a rename's Group name (common-items.md components 5 and 7): its label; its
 * message, while it has one; a visually hidden hint of the limit (conditional-reveal.md's `-hint`); the shared text
 * input, 48 px tall, with no `maxLength`, so that a paste over the limit is kept whole; and the count under it, which
 * counts the name as its form sends it. It has an item field's count and error markup, but it is described by its
 * message, then the hint, never by the help text, which would bury the count, nor by the count, which changes at each
 * keystroke and has a twin of its own (common-items.md component 2). Past the limit, the input's border and the count
 * take the error colour, which is not yet an error.
 */
export function GroupNameField({
  fieldId,
  label,
  name,
  error,
  inputs,
  readOnly = false,
  fieldRef,
  onChange,
}: GroupNameFieldProps) {
  const hintId = `${fieldId}-hint`;
  const length = cleanText(name).length;

  return (
    <div className={styles.field}>
      <FieldGroup fieldId={fieldId} label={label} error={error}>
        <p id={hintId} className="visually-hidden">
          {msg.characterLimitHint(limits.groupName)}
        </p>
        <input
          ref={fieldRef}
          id={fieldId}
          type="text"
          className={`${textInput.input} ${styles.input}`}
          value={name}
          readOnly={readOnly}
          autoComplete="off"
          autoCapitalize="sentences"
          aria-describedby={error === undefined ? hintId : `${fieldId}-error ${hintId}`}
          aria-invalid={error === undefined ? undefined : true}
          data-over-limit={length > limits.groupName || undefined}
          onChange={(event) => onChange(event.currentTarget.value)}
        />
        <CharacterCount length={length} limit={limits.groupName} inputs={inputs} />
      </FieldGroup>
    </div>
  );
}
