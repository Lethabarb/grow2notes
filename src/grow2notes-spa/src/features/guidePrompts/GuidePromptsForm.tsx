import { useLayoutEffect, useRef, useState } from 'react';
import textBox from '../../components/TextBox.module.css';
import { fieldIds } from './fieldIds.ts';
import type { GuidePrompts } from './guidePromptsQuery.ts';

/**
 * The guide prompts editor (guide-prompts.md component 6), mounted once the prompts have loaded: an uncontrolled
 * `<textarea>` in the shared text box, holding the loaded text exactly, line breaks included, and described by the
 * page's help text and limit hint. It is never focused by itself, as on a phone the keyboard would open over the help
 * text, and has no placeholder, which could be taken for saved prompts.
 */
export function GuidePromptsForm({ loaded }: { loaded: GuidePrompts }) {
  // Read once: a later refetch of the query must never change what the field holds (settings-textarea.md *Behaviour*
  // 2), as `defaultValue` would when it changed.
  const [text] = useState(loaded.text);
  const field = useRef<HTMLTextAreaElement>(null);
  const grow = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    // Leaves out the browser's own completions, in text that every worker sees on every note; React has no prop for
    // it yet.
    field.current?.setAttribute('writingsuggestions', 'false');
  }, []);

  return (
    <form noValidate>
      {/* The copy behind the box, from data-value, sizes it in a browser without field-sizing (TextBox.module.css). */}
      <div ref={grow} className={textBox.grow} data-value={text}>
        <textarea
          ref={field}
          id={fieldIds.text}
          name="text"
          className={textBox.box}
          defaultValue={text}
          aria-describedby={`${fieldIds.help} ${fieldIds.limit}`}
          spellCheck
          autoCapitalize="sentences"
          autoComplete="off"
          onInput={(event) => {
            grow.current?.setAttribute('data-value', event.currentTarget.value);
          }}
        />
      </div>
    </form>
  );
}
