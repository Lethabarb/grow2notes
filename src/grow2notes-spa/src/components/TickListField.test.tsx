import { fireEvent, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { TickListField, type TickItem } from './TickListField.tsx';

// note-form.md's made-up goals, with one of A6's 200 characters, unbroken.
const breakfast = { id: '0b2f6c1e-4d38-4a95-9e07-6c1d8f3a5b24', text: 'Makes own breakfast' };
const bus = { id: '5e9a3d7c-1b46-4f82-a0d3-8c2e6b4f1a97', text: 'Catches bus to day program' };
const unbroken = { id: '9c4d1f7a-3e25-4b68-8d0f-2a7e5c9b3d16', text: 'x'.repeat(200) };

type FieldProps = {
  items: readonly TickItem[];
  initiallyTicked?: readonly string[];
  onToggle?: (id: string, isTicked: boolean) => void;
};

/** Goals' list, its ticks kept as the note form keeps them, which a test may also watch. */
function Goals({ items, initiallyTicked = [], onToggle }: FieldProps) {
  const [ticked, setTicked] = useState<ReadonlySet<string>>(() => new Set(initiallyTicked));
  return (
    <TickListField
      idPrefix="goal"
      heading="1. Goals"
      hint="Ticked means reached."
      emptyText="No goals set up for Jane yet. A manager can add them."
      items={items}
      ticked={ticked}
      onToggle={(id, isTicked) => {
        onToggle?.(id, isTicked);
        setTicked((current) => {
          const next = new Set(current);
          if (isTicked) {
            next.add(id);
          } else {
            next.delete(id);
          }

          return next;
        });
      }}
    />
  );
}

describe('TickListField', () => {
  it('is a fieldset whose legend is the <h2> and whose description is the hint under it', () => {
    render(<Goals items={[breakfast, bus]} />);

    const group = screen.getByRole('group', { name: '1. Goals' });
    expect(group.tagName).toBe('FIELDSET');
    expect(group).toHaveAccessibleDescription('Ticked means reached.');
    const legend = group.querySelector('legend')!;
    expect(within(legend).getByRole('heading', { level: 2 })).toHaveTextContent('1. Goals');
    expect(legend.textContent).toBe('1. Goals');
    expect(screen.getByText('Ticked means reached.')).toHaveAttribute('id', 'goal-hint');
  });

  it('gives each item a row that is all its <label>, round its own native box, in the order given', () => {
    render(<Goals items={[breakfast, bus]} initiallyTicked={[bus.id]} />);

    const boxes = screen.getAllByRole('checkbox');
    expect(boxes.map((box) => box.id)).toEqual([`goal-${breakfast.id}`, `goal-${bus.id}`]);
    expect(boxes.map((box) => (box as HTMLInputElement).checked)).toEqual([false, true]);
    for (const [index, item] of [breakfast, bus].entries()) {
      const box = boxes[index];
      expect(box).toHaveAccessibleName(item.text);
      expect(box).toHaveAttribute('type', 'checkbox');
      expect(box).toHaveAttribute('value', item.id);
      const row = box.closest('label')!;
      expect(row).toHaveAttribute('for', box.id);
      expect(row.textContent).toBe(item.text);
    }
  });

  it('ticks and unticks a box when its words are clicked, as a tap on the row does, with no handler of its own', () => {
    const onToggle = vi.fn();
    render(<Goals items={[breakfast, bus]} onToggle={onToggle} />);
    const box = screen.getByRole('checkbox', { name: breakfast.text });

    fireEvent.click(screen.getByText(breakfast.text));
    expect(box).toBeChecked();
    fireEvent.click(screen.getByText(breakfast.text));
    expect(box).not.toBeChecked();

    expect(onToggle.mock.calls).toEqual([
      [breakfast.id, true],
      [breakfast.id, false],
    ]);
  });

  it('wraps long wording anywhere rather than cutting it short', () => {
    render(<Goals items={[unbroken]} />);

    expect(screen.getByText(unbroken.text)).toHaveStyle({ overflowWrap: 'anywhere' });
    expect(screen.getByText(unbroken.text)).not.toHaveStyle({ textOverflow: 'ellipsis' });
  });

  it('shows the heading and the sentence alone, with no fieldset, when there are no items', () => {
    const { container } = render(<Goals items={[]} />);

    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('1. Goals');
    expect(screen.getByText('No goals set up for Jane yet. A manager can add them.')).toBeInTheDocument();
    expect(container.querySelector('fieldset')).toBeNull();
    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.queryByText('Ticked means reached.')).toBeNull();
  });
});
