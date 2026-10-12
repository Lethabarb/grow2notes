import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { OneTimeCodeField } from './OneTimeCodeField.tsx';

type FormProps = { error?: string; onChange?: (value: string) => void; onSubmit?: () => void };

/** A made-up one-field form round the field, as view G and sign-in's code step hold it. */
function Form({ error, onChange, onSubmit }: FormProps) {
  const [value, setValue] = useState('');
  return (
    <form
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit?.();
      }}
    >
      <OneTimeCodeField
        id="code"
        label="Made-up label"
        hint="Made-up hint"
        error={error}
        value={value}
        onChange={(next) => {
          setValue(next);
          onChange?.(next);
        }}
      />
      <button type="submit">Made-up button</button>
    </form>
  );
}

function field(): HTMLInputElement {
  return screen.getByLabelText<HTMLInputElement>('Made-up label');
}

describe('OneTimeCodeField', () => {
  it('is one labelled text field for a number pad and a filled-in code, described by its hint', () => {
    render(<Form />);

    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
    expect(screen.getAllByRole('textbox')).toEqual([field()]);
    expect(field()).toHaveAttribute('id', 'code');
    expect(field()).toHaveAttribute('name', 'code');
    expect(field()).toHaveAttribute('type', 'text');
    expect(field()).toHaveAttribute('inputmode', 'numeric');
    expect(field()).toHaveAttribute('autocomplete', 'one-time-code');
    expect(field()).toHaveAttribute('enterkeyhint', 'go');
    expect(field()).toHaveAttribute('spellcheck', 'false');
    expect(field()).toHaveAttribute('autocapitalize', 'off');
    expect(field()).toHaveAttribute('autocorrect', 'off');
    expect(field()).toHaveAttribute('aria-describedby', 'code-hint');
    expect(field()).toHaveAccessibleDescription('Made-up hint');
    expect(field()).not.toHaveAttribute('aria-invalid');
  });

  it('has no maxLength, pattern or placeholder, which would cut, refuse or pass for a code', () => {
    render(<Form />);

    expect(field()).not.toHaveAttribute('maxlength');
    expect(field()).not.toHaveAttribute('pattern');
    expect(field()).not.toHaveAttribute('placeholder');
  });

  it('is large, bold and about as wide as the code, but no wider than its column, and 48 px tall', () => {
    render(<Form />);

    expect(field()).toHaveStyle({
      inlineSize: '10ch',
      maxInlineSize: '100%',
      minBlockSize: 'var(--target-button)',
      fontSize: 'var(--font-size-h2)',
      fontWeight: '700',
      fontVariantNumeric: 'tabular-nums',
      letterSpacing: '0.15em',
    });
  });

  it('gives what was typed or pasted as it is, never reformatted', () => {
    const onChange = vi.fn<(value: string) => void>();
    render(<Form onChange={onChange} />);

    fireEvent.change(field(), { target: { value: ' 123-4 56 ' } });

    expect(onChange).toHaveBeenCalledExactlyOnceWith(' 123-4 56 ');
    expect(field()).toHaveValue(' 123-4 56 ');
  });

  it('never submits its form by itself, however many digits are typed', () => {
    const onSubmit = vi.fn<() => void>();
    render(<Form onSubmit={onSubmit} />);

    for (const typed of ['1', '123', '123456', '1234567']) {
      fireEvent.change(field(), { target: { value: typed } });
    }

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('is described by its hint, then its message, and marked invalid while it has one', () => {
    render(<Form error="Made-up message" />);

    expect(field()).toHaveAttribute('aria-describedby', 'code-hint code-error');
    expect(field()).toHaveAccessibleDescription('Made-up hint Error: Made-up message');
    expect(field()).toHaveAttribute('aria-invalid', 'true');
  });
});
