import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type * as ApiModule from './api.ts';
import type * as SessionModule from './session.ts';

// A fresh copy of the wrapper for each test, as it keeps the antiforgery token from one request to the next.
let api: typeof ApiModule.api;
let ApiError: typeof ApiModule.ApiError;
let classify: typeof ApiModule.classify;
let onSessionEnded: typeof SessionModule.onSessionEnded;

const fetchMock = vi.fn<typeof fetch>();

const antiforgeryPath = '/api/auth/antiforgery';

/** The antiforgery endpoint's answer: a `204` with the token in a header. */
function tokenAnswer(token: string) {
  return new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': token } });
}

/**
 * Stubs `fetch` as the server: `GET /api/auth/antiforgery` answers with a new token each time, `token-1`, then
 * `token-2` and so on, and every other request gets the next of `answers`, in turn.
 */
function serve(...answers: Response[]) {
  let issued = 0;
  fetchMock.mockImplementation((path) => {
    if (path === antiforgeryPath) {
      issued += 1;
      return Promise.resolve(tokenAnswer(`token-${issued}`));
    }

    const answer = answers.shift();
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return Promise.resolve(answer);
  });
}

/** A JSON answer. */
function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its extensions such as `code`. */
function problem(status: number, extensions: Record<string, unknown> = {}) {
  return new Response(JSON.stringify({ type: 'about:blank', title: 'Problem', status, ...extensions }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

/** Like `fetch` to a server that never answers: it rejects only when its signal aborts, with the signal's reason. */
function noAnswer(_input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

/** Like `fetch` to a server that sends a `200` and part of its body, then nothing more, until the signal aborts. */
function stalledBody(_input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(new TextEncoder().encode('[{"id":'));
      const signal = init?.signal;
      signal?.addEventListener('abort', () => {
        controller.error(signal.reason);
      });
    },
  });
  return Promise.resolve(new Response(body, { status: 200, headers: { 'Content-Type': 'application/json' } }));
}

/** Each request sent, in order: its path, method, headers and body. */
function sent() {
  return fetchMock.mock.calls.map(([path, init]) => ({
    // api() passes a path, never a Request or a URL.
    path: path as string,
    method: init?.method,
    headers: new Headers(init?.headers),
    body: init?.body,
  }));
}

/** The one request sent apart from any for the antiforgery token. */
function sentRequest() {
  const requests = sent().filter(({ path }) => path !== antiforgeryPath);
  expect(requests).toHaveLength(1);
  return requests[0];
}

/** Each request sent, in order, as its method and path and the antiforgery token it carried, if any. */
function trail() {
  return sent().map(({ path, method, headers }) => {
    const token = headers.get('X-XSRF-TOKEN');
    return token === null ? `${method} ${path}` : `${method} ${path} with ${token}`;
  });
}

/** The request's promise, and whether it has settled yet, for a test that advances the timers. */
function track(request: Promise<unknown>) {
  let settled = false;
  const settle = () => {
    settled = true;
  };
  void request.then(settle, settle);
  return { request, settled: () => settled };
}

const removers: (() => void)[] = [];

/** A listener for the session-ended event, removed after the test. */
function listen() {
  const listener = vi.fn();
  removers.push(onSessionEnded(listener));
  return listener;
}

beforeEach(async () => {
  vi.resetModules();
  ({ api, ApiError, classify } = await import('./api.ts'));
  ({ onSessionEnded } = await import('./session.ts'));
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  for (const remove of removers.splice(0)) {
    remove();
  }
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('api', () => {
  it('sends a GET with no body and no Content-Type, and gives the JSON of a 2xx answer', async () => {
    const users = [{ id: '7d1c', displayName: 'Sam Lee', role: 'Worker', status: 'Active' }];
    fetchMock.mockResolvedValue(json(200, users));

    await expect(api('/api/admin/users')).resolves.toEqual(users);

    const request = sentRequest();
    expect(request.path).toBe('/api/admin/users');
    expect(request.method).toBe('GET');
    expect(request.body).toBeUndefined();
    expect([...request.headers]).toEqual([]);
  });

  it('sends a body as JSON, with Content-Type: application/json', async () => {
    const invite = { email: 'sam@example.com', displayName: 'Sam Lee', role: 'Worker' };
    serve(new Response(null, { status: 201 }));

    await api('/api/admin/users', { method: 'POST', body: invite });

    const request = sentRequest();
    expect(request.method).toBe('POST');
    expect(request.body).toBe(JSON.stringify(invite));
    expect(request.headers.get('Content-Type')).toBe('application/json');
  });

  it.each([204, 200, 201])('gives nothing for a %i with no body', async (status) => {
    serve(new Response(null, { status }));

    await expect(api('/api/admin/users/7d1c', { method: 'PUT', body: {} })).resolves.toBeUndefined();
  });

  it('throws ApiError with the status and the code of a problem', async () => {
    serve(problem(409, { code: 'user.email_in_use' }));

    await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(
      new ApiError(409, 'user.email_in_use'),
    );
  });

  it('throws ApiError with a 422\'s field errors', async () => {
    const errors = { email: ['Enter an email address, like name@example.com'], role: ['Choose Worker or Manager'] };
    serve(problem(422, { code: 'validation.failed', errors }));

    await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(
      new ApiError(422, 'validation.failed', errors),
    );
  });

  it.each([
    ['a code that is not a string', problem(409, { code: 409 })],
    ['field errors on a status other than 422', problem(409, { errors: { email: ['Wrong'] } })],
    ['field errors that are not lists of messages', problem(422, { errors: { email: 'Wrong' } })],
  ])('leaves out %s', async (_case, answer) => {
    serve(answer);

    await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(
      new ApiError(answer.status),
    );
  });

  it.each([
    ['an HTML page', '<!doctype html><title>502 Bad Gateway</title>'],
    ['an empty body', ''],
    ['JSON that is not an object', '"Bad gateway"'],
  ])('throws ApiError with the status alone for an error body that is %s', async (_case, body) => {
    fetchMock.mockResolvedValue(new Response(body, { status: 502, headers: { 'Content-Type': 'text/html' } }));

    await expect(api('/api/admin/users')).rejects.toStrictEqual(new ApiError(502));
  });

  it('throws ApiError with status 0 when fetch rejects', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(api('/api/admin/users')).rejects.toStrictEqual(new ApiError(0));
  });

  describe('timeout', () => {
    it('fails with status 0 when no answer has come after 10 seconds, and not before', async () => {
      fetchMock.mockImplementation(noAnswer);
      const { request, settled } = track(api('/api/admin/users'));

      await vi.advanceTimersByTimeAsync(9_999);
      expect(settled()).toBe(false);

      await vi.advanceTimersByTimeAsync(1);
      await expect(request).rejects.toStrictEqual(new ApiError(0));
    });

    it('waits timeoutMs instead when it is given', async () => {
      fetchMock.mockImplementation(noAnswer);
      const { request, settled } = track(api('/api/reports/daily/2026-10-08', { timeoutMs: 30_000 }));

      await vi.advanceTimersByTimeAsync(29_999);
      expect(settled()).toBe(false);

      await vi.advanceTimersByTimeAsync(1);
      await expect(request).rejects.toStrictEqual(new ApiError(0));
    });

    it('fails with status 0 when the answer\'s body stalls before it is whole', async () => {
      fetchMock.mockImplementation(stalledBody);
      const { request, settled } = track(api('/api/admin/users'));

      await vi.advanceTimersByTimeAsync(9_999);
      expect(settled()).toBe(false);

      await vi.advanceTimersByTimeAsync(1);
      await expect(request).rejects.toStrictEqual(new ApiError(0));
    });

    it('stops once the answer has been read', async () => {
      fetchMock.mockResolvedValueOnce(json(200, [])).mockResolvedValueOnce(problem(503));

      await api('/api/admin/users');
      await expect(api('/api/admin/users')).rejects.toStrictEqual(new ApiError(503));

      expect(vi.getTimerCount()).toBe(0);
    });
  });

  describe('the caller\'s own signal', () => {
    it('aborts the request, whose abort is rethrown as it came', async () => {
      fetchMock.mockImplementation(noAnswer);
      const ended = listen();
      const caller = new AbortController();
      const reason = new DOMException('The page was left.', 'AbortError');
      const { request } = track(api('/api/admin/users', { signal: caller.signal }));

      caller.abort(reason);

      await expect(request).rejects.toBe(reason);
      expect(fetchMock.mock.calls[0][1]?.signal?.aborted).toBe(true);
      expect(ended).not.toHaveBeenCalled();
      expect(vi.getTimerCount()).toBe(0);
    });

    it('aborts the reading of the body too', async () => {
      fetchMock.mockImplementation(stalledBody);
      const caller = new AbortController();
      const reason = new DOMException('The page was left.', 'AbortError');
      const { request } = track(api('/api/admin/users', { signal: caller.signal }));
      await vi.advanceTimersByTimeAsync(0);

      caller.abort(reason);

      await expect(request).rejects.toBe(reason);
    });

    it('sends nothing when it has aborted already', async () => {
      const caller = new AbortController();
      const reason = new DOMException('The page was left.', 'AbortError');
      caller.abort(reason);

      await expect(api('/api/admin/users', { signal: caller.signal })).rejects.toBe(reason);
      expect(fetchMock).not.toHaveBeenCalled();
    });
  });

  describe('If-Match', () => {
    it('sends the row\'s token as exactly one quoted value', async () => {
      serve(new Response(null, { status: 200 }));

      await api('/api/admin/goals/4b2e', { method: 'PUT', body: { text: 'Cook a meal' }, ifMatch: 'AAAAAAAAB9E=' });

      expect(sentRequest().headers.get('If-Match')).toBe('"AAAAAAAAB9E="');
    });

    it('refuses an empty token, and sends nothing', async () => {
      await expect(api('/api/admin/goals/4b2e/archive', { method: 'POST', ifMatch: '' })).rejects.toThrow(
        'If-Match needs the row\'s token',
      );
      expect(fetchMock).not.toHaveBeenCalled();
    });
  });

  describe('the session-ended event', () => {
    it('is raised once by a 401 from /api/auth/me, before the ApiError is thrown', async () => {
      const order: string[] = [];
      const ended = listen();
      ended.mockImplementation(() => order.push('session ended'));
      fetchMock.mockResolvedValue(problem(401));

      await api('/api/auth/me').catch((error: unknown) => {
        expect(error).toStrictEqual(new ApiError(401));
        order.push('thrown');
      });

      expect(ended).toHaveBeenCalledOnce();
      expect(order).toEqual(['session ended', 'thrown']);
    });

    it.each([
      '/api/auth/login',
      '/api/auth/login/totp',
      '/api/auth/passkey',
      '/api/auth/passkey/options',
      '/api/auth/setup/start',
      '/api/auth/setup/password',
      '/api/auth/logout',
    ])('is not raised by a 401 from %s, which is still thrown', async (path) => {
      const ended = listen();
      serve(problem(401));

      await expect(api(path, { method: 'POST', body: {} })).rejects.toStrictEqual(new ApiError(401));
      expect(ended).not.toHaveBeenCalled();
    });

    it('is not raised by any other failure', async () => {
      const ended = listen();
      fetchMock.mockResolvedValueOnce(problem(403)).mockRejectedValueOnce(new TypeError('Failed to fetch'));

      await expect(api('/api/auth/me')).rejects.toStrictEqual(new ApiError(403));
      await expect(api('/api/auth/me')).rejects.toStrictEqual(new ApiError(0));
      expect(ended).not.toHaveBeenCalled();
    });

    it('is not heard by a listener that has been removed', async () => {
      const kept = listen();
      const removed = vi.fn();
      onSessionEnded(removed)();
      serve(problem(401));

      await expect(api('/api/auth/ping', { method: 'POST' })).rejects.toStrictEqual(new ApiError(401));

      expect(kept).toHaveBeenCalledOnce();
      expect(removed).not.toHaveBeenCalled();
    });
  });

  describe('the antiforgery token', () => {
    const tokenRequest = `GET ${antiforgeryPath}`;
    const me = { userId: '7d1c', displayName: 'Sam Lee', role: 'Worker', today: '2026-10-09' };

    /** A `2xx` with no body, as most changes answer. */
    function done(status = 204) {
      return new Response(null, { status });
    }

    it('is fetched from the SPA\'s own origin before the first change, which sends it', async () => {
      serve(done(201));

      await api('/api/admin/users', { method: 'POST', body: {} });

      expect(trail()).toEqual([tokenRequest, 'POST /api/admin/users with token-1']);
    });

    it('is kept, and sent on every POST, PUT and DELETE and on no GET', async () => {
      serve(done(201), done(200), done(), json(200, { participants: [] }));

      await api('/api/admin/participants/2f9a/goals', { method: 'POST', body: { text: 'Cook a meal' } });
      await api('/api/admin/goals/4b2e', { method: 'PUT', body: { text: 'Cook a meal' }, ifMatch: 'AAAAAAAAB9E=' });
      await api('/api/participants/2f9a/notes/2026-10-09/draft', { method: 'DELETE' });
      await api('/api/today');

      expect(trail()).toEqual([
        tokenRequest,
        'POST /api/admin/participants/2f9a/goals with token-1',
        'PUT /api/admin/goals/4b2e with token-1',
        'DELETE /api/participants/2f9a/notes/2026-10-09/draft with token-1',
        'GET /api/today',
      ]);
    });

    it('is neither fetched nor sent for a GET', async () => {
      fetchMock.mockResolvedValue(json(200, { participants: [] }));

      await api('/api/today');

      expect(trail()).toEqual(['GET /api/today']);
    });

    it('is fetched once for changes sent together', async () => {
      serve(done(), done());

      await Promise.all([
        api('/api/admin/goals/4b2e/archive', { method: 'POST' }),
        api('/api/admin/goals/9c0d/archive', { method: 'POST' }),
      ]);

      expect(trail()).toEqual([
        tokenRequest,
        'POST /api/admin/goals/4b2e/archive with token-1',
        'POST /api/admin/goals/9c0d/archive with token-1',
      ]);
    });

    it.each(['/api/auth/login/totp', '/api/auth/passkey', '/api/auth/setup/password', '/api/auth/setup/passkey'])(
      'is fetched afresh for the change after a 2xx from %s',
      async (path) => {
        serve(json(200, me), done());

        await api(path, { method: 'POST', body: {} });
        await api('/api/auth/ping', { method: 'POST' });

        expect(trail()).toEqual([
          tokenRequest,
          `POST ${path} with token-1`,
          tokenRequest,
          'POST /api/auth/ping with token-2',
        ]);
      },
    );

    it.each(['/api/auth/login', '/api/auth/passkey/options', '/api/auth/setup/passkey/options'])(
      'is kept after a 2xx from %s',
      async (path) => {
        serve(json(200, {}), done());

        await api(path, { method: 'POST', body: {} });
        await api('/api/auth/ping', { method: 'POST' });

        expect(trail()).toEqual([tokenRequest, `POST ${path} with token-1`, 'POST /api/auth/ping with token-1']);
      },
    );

    it('is kept after a sign-in that fails', async () => {
      serve(problem(401), done());

      await expect(api('/api/auth/login/totp', { method: 'POST', body: { code: '123456' } })).rejects.toStrictEqual(
        new ApiError(401),
      );
      await api('/api/auth/ping', { method: 'POST' });

      expect(trail()).toEqual([
        tokenRequest,
        'POST /api/auth/login/totp with token-1',
        'POST /api/auth/ping with token-1',
      ]);
    });

    it('is fetched afresh for a change refused 400 with no code, sent once more with the same body', async () => {
      const goal = { text: 'Cook a meal' };
      serve(problem(400), done(201));

      await expect(api('/api/admin/participants/2f9a/goals', { method: 'POST', body: goal })).resolves.toBeUndefined();

      expect(trail()).toEqual([
        tokenRequest,
        'POST /api/admin/participants/2f9a/goals with token-1',
        tokenRequest,
        'POST /api/admin/participants/2f9a/goals with token-2',
      ]);
      const [first, second] = sent().filter(({ path }) => path !== antiforgeryPath);
      expect(first.body).toBe(JSON.stringify(goal));
      expect(second.body).toBe(first.body);
    });

    it('is fetched afresh once for changes refused together', async () => {
      serve(problem(400), problem(400), done(), done());

      await Promise.all([
        api('/api/admin/goals/4b2e/archive', { method: 'POST' }),
        api('/api/admin/goals/9c0d/archive', { method: 'POST' }),
      ]);

      expect(trail()).toEqual([
        tokenRequest,
        'POST /api/admin/goals/4b2e/archive with token-1',
        'POST /api/admin/goals/9c0d/archive with token-1',
        tokenRequest,
        'POST /api/admin/goals/4b2e/archive with token-2',
        'POST /api/admin/goals/9c0d/archive with token-2',
      ]);
    });

    it('is not fetched a third time for a second 400, which is thrown', async () => {
      serve(problem(400), problem(400));

      await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(new ApiError(400));

      expect(trail()).toEqual([
        tokenRequest,
        'POST /api/admin/users with token-1',
        tokenRequest,
        'POST /api/admin/users with token-2',
      ]);
    });

    it('is not fetched afresh for a 400 with a code, which is thrown', async () => {
      // No 400 has a code yet (design.md §6.9), so this one is made up.
      serve(problem(400, { code: 'sample.refused' }));

      await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(
        new ApiError(400, 'sample.refused'),
      );

      expect(trail()).toEqual([tokenRequest, 'POST /api/admin/users with token-1']);
    });

    it('is not fetched for a 400 to a GET, which is thrown', async () => {
      fetchMock.mockResolvedValue(problem(400));

      await expect(api('/api/today')).rejects.toStrictEqual(new ApiError(400));

      expect(trail()).toEqual(['GET /api/today']);
    });

    it.each([
      [0, () => Promise.reject(new TypeError('Failed to fetch'))],
      [503, () => Promise.resolve(problem(503))],
    ])('fails its change with status %i when its request does, and is asked for again by the next', async (
      status,
      failedTokenRequest,
    ) => {
      serve(done(201));
      fetchMock.mockImplementationOnce(failedTokenRequest);

      await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(new ApiError(status));
      expect(trail()).toEqual([tokenRequest]);

      await api('/api/admin/users', { method: 'POST', body: {} });
      expect(trail()).toEqual([tokenRequest, tokenRequest, 'POST /api/admin/users with token-1']);
    });

    it('fails its change with status 0 when its request has no answer after 10 seconds, and not before', async () => {
      fetchMock.mockImplementation(noAnswer);
      const { request, settled } = track(api('/api/admin/users', { method: 'POST', body: {} }));

      await vi.advanceTimersByTimeAsync(9_999);
      expect(settled()).toBe(false);

      await vi.advanceTimersByTimeAsync(1);
      await expect(request).rejects.toStrictEqual(new ApiError(0));
      expect(trail()).toEqual([tokenRequest]);
    });

    it('comes by a request with a timeout of its own, after which the change has its own 10 seconds', async () => {
      fetchMock
        .mockImplementationOnce(
          () =>
            new Promise((resolve) => {
              setTimeout(() => resolve(tokenAnswer('token-1')), 9_000);
            }),
        )
        .mockImplementationOnce(noAnswer);
      const { request, settled } = track(api('/api/admin/users', { method: 'POST', body: {} }));

      await vi.advanceTimersByTimeAsync(9_000 + 9_999);
      expect(settled()).toBe(false);

      await vi.advanceTimersByTimeAsync(1);
      await expect(request).rejects.toStrictEqual(new ApiError(0));
      expect(trail()).toEqual([tokenRequest, 'POST /api/admin/users with token-1']);
    });

    it('fails its change as something that went wrong when the endpoint\'s answer holds none', async () => {
      fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

      const error = await api('/api/admin/users', { method: 'POST', body: {} }).catch((thrown: unknown) => thrown);

      expect(classify(error)).toBe('server');
      expect(trail()).toEqual([tokenRequest]);
    });

    it('is kept in no storage and no cookie that script can read', async () => {
      serve(problem(400), done(201), json(200, me), done());

      await api('/api/admin/users', { method: 'POST', body: {} });
      await api('/api/auth/login/totp', { method: 'POST', body: { code: '123456' } });
      await api('/api/auth/ping', { method: 'POST' });

      expect(trail().filter((request) => request === tokenRequest)).toHaveLength(3);
      expect(localStorage.length).toBe(0);
      expect(sessionStorage.length).toBe(0);
      expect(document.cookie).toBe('');
    });
  });
});

describe('classify', () => {
  it.each<{ failed: string; error: () => unknown; failure: ApiModule.Failure }>([
    { failed: 'status 0', error: () => new ApiError(0), failure: 'offline' },
    { failed: 'a 401', error: () => new ApiError(401), failure: 'signedOut' },
    { failed: 'a 400', error: () => new ApiError(400), failure: 'server' },
    { failed: 'a 403', error: () => new ApiError(403), failure: 'server' },
    { failed: 'a 404', error: () => new ApiError(404), failure: 'server' },
    { failed: 'a 409', error: () => new ApiError(409, 'user.email_in_use'), failure: 'server' },
    { failed: 'a 422', error: () => new ApiError(422, 'validation.failed', {}), failure: 'server' },
    { failed: 'a 500', error: () => new ApiError(500), failure: 'server' },
    { failed: 'a 503', error: () => new ApiError(503), failure: 'server' },
    { failed: 'a TypeError from a bug', error: () => new TypeError('x is not a function'), failure: 'server' },
    { failed: 'something thrown that is not an error', error: () => 'failed', failure: 'server' },
  ])('gives $failure for $failed', ({ error, failure }) => {
    expect(classify(error())).toBe(failure);
  });
});
