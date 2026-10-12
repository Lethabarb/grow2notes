import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { PasswordInput } from './PasswordInput.tsx';

type FormProps = { toggle?: boolean; hint?: string; error?: string; onChange?: (value: string) => void };

/** A made-up form round the field, as sign-in's (with no toggle) or Create a password's (with one) holds it. */
function Form({ toggle = false, hint, error, onChange }: FormProps) {
  const [value, setValue] = useState('');
  return (
    <form noValidate onSubmit={(event) => event.preventDefault()}>
      <PasswordInput
        id="password"
        label="Password"
        hint={hint}
        error={error}
        autoComplete={toggle ? 'new-password' : 'current-password'}
        toggle={toggle}
        value={value}
        onChange={(next) => {
          setValue(next);
          onChange?.(next);
        }}
      />
      <button type="submit">Continue</button>
    </form>
  );
}

function field(): HTMLInputElement {
  return screen.getByLabelText<HTMLInputElement>('Password');
}

describe('PasswordInput', () => {
  it('is, with no toggle, a plain labelled password field with nothing beside it, as sign-in has it (D68)', () => {
    render(<Form />);

    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
    expect(field()).toHaveAttribute('type', 'password');
    expect(field()).toHaveAttribute('name', 'password');
    expect(field()).toHaveAttribute('autocomplete', 'current-password');
    expect(field()).toHaveAttribute('spellcheck', 'false');
    expect(field()).toHaveAttribute('autocapitalize', 'none');
    expect(field()).toHaveAttribute('autocorrect', 'off');
    expect(field()).not.toHaveAttribute('minlength');
    expect(field()).not.toHaveAttribute('maxlength');
    expect(field()).not.toHaveAttribute('aria-describedby');
    expect(field()).not.toHaveAttribute('aria-invalid');
    expect(screen.getAllByRole('button')).toEqual([screen.getByRole('button', { name: 'Continue' })]);
    expect(document.querySelector('[aria-live]')).toBeNull();
  });

  it('gives what was typed as it is, never trimmed', () => {
    const onChange = vi.fn<(value: string) => void>();
    render(<Form onChange={onChange} />);

    fireEvent.change(field(), { target: { value: '  made-up password\t' } });

    expect(onChange).toHaveBeenCalledExactlyOnceWith('  made-up password\t');
    expect(field()).toHaveValue('  made-up password\t');
  });

  it('is described by its hint, then its message, and marked invalid while it has one', () => {
    render(<Form hint="Made-up hint" error="Made-up message" />);

    expect(field()).toHaveAttribute('aria-describedby', 'password-hint password-error');
    expect(field()).toHaveAccessibleDescription('Made-up hint Error: Made-up message');
    expect(field()).toHaveAttribute('aria-invalid', 'true');
  });

  it('says nothing in its polite line when the form is submitted with the password already hidden', () => {
    render(<Form toggle />);
    const line = document.querySelector('[aria-live="polite"]');

    fireEvent.submit(field().form!);

    expect(field()).toHaveAttribute('type', 'password');
    expect(line).toBeEmptyDOMElement();
  });

  it('hides a shown password again however the form is submitted, and says so', () => {
    render(<Form toggle />);
    fireEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(field()).toHaveAttribute('type', 'text');

    // As Enter in the field does, with no press of a button.
    fireEvent.submit(field().form!);

    expect(field()).toHaveAttribute('type', 'password');
    expect(screen.getByRole('button', { name: 'Show password' })).toHaveTextContent('Show');
    expect(document.querySelector('[aria-live="polite"]')).toHaveTextContent('Your password is hidden');
  });
});
