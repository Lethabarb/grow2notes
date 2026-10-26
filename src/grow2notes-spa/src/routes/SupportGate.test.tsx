import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { fullPageLoad } from './fullPageLoad.ts';
import { rearmUnloadGuard } from './leaveGuard.ts';
import { routes } from './routes.tsx';

// jsdom does not navigate, so the full reload is a stand-in that counts its calls.
vi.mock('./fullPageLoad.ts');

// A made-up Support person (A35).
const support: Me = {
  userId: 'e7c2a94d-3b58-4f61-8d0a-5a1f9b6c2e34',
  displayName: 'Ivy Chen',
  role: 'Support',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

const fetchMock = vi.fn<typeof fetch>();
const logoutPath = '/api/auth/logout';

/** Stubs `fetch` as the server: `/me` answers Support, the antiforgery endpoint a token, and the logout `logout`. */
function serve(logout: () => Promise<Response>) {
  fetchMock.mockImplementation((path) => {
    if (path === '/api/auth/antiforgery') {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'made-up-token' } }));
    }

    if (path === logoutPath) {
      return logout();
    }

    return Promise.resolve(
      new Response(JSON.stringify(support), { headers: { 'Content-Type': 'application/json' } }),
    );
  });
}

/** Renders the app at `/`, where a Support account sees the Support page. */
async function renderSupportPage() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <RouterProvider router={createMemoryRouter(routes, { initialEntries: ['/'] })} />
    </QueryClientProvider>,
  );
  await settle();
}

/** Lets every request and render that falls due now run. */
async function settle() {
  await act(() => vi.advanceTimersByTimeAsync(0));
}

const signOutButton = () => within(screen.getByRole('main')).getByRole('button', { name: 'Sign out' });

/** The Support page's own status line: the one in `<main>` that is shown, not the page's hidden one. */
function statusLine(): HTMLElement {
  const shown = within(screen.getByRole('main'))
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  rearmUnloadGuard();
  fetchMock.mockReset();
  vi.mocked(fullPageLoad).mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('the Support page', () => {
  it('posts the logout and loads the app afresh when Sign out is pressed', async () => {
    serve(() => Promise.resolve(new Response(null, { status: 204 })));
    await renderSupportPage();

    fireEvent.click(signOutButton());
    await settle();

    const logouts = fetchMock.mock.calls.filter(([path]) => path === logoutPath);
    expect(logouts).toHaveLength(1);
    expect(logouts[0][1]?.method).toBe('POST');
    expect(fullPageLoad).toHaveBeenCalledOnce();
  });

  it('says why a sign-out failed in its status line, with Sign out ready again', async () => {
    serve(() => Promise.reject(new TypeError('Failed to fetch')));
    await renderSupportPage();
    expect(statusLine()).toBeEmptyDOMElement();

    fireEvent.click(signOutButton());
    await settle();

    expect(statusLine().textContent).toBe('Not signed out: no connection. Try again.');
    expect(signOutButton()).not.toHaveAttribute('aria-disabled');
    expect(fullPageLoad).not.toHaveBeenCalled();
  });
});
