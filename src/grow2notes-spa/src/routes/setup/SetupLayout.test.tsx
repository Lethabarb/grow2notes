import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, renderHook, screen } from '@testing-library/react';
import { useEffect } from 'react';
import { createMemoryRouter, useLocation } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../api/queryClient.ts';
import { routes, type AppRoute } from '../routes.tsx';
import { useSetup } from './setupContext.ts';

// A made-up person and password (A35), the password with the spaces round it that setup keeps.
const account = { email: 'sam.lee@example.org', displayName: 'Sam Lee' };
const password = ' made-up password ';

/** The address of each page that the probe has been committed on, in order. */
const committed: string[] = [];

/**
 * Notes the address it is committed on, shows what the setup layout holds, puts the made-up account and password
 * there, refuses the password, finishes setup, and drops it all.
 */
function Probe() {
  const setup = useSetup();
  const { pathname } = useLocation();
  useEffect(() => {
    committed.push(pathname);
  });
  return (
    <>
      <p>{setup.account === undefined ? 'Nothing in memory' : setup.account.email}</p>
      <p data-testid="password">{setup.password ?? ''}</p>
      <p>{setup.passwordRefused ? 'Password refused' : 'Password not refused'}</p>
      <button type="button" onClick={() => setup.setAccount(account)}>
        Remember
      </button>
      <button type="button" onClick={() => setup.setPassword(password)}>
        Remember password
      </button>
      <button type="button" onClick={setup.refusePassword}>
        Refuse password
      </button>
      <button type="button" onClick={setup.finish}>
        Finish
      </button>
      <button type="button" onClick={setup.drop}>
        Drop
      </button>
    </>
  );
}

/** The password the setup layout holds, as it holds it. */
function heldPassword() {
  return screen.getByTestId('password').textContent;
}

/** The app's route table, with each page under the setup layout replaced by the probe. */
function withProbes(table: AppRoute[]): AppRoute[] {
  return table.map((route) => {
    if (route.index) {
      return route;
    }

    const children =
      route.path === 'setup'
        ? route.children?.map((child) => ({ ...child, element: <Probe /> }))
        : route.children && withProbes(route.children);
    return { ...route, children };
  });
}

/** Renders the app at `path`, with the probes, on a memory router, under a query client of its own. */
function renderApp(path: string) {
  const router = createMemoryRouter(withProbes(routes), { initialEntries: [path] });
  render(
    <QueryClientProvider client={createQueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return router;
}

beforeEach(() => {
  // The shell's GET /api/auth/me, answered as for a person with no session.
  vi.stubGlobal('fetch', vi.fn<typeof fetch>(() => Promise.resolve(new Response(null, { status: 401 }))));
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('SetupLayout', () => {
  it('keeps the account and the password in memory between /setup, /setup/password and /setup/app', async () => {
    const router = renderApp('/setup');
    expect(screen.getByText('Nothing in memory')).toBeInTheDocument();
    expect(heldPassword()).toBe('');

    fireEvent.click(screen.getByRole('button', { name: 'Remember' }));
    expect(screen.getByText(account.email)).toBeInTheDocument();

    await act(() => router.navigate('/setup/password'));
    fireEvent.click(screen.getByRole('button', { name: 'Remember password' }));
    expect(screen.getByText(account.email)).toBeInTheDocument();
    await act(() => router.navigate('/setup/app'));
    expect(screen.getByText(account.email)).toBeInTheDocument();
    expect(heldPassword()).toBe(password);
    await act(() => router.navigate(-2));
    expect(router.state.location.pathname).toBe('/setup');
    expect(screen.getByText(account.email)).toBeInTheDocument();
    expect(heldPassword()).toBe(password);
  });

  it('drops the account, the password and its refusal together, as a 401 from a setup call does', () => {
    renderApp('/setup/password');
    fireEvent.click(screen.getByRole('button', { name: 'Remember' }));
    fireEvent.click(screen.getByRole('button', { name: 'Remember password' }));
    fireEvent.click(screen.getByRole('button', { name: 'Refuse password' }));

    fireEvent.click(screen.getByRole('button', { name: 'Drop' }));

    expect(screen.getByText('Nothing in memory')).toBeInTheDocument();
    expect(heldPassword()).toBe('');
    expect(screen.getByText('Password not refused')).toBeInTheDocument();
  });

  it('keeps the server\'s refusal of the password until a password is taken again', async () => {
    const router = renderApp('/setup/password');
    fireEvent.click(screen.getByRole('button', { name: 'Remember password' }));
    expect(screen.getByText('Password not refused')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Refuse password' }));
    await act(() => router.navigate('/setup/app'));
    await act(() => router.navigate(-1));

    expect(screen.getByText('Password refused')).toBeInTheDocument();
    expect(heldPassword()).toBe(password);

    fireEvent.click(screen.getByRole('button', { name: 'Remember password' }));

    expect(screen.getByText('Password not refused')).toBeInTheDocument();
  });

  it('keeps the page that finished setup, then makes /setup\'s entry Today\'s, committing no page there', async () => {
    const router = renderApp('/setup');
    await act(() => router.navigate('/setup/password'));
    await act(() => router.navigate('/setup/app'));

    fireEvent.click(screen.getByRole('button', { name: 'Finish' }));

    expect(router.state.location.pathname).toBe('/setup/app');
    expect(screen.getByRole('button', { name: 'Finish' })).toBeInTheDocument();
    committed.length = 0;

    await act(() => router.navigate(-2));

    expect(router.state.historyAction).toBe('REPLACE');
    expect(router.state.location.pathname).toBe('/');
    expect(screen.queryByRole('button', { name: 'Finish' })).not.toBeInTheDocument();
    expect(committed).toEqual([]);
    // The entries after it are Forward's, where the layout starts again with nothing in memory.
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/setup/password');
    expect(screen.getByText('Nothing in memory')).toBeInTheDocument();
  });

  it('drops the account and the password on leaving the setup addresses, so that Back finds nothing', async () => {
    const router = renderApp('/setup');
    fireEvent.click(screen.getByRole('button', { name: 'Remember' }));
    await act(() => router.navigate('/setup/password'));
    fireEvent.click(screen.getByRole('button', { name: 'Remember password' }));

    await act(() => router.navigate('/'));
    expect(screen.queryByText(account.email)).not.toBeInTheDocument();

    await act(() => router.navigate(-1));
    expect(router.state.location.pathname).toBe('/setup/password');
    expect(screen.getByText('Nothing in memory')).toBeInTheDocument();
    expect(heldPassword()).toBe('');
  });

  it('keeps the account and the password out of router state and browser storage', async () => {
    const router = renderApp('/setup');
    fireEvent.click(screen.getByRole('button', { name: 'Remember' }));
    fireEvent.click(screen.getByRole('button', { name: 'Remember password' }));

    await act(() => router.navigate('/setup/password'));

    expect(router.state.location.state).toBeNull();
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });
});

describe('useSetup', () => {
  it('throws outside the setup layout', () => {
    // React reports the error it rethrows; the test expects it.
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);

    expect(() => renderHook(() => useSetup())).toThrow('useSetup is for the pages under the setup layout.');
    consoleError.mockRestore();
  });
});
