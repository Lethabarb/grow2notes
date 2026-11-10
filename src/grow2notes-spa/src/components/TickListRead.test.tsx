import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import styles from './TickList.module.css';
import { TickListRead, TickRowRead, type TickedItem } from './TickListRead.tsx';

// note-form.md's made-up goals, as a submitted version holds them, with one of A6's 200 characters, unbroken.
const breakfast = { text: 'Makes own breakfast', isTicked: true };
const bus = { text: 'Catches bus to day program', isTicked: false };
const unbroken = { text: 'x'.repeat(200), isTicked: false };

function Goals({ items }: { items: readonly TickedItem[] }) {
  return <TickListRead heading="Short term goals" emptyText="No goals set" items={items} />;
}

describe('TickListRead', () => {
  it('has an <h2> of its own name, as each group of the common items has', () => {
    render(<TickListRead heading="Community outing" items={[breakfast]} />);

    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Community outing');
    expect(screen.queryByRole('heading', { level: 3 })).toBeNull();
  });

  it('shows the heading alone, with no sentence, for no items and no emptyText', () => {
    const { container } = render(<TickListRead heading="Community outing" items={[]} />);

    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument();
    expect(container.querySelector('p, ul')).toBeNull();
  });

  it('says Details: and the details under an item that has them, after its tick state', () => {
    render(<Goals items={[{ text: 'Other', isTicked: true, detail: 'Went to the library' }, bus]} />);

    const [other, notOther] = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(other.textContent).toBe('Other, ticked,Details: Went to the library');
    expect(within(other).getByText('Details: Went to the library')).toBeInTheDocument();
    expect(notOther.textContent).toBe('Catches bus to day program, not ticked');
  });

  it('lists each item under the <h2>, in the order given, read as its wording, then ticked or not ticked', () => {
    render(<Goals items={[breakfast, bus]} />);

    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Short term goals');
    const list = screen.getByRole('list');
    expect(list).toHaveAttribute('role', 'list');
    expect(within(list).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Makes own breakfast, ticked',
      'Catches bus to day program, not ticked',
    ]);
  });

  // Never a disabled or read-only box, which a screen reader would offer as a control (checkbox-list.md).
  it('is text, with no form control, tab stop or role of a checkbox', () => {
    const { container } = render(<Goals items={[breakfast, bus]} />);

    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(container.querySelector('input, button, [tabindex], [aria-checked], [aria-readonly]')).toBeNull();
  });

  it('draws one box for each item, hidden from screen readers, with a tick inside on a ticked item alone', () => {
    render(<Goals items={[breakfast, bus]} />);

    const icons = screen.getAllByRole('listitem').map((item) => item.querySelector('svg')!);
    for (const icon of icons) {
      expect(icon).toHaveAttribute('aria-hidden', 'true');
      expect(icon).toHaveAttribute('focusable', 'false');
      expect(icon.querySelectorAll('rect')).toHaveLength(1);
    }

    expect(icons.map((icon) => icon.querySelectorAll('path').length)).toEqual([1, 0]);
  });

  it('wraps a long unbroken wording anywhere, never cutting it short', () => {
    render(<Goals items={[unbroken]} />);

    const wording = screen.getByText(unbroken.text, { exact: false });
    expect(wording).toHaveClass(styles.readText);
    expect(wording).toHaveStyle({ overflowWrap: 'anywhere' });
    expect(wording.textContent).toContain(unbroken.text);
  });

  it('says its empty sentence under the heading in place of a list when it has no items', () => {
    render(<Goals items={[]} />);

    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Short term goals');
    expect(screen.getByText('No goals set').tagName).toBe('P');
    expect(screen.queryByRole('list')).toBeNull();
  });
});

// A draft's flag as a manager reads it (participant-notes.md *Someone else's draft, manager*).
describe('TickRowRead', () => {
  it.each([
    { isTicked: true, read: 'Flag for manager, ticked', ticks: 1 },
    { isTicked: false, read: 'Flag for manager, not ticked', ticks: 0 },
  ])('is one line, read as its wording, then "$read", with no list or control', ({ isTicked, read, ticks }) => {
    const { container } = render(<TickRowRead text="Flag for manager" isTicked={isTicked} />);

    const row = container.firstElementChild!;
    expect(row.tagName).toBe('P');
    expect(row).toHaveClass(styles.readItem, styles.readRow);
    expect(row.textContent).toBe(read);
    expect(row.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    expect(row.querySelectorAll('svg path')).toHaveLength(ticks);
    expect(screen.queryByRole('list')).toBeNull();
    expect(container.querySelector('input, button, [tabindex], [aria-checked], [aria-readonly]')).toBeNull();
  });
});
