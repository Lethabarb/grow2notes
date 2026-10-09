import { focusManager, onlineManager, QueryClientProvider, useMutation, useQuery } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError } from './api.ts';
import { createQueryClient } from './queryClient.ts';

type Participant = { id: string; givenName: string; familyName: string; dateOfBirth: string; status: string };
type NewParticipant = Pick<Participant, 'givenName' | 'familyName' | 'dateOfBirth'>;

const participantsPath = '/api/admin/participants';
const antiforgeryPath = '/api/auth/antiforgery';

const participants: Participant[] = [
  { id: '3f2a', givenName: 'Jane', familyName: 'Citizen', dateOfBirth: '1990-04-12', status: 'Active' },
];
const newParticipant: NewParticipant = { givenName: 'Sam', familyName: 'Lee', dateOfBirth: '1988-11-03' };

const fetchMock = vi.fn<typeof fetch>();

/**
 * Stubs `fetch` as the server: the antiforgery endpoint always gives a token, so that a test counts and answers only
 * the requests it makes itself, and every other request gets a new answer from `answer`.
 */
function serve(answer: (init?: RequestInit) => Promise<Response>) {
  fetchMock.mockImplementation((path, init) =>
    path === antiforgeryPath
      ? Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }))
      : answer(init),
  );
}

/** How many requests have been sent, leaving out those for the antiforgery token. */
function sent() {
  return fetchMock.mock.calls.filter(([path]) => path !== antiforgeryPath).length;
}

/** Like `fetch` with no connection, which rejects at once. */
function noConnection(): Promise<Response> {
  return Promise.reject(new TypeError('Failed to fetch'));
}

/** Like `fetch` to a server that never answers: it rejects only when its signal aborts, with the signal's reason. */
function noAnswer(init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

/** An RFC 9457 problem with no `code`, as the API answers a failure (design.md §6.1). */
function problem(status: number): Promise<Response> {
  return Promise.resolve(
    new Response(JSON.stringify({ type: 'about:blank', title: 'Problem', status }), {
      status,
      headers: { 'Content-Type': 'application/problem+json' },
    }),
  );
}

/** A `200` with a JSON body. */
function json(body: unknown): Promise<Response> {
  return Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** Renders `hook` in a component under a client of its own, made as `main.tsx` makes the app's. */
function renderWithClient<T>(hook: () => T) {
  const client = createQueryClient();
  return renderHook(hook, {
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  });
}

/** A screen's query, which passes TanStack's signal to `api()`, as every screen's does. */
function useParticipants() {
  return useQuery({
    queryKey: ['participants'],
    queryFn: ({ signal }) => api<Participant[]>(participantsPath, { signal }),
  });
}

/** A form's mutation. */
function useAddParticipant() {
  return useMutation({
    mutationFn: (participant: NewParticipant) => api(participantsPath, { method: 'POST', body: participant }),
  });
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

// The three ways a request can fail that a second try might get past, each with how long it takes to fail and the
// status it fails with.
const transientFailures = [
  { failure: 'no connection', answer: noConnection, failsAfter: 0, status: 0 },
  { failure: 'a timeout', answer: noAnswer, failsAfter: 10_000, status: 0 },
  { failure: 'a 503', answer: () => problem(503), failsAfter: 0, status: 503 },
];

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  onlineManager.setOnline(true);
  focusManager.setFocused(undefined);
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('createQueryClient', () => {
  describe('a query', () => {
    it.each(transientFailures)(
      'that fails with $failure is fetched once more, silently, 1 s after it fails, then fails',
      async ({ answer, failsAfter, status }) => {
        serve(answer);
        const { result } = renderWithClient(useParticipants);

        await wait(failsAfter + 999);
        expect(sent()).toBe(1);
        expect(result.current.isPending).toBe(true);

        await wait(1);
        expect(sent()).toBe(2);
        expect(result.current.isPending).toBe(true);

        await wait(60_000);
        expect(result.current.isLoadingError).toBe(true);
        expect(result.current.error).toStrictEqual(new ApiError(status));
        expect(sent()).toBe(2);
      },
    );

    it.each([401, 403, 404, 409, 410, 412, 422])('answered %i is fetched once, and fails', async (status) => {
      serve(() => problem(status));
      const { result } = renderWithClient(useParticipants);

      await wait(0);
      expect(result.current.isLoadingError).toBe(true);
      expect(result.current.error).toStrictEqual(new ApiError(status));

      await wait(60_000);
      expect(sent()).toBe(1);
    });

    it('runs while the browser reports offline, and fails rather than waiting', async () => {
      onlineManager.setOnline(false);
      serve(noConnection);
      const { result } = renderWithClient(useParticipants);

      await wait(0);
      expect(sent()).toBe(1);
      expect(result.current.fetchStatus).toBe('fetching');

      await wait(60_000);
      expect(result.current.isLoadingError).toBe(true);
      expect(result.current.error).toStrictEqual(new ApiError(0));
      expect(sent()).toBe(2);
    });

    it('is not fetched again when the page becomes visible again', async () => {
      serve(() => json(participants));
      const { result } = renderWithClient(useParticipants);
      await wait(0);
      // TanStack's staleTime of 0 makes the list stale at once, so only refetchOnWindowFocus stops it being fetched.
      expect(result.current.data).toEqual(participants);

      act(() => {
        focusManager.setFocused(false);
      });
      act(() => {
        focusManager.setFocused(true);
      });
      await wait(60_000);

      expect(sent()).toBe(1);
    });
  });

  describe('a mutation', () => {
    it.each(transientFailures)('that fails with $failure is sent once', async ({ answer, status }) => {
      serve(answer);
      const { result } = renderWithClient(useAddParticipant);

      act(() => {
        result.current.mutate(newParticipant);
      });
      await wait(60_000);

      expect(result.current.error).toStrictEqual(new ApiError(status));
      expect(sent()).toBe(1);
    });

    it('is sent at once and fails while the browser reports offline, and is not sent again once online', async () => {
      onlineManager.setOnline(false);
      serve(noConnection);
      const { result } = renderWithClient(useAddParticipant);

      act(() => {
        result.current.mutate(newParticipant);
      });
      await wait(0);
      expect(sent()).toBe(1);
      expect(result.current.isPaused).toBe(false);
      expect(result.current.error).toStrictEqual(new ApiError(0));

      act(() => {
        onlineManager.setOnline(true);
      });
      await wait(60_000);
      expect(sent()).toBe(1);
    });
  });

  it('leaves nothing in localStorage or sessionStorage after a query and a mutation', async () => {
    serve((init) =>
      init?.method === 'POST' ? Promise.resolve(new Response(null, { status: 201 })) : json(participants),
    );
    const { result } = renderWithClient(() => ({ list: useParticipants(), add: useAddParticipant() }));
    await wait(0);

    act(() => {
      result.current.add.mutate(newParticipant);
    });
    await wait(0);

    expect(result.current.list.data).toEqual(participants);
    expect(result.current.add.isSuccess).toBe(true);
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });
});
