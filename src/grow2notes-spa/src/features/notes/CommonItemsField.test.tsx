import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { CommonItemsField } from './CommonItemsField.tsx';
import type { DraftCommonItem, DraftGroup } from './noteQueries.ts';

// group-picker.md's made-up groups: Every note, then three in the managers' order, and one with no items.
function item(id: string, text: string, needsDetail = false): DraftCommonItem {
  return { id, text, isTicked: false, needsDetail, detail: null };
}

function group(id: string, name: string, items: DraftCommonItem[], isEveryNote = false): DraftGroup {
  return { id, name, isEveryNote, isPicked: false, items };
}

const medication = item('c1a4e7b2-3f58-4d19-8b60-9e2d7a5c3f81', 'Medication prompted');
const mealPrepared = item('d6b3f8a1-4e29-4c70-9a15-0f7c2e8b4d63', 'Meal prepared');
const trains = item('e8c5a9d3-6b41-4f02-b7e8-1a3d9c6f2b74', 'Travelled by bus or train');
const showered = item('a9e4c6f2-1d75-4b38-8c03-7d2b5a1e9f40', 'Showered');
const other = item('f2d7b4a8-0c63-4e91-a5b9-3c8e1d6a7f25', 'Other (staff to complete if different from above)', true);

const everyNote = group('4b7d1e9a-2c63-4f85-a8b0-6e3f9c1d5a72', 'Every note', [medication, mealPrepared], true);
const outing = group('7c2a8e5d-9b14-4d63-a1f7-4e0b6d3c8a95', 'Community outing', [trains]);
const inHome = group('1e5b8c3f-7a96-4d02-b4e1-2c9f6a0d7b38', 'In-home support', []);
const personalCare = group('b3f6d9a2-5e81-4c47-9d20-8a1c7e4b6f53', 'Personal care', [showered]);

const emptyText = 'No common items set up.';

type Props = Partial<Parameters<typeof CommonItemsField>[0]>;

function renderField(props: Props = {}) {
  const onPick = vi.fn();
  const onToggle = vi.fn();
  const onDetail = vi.fn();
  const view = render(
    <CommonItemsField
      groups={[everyNote, outing, inHome, personalCare]}
      picked={new Set()}
      ticked={new Set()}
      details={{}}
      emptyText={emptyText}
      onPick={onPick}
      onToggle={onToggle}
      onDetail={onDetail}
      {...props}
    />,
  );
  return { ...view, onPick, onToggle, onDetail };
}

/** The wrappers of the groups, by their IDs, in the page's order. */
function wrappers(container: HTMLElement): string[] {
  return [...container.querySelectorAll('[id^="group-"]')].map((element) => element.id);
}

