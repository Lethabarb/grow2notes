import { useEffect, useRef, useState } from 'react';
import { msg } from '../copy/messages.ts';
import styles from './CharacterCount.module.css';

// The twin speaks once typing has stopped for 1 s, never at each keystroke (conditional-reveal.md *Behaviour*).
const countPause = 1000;

type CharacterCountProps = {
  /** The length of the text as its form would send it, so that the count and the form's check agree. */
  length: number;
  /** The form's limit, from `limits`. */
  limit: number;
  /**
   * Shows the count only from this length, as Guide prompts' does from 900 characters (guide-prompts.md 6c), and
   * nothing below it. Without it, the count shows from the first character, and "You can enter up to …" while empty.
   */
  from?: number;
  /**
   * How many inputs the field has had, which its form counts: the twin speaks once 1 s has passed with no new one, so
   * that a change that no input made, such as the field emptied after Add, says nothing, and calls off what an input
   * just before it was going to say.
   */
  inputs: number;
};

/**
 * A field's character count (conditional-reveal.md *Character count*, GOV.UK's): under the field, the limit, or how
 * many characters are left, or, past the limit, how many too many, bold in the error colour, which is not yet an
 * error; hidden from screen readers, which hear its always-rendered polite twin instead, once typing has stopped.
 * Nothing stops the typing: a field with a count has no `maxLength`, so pasted and dictated text is kept whole.
 */
export function CharacterCount({ length, limit, from, inputs }: CharacterCountProps) {
  let words: string | undefined;
  if (from === undefined && length === 0) {
    words = msg.characterLimitHint(limit);
  } else if (length >= (from ?? 0)) {
    words = msg.characterCount(length, limit);
  }

  const over = length > limit;
  const [spoken, setSpoken] = useState('');
  // The inputs that the twin has answered.
  const answered = useRef(inputs);

  useEffect(() => {
    if (inputs === answered.current) {
      return;
    }

    answered.current = inputs;
    const timer = window.setTimeout(() => {
      setSpoken(words ?? '');
    }, countPause);
    return () => window.clearTimeout(timer);
  }, [inputs, words]);

  return (
    <>
      {words !== undefined && (
        <p aria-hidden="true" className={styles.count} data-over-limit={over || undefined}>
          {words}
        </p>
      )}
      <p aria-live="polite" className="visually-hidden">
        {spoken}
      </p>
    </>
  );
}
