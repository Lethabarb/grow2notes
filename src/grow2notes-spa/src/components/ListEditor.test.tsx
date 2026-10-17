import { QueryClientProvider, queryOptions, type QueryClient } from '@tanstack/react-query';
import { act, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../api/queryClient.ts';
import { ListEditor, type ListRow } from './ListEditor.tsx';

const queryKey = ['admin', 'goals', '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95'];

const help =
  'Changes apply to notes started from now on. Notes already started or submitted keep the wording they were written '
  + 'with.';
const empty = 'No goals yet. Notes will show an empty Goals section.';

/** A made-up goal (A35), active unless it has the time it was archived at. */
function goal(id: string, text: string, sortOrder: number, archivedAtUtc: string | null = null): ListRow {
  return { id, text, sortOrder, archivedAtUtc, rowVersion: `AAAAAAAAB${id}=` };
}

const breakfast = goal('1', 'Makes own breakfast', 0);
const bus = goal('2', 'Catches the bus to day program and phones when she arrives', 1);
const sister = goal('3', 'Phones her sister', 2);
// Archived in this order, and so shown the other way round. The later one is half a second after the earlier, which
// a comparison of the two as text would put first, as "Z" sorts after ".".
const washing = goal('4', 'Uses the washing machine', 3, '2026-10-05T01:00:00Z');
const shop = goal('5', 'Walks to the shop', 4, '2026-10-05T01:00:00.5Z');

/**
 * Renders the editor for goals, its list in the query already, as its page puts it there, under a query client of its
 * own; gives the client, and the query's function, which the editor must never call.
 */
function renderEditor(rows: ListRow[], readOnly = false): { client: QueryClient; queryFn: () => Promise<ListRow[]> } {
  const client = createQueryClient();
  client.setQueryData(queryKey, rows);
  const queryFn = vi.fn<() => Promise<ListRow[]>>();
  render(
    <QueryClientProvider client={client}>
      <ListEditor kind="goal" api={{ query: queryOptions({ queryKey, queryFn }) }} readOnly={readOnly} />
    </QueryClientProvider>,
  );
  return { client, queryFn };
}

/** The numbered list of active goals, or `null` while there is none. */
function activeList(): HTMLOListElement | null {
  return document.querySelector('ol');
}

/** Each active goal's list item. */
function activeItems(): HTMLLIElement[] {
  return Array.from(activeList()?.children ?? [], (item) => item as HTMLLIElement);
}

/** The archived section, or `null` while it is left out. */
function archivedSection(): HTMLDetailsElement | null {
  return document.querySelector('details');
}

/** The archived goals' texts, in the order shown. */
function archivedTexts(): string[] {
  return Array.from(archivedSection()?.querySelectorAll('ul > li') ?? [], (item) => item.textContent);
}

describe('ListEditor', () => {
  it('shows the help text, then the active goals as a numbered list, each its text and then its empty alert', () => {
    renderEditor([breakfast, bus, sister]);

    const helpText = screen.getByText(help);
    expect(helpText.tagName).toBe('P');
    expect(helpText.compareDocumentPosition(activeList()!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(activeItems().map((item) => item.tagName)).toEqual(['LI', 'LI', 'LI']);
    for (const [index, item] of activeItems().entries()) {
      const text = within(item).getByText([breakfast, bus, sister][index].text);
      const alert = within(item).getByRole('alert');
      expect(alert).toBeEmptyDOMElement();
      expect(text.compareDocumentPosition(alert) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  // Rows out of SortOrder, as no answer of the API's is, show that the editor keeps the list's order: the API breaks
  // ties in SortOrder as the reorder reads the goals, which the client cannot do again.
  it('keeps the order the list gives, never sorting it', () => {
    renderEditor([{ ...sister, sortOrder: 1 }, { ...breakfast, sortOrder: 0 }, { ...bus, sortOrder: 2 }]);

    expect(activeItems().map((item) => item.textContent)).toEqual([sister.text, breakfast.text, bus.text]);
  });

  it("wraps a goal's text anywhere, so that a long word cannot push the page sideways", () => {
    renderEditor([goal('6', 'Wolfeschlegelsteinhausenbergerdorff-and-a-pasted-link', 0)]);

    expect(screen.getByText('Wolfeschlegelsteinhausenbergerdorff-and-a-pasted-link')).toHaveStyle({
      overflowWrap: 'anywhere',
    });
  });

  it('shows the empty sentence in place of the list, and no archived section, when there are no goals', () => {
    renderEditor([]);

    expect(screen.getByText(empty).tagName).toBe('P');
    expect(activeList()).toBeNull();
    expect(archivedSection()).toBeNull();
    expect(screen.getByText(help)).toBeInTheDocument();
  });

  it('shows the empty sentence, then the archived section, when every goal is archived', () => {
    renderEditor([washing, shop]);

    expect(activeList()).toBeNull();
    expect(screen.getByText(empty).compareDocumentPosition(archivedSection()!) & Node.DOCUMENT_POSITION_FOLLOWING)
      .toBeTruthy();
  });

  it('lists the archived goals newest archived first, after the list, in a closed details with a plain summary', () => {
    renderEditor([breakfast, washing, shop, sister]);

    const details = archivedSection()!;
    expect(activeItems().map((item) => item.textContent)).toEqual([breakfast.text, sister.text]);
    expect(activeList()!.compareDocumentPosition(details) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(details.open).toBe(false);
    expect(details).not.toHaveAttribute('open');
    const summary = details.querySelector('summary')!;
    expect(summary).toBe(details.firstElementChild);
    expect(summary.textContent).toBe('Archived goals');
    expect(summary.children).toHaveLength(0);
    expect(archivedTexts()).toEqual([shop.text, washing.text]);
    expect(within(details).queryByRole('alert')).toBeNull();
  });

  // Uncontrolled, so that React never closes what the person opened.
  it('leaves the archived section as the person left it when the list changes', async () => {
    const { client } = renderEditor([breakfast, washing]);
    archivedSection()!.open = true;

    client.setQueryData(queryKey, [breakfast, washing, shop]);
    // TanStack tells the editor on a timeout of 0.
    await act(() => new Promise((resolve) => setTimeout(resolve, 0)));

    expect(archivedTexts()).toEqual([shop.text, washing.text]);
    expect(archivedSection()!.open).toBe(true);
  });

  it('read-only, shows the goals as numbered text and the archived ones as text, with no help, alert or button', () => {
    renderEditor([breakfast, washing, sister, shop], true);

    expect(screen.queryByText(help)).toBeNull();
    expect(activeItems().map((item) => item.textContent)).toEqual([breakfast.text, sister.text]);
    expect(archivedTexts()).toEqual([shop.text, washing.text]);
    expect(screen.queryAllByRole('alert')).toEqual([]);
    expect(screen.queryAllByRole('button')).toEqual([]);
    expect(screen.queryAllByRole('textbox')).toEqual([]);
    expect(document.querySelector('form')).toBeNull();
  });

  it('read-only, shows the empty sentence when there are no goals', () => {
    renderEditor([], true);

    expect(screen.getByText(empty)).toBeInTheDocument();
    expect(screen.queryByText(help)).toBeNull();
  });

  it('never fetches the list as it mounts, as its page has put it in the query', async () => {
    const { queryFn } = renderEditor([breakfast]);

    await act(() => new Promise((resolve) => setTimeout(resolve, 0)));

    expect(queryFn).not.toHaveBeenCalled();
  });
});