describe('CommonItemsField', () => {
  it('is in this order: its hint, Every note, the picker, then each offered group, each under its own name', () => {
    const { container } = renderField({ picked: new Set([personalCare.id, outing.id]) });

    const order = [...container.querySelectorAll('#common-hint, fieldset, [id^="group-"]')].map((element) =>
      element instanceof HTMLFieldSetElement
        ? `fieldset ${element.getAttribute('aria-describedby')}`
        : element.id || element.tagName,
    );
    expect(order).toEqual([
      'common-hint',
      'fieldset common-hint',
      'fieldset picker-hint',
      `group-${outing.id}`,
      'fieldset common-hint',
      `group-${personalCare.id}`,
      'fieldset common-hint',
    ]);
    expect(screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual([
      'Every note',
      'Which of these happened?',
      'Community outing',
      'Personal care',
    ]);
    expect(screen.queryByRole('heading', { level: 3 })).toBeNull();
    expect(screen.getByText('Ticked means done.')).toHaveAttribute('id', 'common-hint');
    expect(container.firstElementChild!.tagName).toBe('DIV');
  });

  it('keeps the managers order of the wrappers whatever order the groups were picked in', () => {
    const first = renderField({ picked: new Set([outing.id, personalCare.id]) });
    const one = wrappers(first.container);
    first.unmount();
    const second = renderField({ picked: new Set([personalCare.id, outing.id]) });

    expect(one).toEqual([`group-${outing.id}`, `group-${personalCare.id}`]);
    expect(wrappers(second.container)).toEqual(one);
  });

  it('puts Every note first, as an <h2>, and never as a row of the picker', () => {
    renderField();

    const every = screen.getByRole('group', { name: 'Every note' });
    expect(within(every.querySelector('legend')!).getByRole('heading', { level: 2 })).toBeInTheDocument();
    expect(
      within(every)
        .getAllByRole('checkbox')
        .map((box) => box.id),
    ).toEqual([`common-${medication.id}`, `common-${mealPrepared.id}`]);
    const picker = screen.getByRole('group', { name: 'Which of these happened?' });
    expect(
      within(picker)
        .getAllByRole('checkbox')
        .map((box) => box.id),
    ).toEqual([`pick-${outing.id}`, `pick-${personalCare.id}`]);
  });

  it('does not offer a group with no items, in the picker or as a list', () => {
    const { container } = renderField();

    expect(screen.queryByRole('checkbox', { name: inHome.name })).toBeNull();
    expect(screen.queryByRole('heading', { name: inHome.name, hidden: true })).toBeNull();
    expect(container.querySelector(`#group-${inHome.id}`)).toBeNull();
  });

  it('gives no picker when only Every note has items', () => {
    const { container } = renderField({ groups: [everyNote, inHome] });

    expect(screen.getByRole('group', { name: 'Every note' })).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Which of these happened?' })).toBeNull();
    expect(container.querySelectorAll('fieldset')).toHaveLength(1);
    expect(container.querySelector('#picker-hint')).toBeNull();
  });

  it('does not render Every note when it has no items, the picker coming first', () => {
    renderField({ groups: [{ ...everyNote, items: [] }, outing] });

    expect(screen.queryByRole('group', { name: 'Every note' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Every note' })).toBeNull();
    expect(screen.getByRole('group', { name: 'Which of these happened?' })).toBeInTheDocument();
  });

  it.each([
    { named: 'no group', groups: [] },
    { named: 'no item in any group', groups: [{ ...everyNote, items: [] }, inHome] },
  ])('shows the sentence alone, with no heading, fieldset or hint, for $named', ({ groups }) => {
    const { container } = renderField({ groups });

    expect(screen.getByText(emptyText)).toBeInTheDocument();
    expect(container.querySelector('fieldset, #common-hint, #picker-hint')).toBeNull();
    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.queryByRole('heading')).toBeNull();
  });

  it('says which wrapper each picker box controls, and whether it is expanded', () => {
    renderField({ picked: new Set([outing.id]) });

    const boxOuting = screen.getByRole('checkbox', { name: outing.name });
    const boxCare = screen.getByRole('checkbox', { name: personalCare.name });
    expect(boxOuting).toHaveAttribute('aria-controls', `group-${outing.id}`);
    expect(boxCare).toHaveAttribute('aria-controls', `group-${personalCare.id}`);
    expect(boxOuting).toHaveAttribute('aria-expanded', 'true');
    expect(boxOuting).toBeChecked();
    expect(boxCare).toHaveAttribute('aria-expanded', 'false');
    expect(boxCare).not.toBeChecked();
  });

  it('keeps an unpicked wrapper in the page, hidden, with its boxes out of the accessibility tree', () => {
    const { container } = renderField({ picked: new Set([outing.id]) });

    expect(container.querySelector(`#group-${outing.id}`)).not.toHaveAttribute('hidden');
    expect(container.querySelector(`#group-${personalCare.id}`)).toHaveAttribute('hidden');
    expect(screen.getByRole('checkbox', { name: trains.text })).toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: showered.text })).toBeNull();
    expect(screen.getByRole('checkbox', { name: showered.text, hidden: true })).not.toBeChecked();
  });

  it('describes Every note and each group by the section hint, and the picker by its own', () => {
    renderField({ picked: new Set([outing.id]) });

    const picker = screen.getByRole('group', { name: 'Which of these happened?' });
    expect(screen.getByRole('group', { name: 'Every note' })).toHaveAccessibleDescription('Ticked means done.');
    expect(screen.getByRole('group', { name: outing.name })).toHaveAccessibleDescription('Ticked means done.');
    expect(picker).toHaveAccessibleDescription('Tick all that happened. Their items show below.');
    expect(picker.querySelector('legend h2')).not.toBeNull();
  });

  it('reports a pick, with the group, and a tick, with the item', () => {
    const { onPick, onToggle } = renderField({ picked: new Set([outing.id]) });

    fireEvent.click(screen.getByRole('checkbox', { name: personalCare.name }));
    fireEvent.click(screen.getByRole('checkbox', { name: outing.name }));
    fireEvent.click(screen.getByText(trains.text));

    expect(onPick.mock.calls).toEqual([
      [personalCare, true],
      [outing, false],
    ]);
    expect(onToggle.mock.calls).toEqual([[trains.id, true]]);
  });

  it('shows the ticks it is given, in Every note and in a picked group', () => {
    renderField({ picked: new Set([outing.id]), ticked: new Set([mealPrepared.id, trains.id]) });

    expect(screen.getByRole('checkbox', { name: mealPrepared.text })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: medication.text })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: trains.text })).toBeChecked();
  });

  describe('an item that asks for details', () => {
    const asking = group('9a1c5e7b-3d28-4f64-b0a9-5e2c8d1f7b36', 'Further supports', [other]);

    it('shows a Details box directly after the item while it is ticked, named with the item', () => {
      const { rerender, onPick, onToggle, onDetail } = renderField({ groups: [asking], picked: new Set([asking.id]) });
      expect(screen.queryByRole('textbox')).toBeNull();

      rerender(
        <CommonItemsField
          groups={[asking]}
          picked={new Set([asking.id])}
          ticked={new Set([other.id])}
          details={{ [other.id]: 'Went to the library' }}
          emptyText={emptyText}
          onPick={onPick}
          onToggle={onToggle}
          onDetail={onDetail}
        />,
      );

      const box = screen.getByRole('textbox', { name: `Details, ${other.text}` });
      expect(box).toHaveAttribute('id', `detail-${other.id}`);
      expect(box).toHaveValue('Went to the library');
      expect(screen.getByRole('checkbox', { name: other.text }).closest('label')!.nextElementSibling).toContainElement(
        box,
      );
    });

    it('reports each input into the box with the item and the whole text', () => {
      const { onDetail } = renderField({
        groups: [asking],
        picked: new Set([asking.id]),
        ticked: new Set([other.id]),
      });

      fireEvent.change(screen.getByRole('textbox', { name: `Details, ${other.text}` }), {
        target: { value: 'Went to the park' },
      });

      expect(onDetail).toHaveBeenCalledWith(other.id, 'Went to the park');
    });

    it('shows the message it is given for the box, linked to the box by its description', () => {
      renderField({
        groups: [asking],
        picked: new Set([asking.id]),
        ticked: new Set([other.id]),
        detailErrors: { [other.id]: `Enter the details for ${other.text}` },
      });

      const box = screen.getByRole('textbox', { name: `Details, ${other.text}` });
      expect(box).toHaveAttribute('aria-invalid', 'true');
      expect(box).toHaveAccessibleDescription(`Error: Enter the details for ${other.text}`);
    });
  });
});
