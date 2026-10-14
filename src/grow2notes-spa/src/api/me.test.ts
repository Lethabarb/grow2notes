import { CancelledError, QueryClientProvider, QueryObserver } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import { createElement } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './api.ts';
import { meQuery, refreshMe, setMe, useMeAnswers, type Me } from './me.ts';
import { createQueryClient } from './queryClient.ts';

// A made-up manager (A35).
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

// A made-up worker (A35), who signs in on the same browser.
const worker: Me = {
  userId: '2e7b9c14-5a03-4f6d-8b21-c9e0d4a7f315',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

const fetchMock = vi.fn<typeof fetch>();

/** A `200` with `me`. */
function answerWith(me: Me): Promise<Response> {
  return Promise.resolve(new Response(JSON.stringify(me), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem with no `code`, as the API answers a failure (design.md §6.1). */
function problem(status: number): Promise<Response> {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status });
  return Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('Me', () => {
  // npm run typecheck fails if the comparison compiles.
  it("types role as the API's role names, so that a comparison with 'manager' does not compile", () => {
    // @ts-expect-error The API gives 'Manager', with a capital, so this comparison would never match.
    expect(manager.role === 'manager').toBe(false);
  });
});

describe('meQuery', () => {
  it('is fetched by no observer by itself, before any answer or after a failure', async () => {
    fetchMock.mockImplementation(() => problem(503));
    const client = createQueryClient();
    // Subscribed, as useQuery subscribes as its component mounts.
    const observe = () => new QueryObserver(client, meQuery).subscribe(() => {});

    observe();
    await vi.advanceTimersByTimeAsync(60_000);
    expect(fetchMock).not.toHaveBeenCalled();

    const failed = expect(refreshMe(client)).rejects.toStrictEqual(new ApiError(503));
    // Past the query client's one silent retry.
    await vi.advanceTimersByTimeAsync(60_000);
    await failed;
    expect(fetchMock).toHaveBeenCalledTimes(2);

    observe();
    await vi.advanceTimersByTimeAsync(60_000);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});

describe('refreshMe', () => {
  it('sends GET /api/auth/me and gives its answer, sharing a request in flight', async () => {
    fetchMock.mockImplementation(() => answerWith(manager));
    const client = createQueryClient();

    const together = [refreshMe(client), refreshMe(client)];

    await expect(Promise.all(together)).resolves.toEqual([manager, manager]);
    expect(fetchMock).toHaveBeenCalledExactlyOnceWith('/api/auth/me', expect.objectContaining({ method: 'GET' }));
    expect(client.getQueryData(meQuery.queryKey)).toEqual(manager);

    // An answer is never fresh enough to keep a refresh from asking again.
    await expect(refreshMe(client)).resolves.toEqual(manager);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('fails as the request fails, and the query keeps the answer it had', async () => {
    fetchMock.mockImplementationOnce(() => answerWith(manager)).mockImplementation(() => problem(401));
    const client = createQueryClient();
    await refreshMe(client);

    await expect(refreshMe(client)).rejects.toStrictEqual(new ApiError(401));
    expect(client.getQueryState(meQuery.queryKey)).toMatchObject({ status: 'error', data: manager });
  });
});

describe('setMe', () => {
  it("puts the Me in ['me'], where a refresh sent before it, answered after it for another, never lands", async () => {
    let answerBefore: (response: Response) => void = () => undefined;
    fetchMock
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            answerBefore = resolve;
          }),
      )
      .mockImplementation(() => answerWith(worker));
    const client = createQueryClient();
    // Sent with the cookie from before the sign-in, and held. Its caller is told nothing of the other person.
    const before = expect(refreshMe(client)).rejects.toBeInstanceOf(CancelledError);

    await setMe(client, worker);
    expect(client.getQueryData(meQuery.queryKey)).toEqual(worker);

    answerBefore(new Response(JSON.stringify(manager), { headers: { 'Content-Type': 'application/json' } }));
    await vi.advanceTimersByTimeAsync(0);

    expect(client.getQueryData(meQuery.queryKey)).toEqual(worker);
    await before;
    // The next refresh sends a request of its own, with the new cookie, rather than sharing the one cancelled.
    await expect(refreshMe(client)).resolves.toEqual(worker);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("cancels only ['me'] itself, not a query under it such as Today's drafts", async () => {
    fetchMock.mockImplementation(() => new Promise(() => {}));
    const client = createQueryClient();
    const drafts = new QueryObserver(client, { queryKey: ['me', 'drafts'], queryFn: () => fetch('/made-up') });
    drafts.subscribe(() => {});
    expect(client.getQueryState(['me', 'drafts'])?.fetchStatus).toBe('fetching');

    await setMe(client, worker);

    expect(client.getQueryState(['me', 'drafts'])?.fetchStatus).toBe('fetching');
  });
});

describe('useMeAnswers', () => {
  it("counts each answer that ['me'] takes, one equal to the answer held and a sign-in's included", async () => {
    fetchMock.mockImplementation(() => answerWith(manager));
    const client = createQueryClient();
    const { result } = renderHook(() => useMeAnswers(), {
      wrapper: ({ children }) => createElement(QueryClientProvider, { client }, children),
    });
    expect(result.current).toBe(0);

    await act(() => refreshMe(client));
    expect(result.current).toBe(1);

    // The same object as before, which only the count tells apart.
    const held = client.getQueryData(meQuery.queryKey);
    await act(() => refreshMe(client));
    expect(client.getQueryData(meQuery.queryKey)).toBe(held);
    expect(result.current).toBe(2);

    await act(() => setMe(client, manager));
    expect(result.current).toBe(3);

    fetchMock.mockImplementation(() => problem(401));
    await act(() => refreshMe(client).catch(() => undefined));
    expect(result.current).toBe(3);
  });
});
