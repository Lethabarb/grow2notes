import { useState } from 'react';
import { CharacterCount } from '../../components/CharacterCount.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import textInput from '../../components/textInput.module.css';
import tickList from '../../components/TickList.module.css';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import { detailId } from './detailId.ts';

type ItemDetailFieldProps = {
  itemId: string;
  /** The item's wording, which names the box for screen readers after its label. */
  itemText: string;
  /** The details as typed, which the form owns. */
  value: string;
  /** Each input into the box, with the whole text, a change for autosave. */
  onChange: (text: string) => void;
  /** Submit's message for the box, once a press has found one, until the input that fixes it. */
  error?: string;
};

/**
 * The Details box under a ticked common item that asks for details (note-form.md component 6b), directly after the item
 * in the page, so that it comes next in the tab order and the reading order: the visible label "Details", which a
 * screen reader hears with the item's wording after it, a short text field, and its count. It goes with the tick, or
 * with the unpicking of the item's group, and its text with it, and focus stays on the item's box (SC 3.2.2). Nothing
 * stops the typing, and the text is required to submit.
 */
export function ItemDetailField({ itemId, itemText, value, onChange, error }: ItemDetailFieldProps) {
  // The box's inputs, after which the count's twin speaks once typing has stopped.
  const [inputs, setInputs] = useState(0);
  const id = detailId(itemId);
  return (
    <div className={tickList.detailField}>
      <FieldGroup
        fieldId={id}
        error={error}
        label={
          <>
            {strings.noteWords.details}
            <span className="visually-hidden">{msg.rowContext(itemText)}</span>
          </>
        }
      >
        <input
          id={id}
          type="text"
          name="detail"
          className={textInput.input}
          value={value}
          aria-invalid={error === undefined ? undefined : true}
          aria-describedby={error === undefined ? undefined : `${id}-error`}
          autoComplete="off"
          onChange={(event) => {
            setInputs((count) => count + 1);
            onChange(event.currentTarget.value);
          }}
        />
        <CharacterCount length={value.length} limit={limits.itemDetail} inputs={inputs} />
      </FieldGroup>
    </div>
  );
}
