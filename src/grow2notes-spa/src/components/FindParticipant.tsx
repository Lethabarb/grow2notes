import { useEffect, useId, useRef, useState } from 'react';
import { msg } from '../copy/messages.ts';
import { strings } from '../copy/strings.ts';
import { Button } from './Button.tsx';
import styles from './FindParticipant.module.css';
import textInput from './textInput.module.css';

// How long the typing must pause before the no-match sentence is read out (search-filter.md, [Opinion]: to be checked
// with NVDA and VoiceOver): long enough that it is said once, not after every key.
const announceDelay = 500;

type FindParticipantProps = {
  /** What is typed, which the screen holds in its own state only, never the address, storage or a query key. */
  value: string;
  onChange: (value: string) => void;
  /** Nothing in the list shown matches what is typed, so the screen shows the no-match sentence in its place. */
  noMatch: boolean;
};

/**
 * Find a participant (search-filter.md): a `<search>` landmark with no form, so Enter does nothing, holding the field
 * and, while it holds text, Clear. It filters the list that the screen holds as each key is typed, which the screen
 * does with `matchesName`. Its own status region, in the page from the first render, says only the no-match sentence,
 * once the typing has paused, the same words as the screen shows in place of the list.
 */
export function FindParticipant({ value, onChange, noMatch }: FindParticipantProps) {
  const inputId = useId();
  const descriptionId = useId();
  const input = useRef<HTMLInputElement>(null);
  const [announcement, setAnnouncement] = useState('');
  const typed = value.trim();

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setAnnouncement(noMatch ? msg.noParticipantMatches(typed) : '');
    }, announceDelay);
    return () => window.clearTimeout(timer);
  }, [noMatch, typed]);

  return (
    <search className={styles.search}>
      <label htmlFor={inputId} className={styles.label}>
        {strings.findParticipant.label}
      </label>
      <p id={descriptionId} className="visually-hidden">
        {strings.findParticipant.description}
      </p>
      <div className={styles.row}>
        <span className={styles.field}>
          <svg className={styles.icon} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
            <circle cx="8.5" cy="8.5" r="5.5" fill="none" stroke="currentColor" strokeWidth="2" />
            <path d="m12.5 12.5 5 5" fill="none" stroke="currentColor" strokeWidth="2" />
          </svg>
          <input
            ref={input}
            id={inputId}
            type="text"
            className={`${textInput.input} ${styles.input}`}
            value={value}
            onChange={(event) => onChange(event.currentTarget.value)}
            aria-describedby={descriptionId}
            autoComplete="off"
            autoCorrect="off"
            spellCheck={false}
          />
        </span>
        {value !== '' && (
          <Button
            className={styles.clear}
            // Focus never leaves the field, so a phone keeps its keyboard open.
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => {
              onChange('');
              input.current?.focus();
            }}
          >
            {strings.findParticipant.clear}{' '}
            <span className="visually-hidden">{strings.findParticipant.clearHidden}</span>
          </Button>
        )}
      </div>
      <p role="status" className="visually-hidden">
        {announcement}
      </p>
    </search>
  );
}
