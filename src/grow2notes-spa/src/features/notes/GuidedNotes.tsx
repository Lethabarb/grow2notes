import { useLayoutEffect, useRef, useState, type FormEvent, type RefObject } from 'react';
import { CharacterCount } from '../../components/CharacterCount.tsx';
import textBox from '../../components/TextBox.module.css';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import styles from './GuidedNotes.module.css';

// GOV.UK's threshold, 90% of the limit (guided-notes-textarea.md *Copy*).
const countFrom = 18_000;

const boxId = 'guided-notes';

type GuidedNotesProps = {
  /** The text that the box holds as it mounts, from the draft, which it never takes again. */
  initialText: string;
  /** The organisation's guide prompts, as saved, line breaks and all; `""` for none. */
  prompts: string;
  /** The box, whose value the form reads as each save is sent (note-form.md component 10). */
  boxRef: RefObject<HTMLTextAreaElement | null>;
  /** Each input into the box, a change for autosave. */
  onChange: () => void;
};

/**
 * Section 3 of the note form (guided-notes-textarea.md): its `<h2>`, which is the box's label, then the box, in the
 * shared text box's look, with the guide prompts as its placeholder, gone at the first character typed and back when
 * the box is emptied (D34), and none when no prompts are saved. The box is uncontrolled, so that nothing but typing
 * changes what it holds; it grows with its text, never shorter than the prompts or about 6 lines, so that the page
 * scrolls rather than the box. Nothing stops the typing: there is no `maxlength`, and the count shows from 18,000
 * characters, counted as the server counts them.
 */
export function GuidedNotes({ initialText, prompts, boxRef, onChange }: GuidedNotesProps) {
  const grow = useRef<HTMLDivElement>(null);
  const [length, setLength] = useState(initialText.length);
  // The box's inputs, after which the count's twin speaks once typing has stopped.
  const [inputs, setInputs] = useState(0);

  useLayoutEffect(() => {
    // Leaves the browser's own completions out of the record of what happened (D31); React has no prop for it yet.
    boxRef.current?.setAttribute('writingsuggestions', 'false');
  }, [boxRef]);

  function onInput(event: FormEvent<HTMLTextAreaElement>) {
    const { value } = event.currentTarget;
    grow.current?.setAttribute('data-value', value);
    setLength(value.length);
    setInputs((count) => count + 1);
    onChange();
  }

  return (
    <div className={styles.section}>
      <h2>
        <label htmlFor={boxId}>{strings.note.guidedNotes}</label>
      </h2>
      {/* The copies behind the box, from data-prompts and data-value, size it (TextBox.module.css). */}
      <div ref={grow} className={textBox.grow} data-prompts={prompts} data-value={initialText}>
        <textarea
          ref={boxRef}
          id={boxId}
          name="narrative"
          className={textBox.box}
          defaultValue={initialText}
          placeholder={prompts === '' ? undefined : prompts}
          aria-required="true"
          data-over-limit={length > limits.guidedNotes || undefined}
          spellCheck
          autoCorrect="on"
          autoCapitalize="sentences"
          autoComplete="off"
          onInput={onInput}
        />
      </div>
      <CharacterCount length={length} limit={limits.guidedNotes} from={countFrom} inputs={inputs} />
    </div>
  );
}
