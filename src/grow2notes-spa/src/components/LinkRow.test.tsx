import { render, screen, within } from '@testing-library/react';
import type { ReactNode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { describe, expect, it } from 'vitest';
import { LinkRow } from './LinkRow.tsx';
import { LinkRowList } from './LinkRowList.tsx';

/** Renders `rows` in a `LinkRowList`, on a router of its own, and gives the list. */
function renderList(rows: ReactNode): HTMLElement {
  const router = createMemoryRouter([{ path: '*', element: <LinkRowList>{rows}</LinkRowList> }]);
  render(<RouterProvider router={router} />);
  return screen.getByRole('list');
}

describe('LinkRow', () => {
  it('is an item of a role="list" list, holding one link, named by its name alone, to its address', () => {
    const list = renderList(
      <LinkRow to="/made-up/4e7a2c1b" name="Jane Citizen" detail={['Wednesday 30 September 2026']} />,
    );

    expect(list).toHaveAttribute('role', 'list');
    const [row] = within(list).getAllByRole('listitem');
    const links = within(row).getAllByRole('link');
    expect(links).toHaveLength(1);
    expect(links[0]).toHaveAccessibleName('Jane Citizen');
    expect(links[0]).toHaveAttribute('href', '/made-up/4e7a2c1b');
  });

  it('describes its link by its detail, each line on its own, with a pause between them for screen readers', () => {
    const list = renderList(
      <LinkRow to="/made-up/4e7a2c1b" name="Jane Citizen" detail={['jane@example.org', 'Second line']} />,
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

  it('has no description and no detail without one', () => {
    const list = renderList(<LinkRow to="/made-up/4e7a2c1b" name="Jane Citizen" />);

    const link = within(list).getByRole('link');
    expect(link).not.toHaveAttribute('aria-describedby');
    expect(list.querySelector('p')).toBeNull();
  });

  it('has a chevron that screen readers and the tab order skip', () => {
    const list = renderList(<LinkRow to="/made-up/4e7a2c1b" name="Jane Citizen" />);

    const chevron = list.querySelector('svg');
    expect(chevron).toHaveAttribute('aria-hidden', 'true');
    expect(chevron).toHaveAttribute('focusable', 'false');
  });
});
