import { render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { describe, expect, it } from 'vitest';
import { strings } from '../copy/strings.ts';
import { ChronoList } from './ChronoList.tsx';
import { LinkRow } from './LinkRow.tsx';

/** A made-up row of a whole list, such as To review's, keyed as its participant and note date are. */
type Row = { key: string; name: string };

const emptyText = 'No flagged notes to review.';

/** Renders `rows` as a whole list, with no `older`, on a router of its own. */
function renderWhole(rows: readonly Row[]) {
  const list = (
    <ChronoList
      items={rows}
      getKey={(row) => row.key}
      emptyText={emptyText}
      renderRow={(row) => <LinkRow look="date" to={`/made-up/${row.key}`} name={row.name} returnKey={row.key} />}
    />
  );
  render(<RouterProvider router={createMemoryRouter([{ path: '*', element: list }])} />);
}

// Past notes' paged mode is checked through its page (PastNotesPage.test.tsx).
describe('ChronoList, whole', () => {
  it('lists its rows in the order given, never sorted again, with no Show older or status line', () => {
    // In none of name, key or date order, as To review's oldest flag can be on any note.
    renderWhole([
      { key: '9c1d/2026-10-01', name: 'Lee Wong' },
      { key: '4e7a/2026-09-29', name: 'Sam Taylor' },
      { key: '2b8f/2026-09-30', name: 'Jane Citizen' },
    ]);

    const list = screen.getByRole('list');
    expect(list.tagName).toBe('OL');
    expect(within(list).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'Lee Wong',
      'Sam Taylor',
      'Jane Citizen',
    ]);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.queryByText(strings.showOlder.button)).toBeNull();
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('says its empty sentence alone, with no list, button or status line, when it has no rows', () => {
    renderWhole([]);

    expect(screen.getByText(emptyText).tagName).toBe('P');
    expect(screen.queryByRole('list')).toBeNull();
    expect(document.querySelector('ol')).toBeNull();
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.queryByRole('status')).toBeNull();
  });
});
