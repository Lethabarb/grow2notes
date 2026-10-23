import { render, screen } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { describe, expect, it } from 'vitest';
import { LinkRow } from './LinkRow.tsx';
import { LinkRowList } from './LinkRowList.tsx';

/** Renders a `LinkRowList` of one row, with the props given, on a router of its own, and gives the list. */
function renderList(props: { ordered?: boolean; reversed?: boolean }): HTMLElement {
  const router = createMemoryRouter([
    {
      path: '*',
      element: (
        <LinkRowList {...props}>
          <LinkRow to="/made-up/2" name="Version 2" returnKey="2" />
        </LinkRowList>
      ),
    },
  ]);
  render(<RouterProvider router={router} />);
  return screen.getByRole('list');
}

describe('LinkRowList', () => {
  it.each([
    { props: {}, tag: 'UL' },
    { props: { ordered: true }, tag: 'OL' },
    // Only an ordered list has numbers to count down.
    { props: { reversed: true }, tag: 'UL' },
  ])('is a <$tag> with no reversed attribute for $props', ({ props, tag }) => {
    const list = renderList(props);

    expect(list.tagName).toBe(tag);
    expect(list).toHaveAttribute('role', 'list');
    expect(list).not.toHaveAttribute('reversed');
  });

  it('is an <ol reversed> when ordered and reversed, for numbers that count down', () => {
    const list = renderList({ ordered: true, reversed: true });

    expect(list.tagName).toBe('OL');
    expect(list).toHaveAttribute('reversed');
    expect(list).toHaveAttribute('role', 'list');
  });
});
