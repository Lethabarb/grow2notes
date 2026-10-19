import { render, screen, within } from '@testing-library/react';
import type { ReactNode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { describe, expect, it } from 'vitest';
import { LinkRow } from './LinkRow.tsx';
import { LinkRowList } from './LinkRowList.tsx';

/** Renders `rows` in a `LinkRowList`, `ordered` or not, on a router of its own, and gives the list. */
function renderList(rows: ReactNode, ordered = false): HTMLElement {
  const router = createMemoryRouter([{ path: '*', element: <LinkRowList ordered={ordered}>{rows}</LinkRowList> }]);
  render(<RouterProvider router={router} />);
  return screen.getByRole('list');
}

describe('LinkRow', () => {
  it('is an item of a role="list" list, holding one link, named by its name alone, to its address', () => {
    const list = renderList(
      <LinkRow
        to="/made-up/4e7a2c1b"
        name="Jane Citizen"
        returnKey="4e7a2c1b"
        detail={['Wednesday 30 September 2026']}
      />,
    );

    expect(list.tagName).toBe('UL');
    expect(list).toHaveAttribute('role', 'list');
    const [row] = within(list).getAllByRole('listitem');
    const links = within(row).getAllByRole('link');
    expect(links).toHaveLength(1);
    expect(links[0]).toHaveAccessibleName('Jane Citizen');
    expect(links[0]).toHaveAttribute('href', '/made-up/4e7a2c1b');
  });

  it('gives its link its return key, by which the shell finds the row again', () => {
    const list = renderList(<LinkRow to="/made-up/4e7a2c1b" name="Jane Citizen" returnKey="4e7a2c1b" />);

    expect(within(list).getByRole('link')).toHaveAttribute('data-return-key', '4e7a2c1b');
  });

  it('describes its link by its detail, each line on its own, with a pause between them for screen readers', () => {
    const list = renderList(
      <LinkRow
        to="/made-up/4e7a2c1b"
        name="Jane Citizen"
        returnKey="4e7a2c1b"
        detail={['jane@example.org', 'Second line']}
      />,
    );

    const link = within(list).getByRole('link');
    expect(link).toHaveAccessibleDescription('jane@example.org, Second line');
    const detail = document.getElementById(link.getAttribute('aria-describedby') ?? '');
    expect(detail?.tagName).toBe('P');
    // The comma goes with the line before it, and only screen readers get it.
    expect(Array.from(detail?.children ?? [], (line) => line.innerHTML)).toEqual([
      'jane@example.org<span class="visually-hidden">,</span>',
      'Second line',
    ]);
  });

  // chronological-list.md *Anatomy* 3, as a To review row's Reason and "Flag removed in a later edit".
  it('puts its plain lines after its detail, each a <p> in reading order, in the row but not its description', () => {
    const list = renderList(
      <LinkRow
        look="date"
        to="/made-up/9c1d/notes/2026-10-01"
        name="Lee Wong"
        returnKey="9c1d/2026-10-01"
        detail={['Alex P. · flagged 11:20 am']}
        extra={['Reason: Missed lunch.', 'Flag removed in a later edit']}
      />,
      true,
    );

    const [row] = within(list).getAllByRole('listitem');
    const link = within(row).getByRole('link');
    expect(link).toHaveAccessibleName('Lee Wong');
    expect(link).toHaveAccessibleDescription('Alex P. · flagged 11:20 am');
    const [detail, ...extra] = Array.from(row.querySelectorAll('p'));
    expect(detail).toHaveAttribute('id', link.getAttribute('aria-describedby'));
    expect(extra.map((line) => line.textContent)).toEqual(['Reason: Missed lunch.', 'Flag removed in a later edit']);
    for (const line of extra) {
      expect(line).not.toHaveAttribute('id');
      // Not positioned, so that the link's ::after over the whole row stays over it, and a tap on it opens the row.
      expect(line).toHaveStyle({ position: 'static', overflowWrap: 'anywhere' });
    }
  });

  it('has no description, detail or plain lines without them', () => {
    const list = renderList(<LinkRow to="/made-up/4e7a2c1b" name="Jane Citizen" returnKey="4e7a2c1b" />);

    const link = within(list).getByRole('link');
    expect(link).not.toHaveAttribute('aria-describedby');
    expect(list.querySelector('p')).toBeNull();
  });

  it('has a chevron that screen readers and the tab order skip', () => {
    const list = renderList(<LinkRow to="/made-up/4e7a2c1b" name="Jane Citizen" returnKey="4e7a2c1b" />);

    const chevron = list.querySelector('svg');
    expect(chevron).toHaveAttribute('aria-hidden', 'true');
    expect(chevron).toHaveAttribute('focusable', 'false');
  });

  it('is an item of an <ol> for rows whose order is their meaning', () => {
    const list = renderList(<LinkRow to="/made-up/4e7a2c1b" name="Jane Citizen" returnKey="4e7a2c1b" />, true);

    expect(list.tagName).toBe('OL');
    expect(list).toHaveAttribute('role', 'list');
  });

  // participant-notes.md component 5: the note's date, with no chevron; its link's look is checked end to end.
  it("names a note's row by its date in a <time>, with no chevron", () => {
    const list = renderList(
      <LinkRow
        look="date"
        to="/made-up/notes/2026-10-01"
        name={<time dateTime="2026-10-01">Thursday 1 October 2026</time>}
        returnKey="2026-10-01"
        detail={['Submitted']}
      />,
      true,
    );

    const link = within(list).getByRole('link');
    expect(link).toHaveAccessibleName('Thursday 1 October 2026');
    expect(link.querySelector('time')).toHaveAttribute('datetime', '2026-10-01');
    expect(link).toHaveAccessibleDescription('Submitted');
    expect(list.querySelector('svg')).toBeNull();
  });
});
