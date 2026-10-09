import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError, classify, type Failure } from './api.ts';
import { onSessionEnded } from './session.ts';

const fetchMock = vi.fn<typeof fetch>();

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

/** The one request sent: its path, method, headers and body. */
function sentRequest() {
  expect(fetchMock).toHaveBeenCalledOnce();
  const [path, init] = fetchMock.mock.calls[0];
  return { path, method: init?.method, headers: new Headers(init?.headers), body: init?.body };
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

beforeEach(() => {
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

    const sent = sentRequest();
    expect(sent.path).toBe('/api/admin/users');
    expect(sent.method).toBe('GET');
    expect(sent.body).toBeUndefined();
    expect([...sent.headers]).toEqual([]);
  });

  it('sends a body as JSON, with Content-Type: application/json', async () => {
    const invite = { email: 'sam@example.com', displayName: 'Sam Lee', role: 'Worker' };
    fetchMock.mockResolvedValue(new Response(null, { status: 201 }));

    await api('/api/admin/users', { method: 'POST', body: invite });

    const sent = sentRequest();
    expect(sent.method).toBe('POST');
    expect(sent.body).toBe(JSON.stringify(invite));
    expect(sent.headers.get('Content-Type')).toBe('application/json');
  });

  it.each([204, 200, 201])('gives nothing for a %i with no body', async (status) => {
    fetchMock.mockResolvedValue(new Response(null, { status }));

    await expect(api('/api/admin/users/7d1c', { method: 'PUT', body: {} })).resolves.toBeUndefined();
  });

  it('throws ApiError with the status and the code of a problem', async () => {
    fetchMock.mockResolvedValue(problem(409, { code: 'user.email_in_use' }));

    await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(
      new ApiError(409, 'user.email_in_use'),
    );
  });

  it('throws ApiError with a 422\'s field errors', async () => {
    const errors = { email: ['Enter an email address, like name@example.com'], role: ['Choose Worker or Manager'] };
    fetchMock.mockResolvedValue(problem(422, { code: 'validation.failed', errors }));

    await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(
      new ApiError(422, 'validation.failed', errors),
    );
  });

  it.each([
    ['a code that is not a string', problem(409, { code: 409 }), new ApiError(409)],
    ['field errors on a status other than 422', problem(400, { errors: { email: ['Wrong'] } }), new ApiError(400)],
    ['field errors that are not lists of messages', problem(422, { errors: { email: 'Wrong' } }), new ApiError(422)],
  ])('leaves out %s', async (_case, answer, error) => {
    fetchMock.mockResolvedValue(answer);

    await expect(api('/api/admin/users', { method: 'POST', body: {} })).rejects.toStrictEqual(error);
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
      fetchMock.mockResolvedValue(new Response(null, { status: 200 }));

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
      fetchMock.mockResolvedValue(problem(401));

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
      fetchMock.mockResolvedValue(problem(401));

      await expect(api('/api/auth/ping', { method: 'POST' })).rejects.toStrictEqual(new ApiError(401));

      expect(kept).toHaveBeenCalledOnce();
      expect(removed).not.toHaveBeenCalled();
    });
  });
});

describe('classify', () => {
  it.each<{ failed: string; error: unknown; failure: Failure }>([
    { failed: 'status 0', error: new ApiError(0), failure: 'offline' },
    { failed: 'a 401', error: new ApiError(401), failure: 'signedOut' },
    { failed: 'a 400', error: new ApiError(400), failure: 'server' },
    { failed: 'a 403', error: new ApiError(403), failure: 'server' },
    { failed: 'a 404', error: new ApiError(404), failure: 'server' },
    { failed: 'a 409', error: new ApiError(409, 'user.email_in_use'), failure: 'server' },
    { failed: 'a 422', error: new ApiError(422, 'validation.failed', {}), failure: 'server' },
    { failed: 'a 500', error: new ApiError(500), failure: 'server' },
    { failed: 'a 503', error: new ApiError(503), failure: 'server' },
    { failed: 'a TypeError from a bug', error: new TypeError('x is not a function'), failure: 'server' },
    { failed: 'something thrown that is not an error', error: 'failed', failure: 'server' },
  ])('gives $failure for $failed', ({ error, failure }) => {
    expect(classify(error)).toBe(failure);
  });
});
