import { act, fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { describe, expect, it, onTestFinished } from 'vitest';
import { BackLink } from './BackLink.tsx';

/**
 * A made-up page with a back link to `/list`, opened at `/page` after `previous`, with the page saying whether that
 * previous entry is the destination.
 */
function renderPage(previous: string, previousIsDestination: boolean) {
  const router = createMemoryRouter(
    [
      { path: '/list', element: <h1>List</h1> },
      { path: '/elsewhere', element: <h1>Elsewhere</h1> },
      {
        path: '/page',
        element: (
          <BackLink to="/list" previousIsDestination={previousIsDestination}>
            List
          </BackLink>
        ),
      },
    ],
    { initialEntries: [previous, '/page'], initialIndex: 1 },
  );
  render(<RouterProvider router={router} />);
  return router;
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
    renderPage('/list', true);

    const link = screen.getByRole('link', { name: 'List' });
    expect(link).toHaveAttribute('href', '/list');
    expect(link).not.toHaveAttribute('aria-disabled');
    const chevron = link.querySelector('svg');
    expect(chevron).toHaveAttribute('aria-hidden', 'true');
    expect(chevron).toHaveAttribute('focusable', 'false');
  });

  it('goes back one entry on a plain click, adding none, when the page says that is its destination', async () => {
    const router = renderPage('/list', true);

    fireEvent.click(screen.getByRole('link', { name: 'List' }));
    await act(async () => {});

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/list');
    // Forward reaches the page again, which it would not after a push, as a push adds an entry after the page.
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/page');
  });

  it('pushes its destination on a plain click when the page does not say so', async () => {
    const router = renderPage('/elsewhere', false);

    fireEvent.click(screen.getByRole('link', { name: 'List' }));
    await act(async () => {});

    expect(router.state.historyAction).toBe('PUSH');
    expect(router.state.location.pathname).toBe('/list');
    await act(() => router.navigate(-1));
    expect(router.state.location.pathname).toBe('/page');
  });

  it.each(['ctrlKey', 'metaKey', 'shiftKey'])(
    'leaves a click with %s to the browser, which follows the href, even when the page says to go back',
    async (modifier) => {
      const router = renderPage('/list', true);
      const { location } = router.state;
      const prevented = watchClicks();

      fireEvent.click(screen.getByRole('link', { name: 'List' }), { [modifier]: true });
      await act(async () => {});

      expect(prevented).toEqual([false]);
      expect(router.state.location).toBe(location);
    },
  );
});
