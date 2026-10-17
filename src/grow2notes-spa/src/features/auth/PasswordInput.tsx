import { useEffect, useRef, useState } from 'react';
import { Button } from '../../components/Button.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import textInput from '../../components/textInput.module.css';
import { strings } from '../../copy/strings.ts';
import styles from './PasswordInput.module.css';

type PasswordInputProps = {
  /** The field's id and name, which stay the same across releases, so that password managers keep filling them. */
  id: string;
  label: string;
  /** The label is the page's `<h1>`, as on Create a password (sign-in.md view F). */
  labelIsHeading?: boolean;
  hint?: string;
  /** The field's message once a submit has found one. */
  error?: string;
  /**
   * The field takes focus as its screen arrives, in place of the `<h1>`, as an error summary does (app-shell.md *The
   * route-change rule (one owner)*, 3a): on Create a password, while it shows the server's refusal of the password.
   */
  routeFocus?: boolean;
  /** `new-password` where a password is made, so that a password manager offers one; `current-password` at sign-in. */
  autoComplete: 'new-password' | 'current-password';
  /** The shortest password taken, which guides a password manager's suggestion; the form's `noValidate` stops it. */
  minLength?: number;
  /** The Show/Hide button, which only Create a password has (design.md §4.1; D68). */
  toggle?: boolean;
  /** As typed: a password is never trimmed. */
  value: string;
  onChange: (value: string) => void;
};

/**
 * The one password field (sign-in.md component 7; sign-in-form.md), with no `maxLength`, no spelling, capitals or
 * corrections of the phone's own, and, where the page asks, a Show/Hide button beside it, which wraps under it where
 * the line is too narrow for both. Show/Hide keeps focus, and its polite line, empty until it is first pressed, says
 * what it did; the field goes back to dots when its form is submitted, as GOV.UK's does.
 */
export function PasswordInput({
  id,
  label,
  labelIsHeading = false,
  hint,
  error,
  routeFocus = false,
  autoComplete,
  minLength,
  toggle = false,
  value,
  onChange,
}: PasswordInputProps) {
  const field = useRef<HTMLInputElement>(null);
  const [shown, setShown] = useState(false);
  const [said, setSaid] = useState('');

  useEffect(() => {
    const form = field.current?.form ?? null;
    if (!toggle || form === null) {
      return;
    }

    // Through the form's own event, which a press of its button and Enter in any of its fields both raise.
    const hide = () => {
      if (field.current?.type === 'text') {
        setShown(false);
        setSaid(strings.passwordToggle.hidden);
      }
    };
    form.addEventListener('submit', hide);
    return () => form.removeEventListener('submit', hide);
  }, [toggle]);

  const description = [hint === undefined ? undefined : `${id}-hint`, error === undefined ? undefined : `${id}-error`]
    .filter((part) => part !== undefined)
    .join(' ');
  const input = (
    <input
      ref={field}
      id={id}
      name={id}
      type={shown ? 'text' : 'password'}
      className={`${textInput.input} ${styles.input}`}
      autoComplete={autoComplete}
      minLength={minLength}
      spellCheck={false}
      autoCapitalize="none"
      autoCorrect="off"
      aria-describedby={description === '' ? undefined : description}
      aria-invalid={error === undefined ? undefined : true}
      data-route-focus={routeFocus || undefined}
      value={value}
      onChange={(event) => onChange(event.currentTarget.value)}
    />
  );

  return (
    <FieldGroup fieldId={id} label={label} labelIsHeading={labelIsHeading} hint={hint} error={error}>
      {toggle ? (
        <>
          <div className={styles.row}>
            {input}
            <Button
              className={styles.toggle}
              aria-controls={id}
              aria-label={shown ? strings.passwordToggle.hideName : strings.passwordToggle.showName}
              onClick={() => {
                setSaid(shown ? strings.passwordToggle.hidden : strings.passwordToggle.visible);
                setShown(!shown);
              }}
            >
              {shown ? strings.passwordToggle.hide : strings.passwordToggle.show}
            </Button>
          </div>
          {/* In the page from the first render, as a live region added with its words may not be read out. */}
          <p className="visually-hidden" aria-live="polite">{said}</p>
        </>
      ) : (
        input
      )}
    </FieldGroup>
  );
}
