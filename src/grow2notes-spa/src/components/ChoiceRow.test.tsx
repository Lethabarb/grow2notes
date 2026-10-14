import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { ChoiceRow } from './ChoiceRow.tsx';

/** Two radios of one set, as a form holds them. */
function Choices() {
  const [chosen, setChosen] = useState('pdf');
  return (
    <fieldset>
      <legend>Format</legend>
      <ChoiceRow
        id="format-pdf"
        name="format"
        label="PDF"
        hint="For reading and printing"
        checked={chosen === 'pdf'}
        onChange={() => setChosen('pdf')}
      />
      <ChoiceRow
        id="format-word"
        name="format"
        label="Word"
        hint="For changing before it is sent"
        checked={chosen === 'word'}
        onChange={() => setChosen('word')}
      />
    </fieldset>
  );
}

describe('ChoiceRow', () => {
  it('is a native radio named by its label alone and described by its hint, which is outside the label', () => {
    render(<Choices />);

    const pdf = screen.getByRole('radio', { name: 'PDF' });
    expect(pdf).toHaveAttribute('type', 'radio');
    expect(pdf).toHaveAttribute('name', 'format');
    expect(pdf).toHaveAccessibleDescription('For reading and printing');
    expect(pdf.closest('label')).not.toHaveTextContent('For reading and printing');
    expect(pdf).toBeChecked();
    expect(pdf).not.toHaveAttribute('disabled');
  });

  it('is chosen by a tap anywhere on its row, its text included', () => {
    render(<Choices />);

    fireEvent.click(screen.getByText('Word'));

    expect(screen.getByRole('radio', { name: 'Word' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'PDF' })).not.toBeChecked();
  });

  it('draws the radio itself, 40 px, in a row at least 56 px tall, never hiding the native control', () => {
    render(<Choices />);

    const radio = screen.getByRole('radio', { name: 'PDF' });
    expect(radio).toHaveStyle({ appearance: 'none', inlineSize: '2.5rem', blockSize: '2.5rem' });
    expect(radio).toBeVisible();
    expect(radio.closest('label')).toHaveStyle({ minBlockSize: 'var(--target-row)' });
  });
});
