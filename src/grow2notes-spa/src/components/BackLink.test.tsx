import { act, fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter, Outlet, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { describe, expect, it, onTestFinished } from 'vitest';
import { useRouteChange } from '../routes/routeChange.ts';
import { BackLink } from './BackLink.tsx';

/** The shell's part here: its route-change rule, which keeps the opened-from memory that the back link reads. */
function Shell() {
  useRouteChange(undefined);
  return <Outlet />;
}

/**
 * Made-up pages with a back link to `/list`, or to its Archived view, opened at the last of `entries`, after the others
 * in its history, as after a reload or from a pasted address, with the shell's rule around them.
 */
function renderAt(...entries: string[]) {
  // Each entry with a key of its own, as a browser gives it, where a memory router gives every test's first entry the
  // key "default", under which the shell's memory would carry a row or a position from one test into the next.
  const initialEntries = entries.map((entry) => ({ ...parsePath(entry), key: crypto.randomUUID() }));
  const router = createMemoryRouter(
    [
      {
        element: <Shell />,
        children: [
          { path: '/list', element: <h1>List</h1> },
          { path: '/elsewhere', element: <h1>Elsewhere</h1> },
          { path: '/page/:id', element: <BackLink to="/list">List</BackLink> },
          { path: '/archived/:id', element: <BackLink to="/list?view=archived">List</BackLink> },
        ],
      },
    ],
    { initialEntries, initialIndex: entries.length - 1 },
  );
  render(<RouterProvider router={router} />);
  return router;
}

/** Goes to `path` as an in-app link does, a push, or, with `replace`, in place of the entry shown. */
async function go(router: ReturnType<typeof renderAt>, path: string, replace = false) {
  await act(() => router.navigate(path, { replace }));
}

/** Presses the back link, with a plain click. */
async function pressBackLink() {
  fireEvent.click(screen.getByRole('link', { name: 'List' }));
  await act(async () => {});
}

/**
 * Records whether each click was prevented by the time it reached the window, then prevents it there, as jsdom cannot
 * follow a link to another page.
 */
function watchClicks(): boolean[] {
  const prevented: boolean[] = [];
  const listener = (event: MouseEvent) => {
    prevented.push(event.defaultPrevented);
    event.preventDefault();
  };
  window.addEventListener('click', listener);
  onTestFinished(() => window.removeEventListener('click', listener));
  return prevented;
}

describe('BackLink', () => {
  it('is a link named by its destination alone, to its destination, with a hidden chevron', () => {
    renderAt('/page/1');

    const link = screen.getByRole('link', { name: 'List' });
    expect(link).toHaveAttribute('href', '/list');
    expect(link).not.toHaveAttribute('aria-disabled');
    const chevron = link.querySelector('svg');
    expect(chevron).toHaveAttribute('aria-hidden', 'true');
    expect(chevron).toHaveAttribute('focusable', 'false');
  });

  it('goes back one entry on a plain click, adding none, when the entry before opened the page', async () => {
    const router = renderAt('/list');
    await go(router, '/page/1');

    await pressBackLink();

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/list');
    // Forward reaches the page again, which it would not after a push, as a push adds an entry after the page.
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/page/1');
  });

  it.each([
    { opener: '/list?view=archived', page: '/page/1', kind: 'the Archived view, for a link to the Active view' },
    { opener: '/list', page: '/archived/1', kind: 'the Active view, for a link to the Archived view' },
  ])('goes back one entry to the view of its destination that opened the page, $kind', async ({ opener, page }) => {
    const router = renderAt(opener);
    await go(router, page);

    await pressBackLink();

    expect(router.state.historyAction).toBe('POP');
    const { pathname, search } = router.state.location;
    expect(`${pathname}${search}`).toBe(opener);
  });

  it('goes back one entry after a replace of the page, which keeps where it was opened from', async () => {
    const router = renderAt('/list');
    await go(router, '/page/1');
    await go(router, '/page/2', true);

    await pressBackLink();

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/list');
  });

  it('pushes its destination on a plain click when another screen opened the page', async () => {
    const router = renderAt('/elsewhere');
    await go(router, '/page/1');

    await pressBackLink();

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe('/list');
    await act(() => router.navigate(-1));
    expect(router.state.location.pathname).toBe('/page/1');
  });

  it('pushes its destination when the page opened first, as after a reload, even with the list before it', async () => {
    const router = renderAt('/list', '/page/1');

    await pressBackLink();

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe('/list');
  });

  it('pushes its destination once the entry that opened the page has been replaced', async () => {
    const router = renderAt('/list');
    await go(router, '/page/1');
    await act(() => router.navigate(-1));
    await go(router, '/list?view=archived', true);
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/page/1');

    await pressBackLink();

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe('/list');
  });

  it.each(['ctrlKey', 'metaKey', 'shiftKey'])(
    'leaves a click with %s to the browser, which follows the href, even when the entry before opened the page',
    async (modifier) => {
      const router = renderAt('/list');
      await go(router, '/page/1');
      const { location } = router.state;
      const prevented = watchClicks();

      fireEvent.click(screen.getByRole('link', { name: 'List' }), { [modifier]: true });
      await act(async () => {});

      expect(prevented).toEqual([false]);
      expect(router.state.location).toBe(location);
    },
  );
});
