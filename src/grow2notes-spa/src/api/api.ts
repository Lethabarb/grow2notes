import { sessionEnded } from './session.ts';

/** A `422`'s field errors (design.md §6.1), each keyed by the field's name in the request's JSON. */
export type FieldErrors = Readonly<Record<string, readonly string[]>>;

/**
 * A request that got no whole answer, with status `0`, or an answer that was not `2xx`, with its status and what its
 * problem details (design.md §6.1, §6.9) say.
 */
export class ApiError extends Error {
  override name = 'ApiError';
  readonly status: number;
  /** The problem's stable `code`, when it has one. */
  readonly code: string | undefined;
  /** A `422`'s field errors, which a form shows on its fields (form-validation.md *Server responses*). */
  readonly errors: FieldErrors | undefined;

  constructor(status: number, code?: string, errors?: FieldErrors) {
    super(code ?? `HTTP ${status}`);
    this.status = status;
    this.code = code;
    this.errors = errors;
  }
}

export type ApiOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  /** Sent as JSON. */
  body?: unknown;
  /** The row's token as the API's JSON gives it, a `rowVersion` or a user's `ConcurrencyStamp` (design.md §6.6). */
  ifMatch?: string;
  /** How long the whole answer may take; the two file downloads pass 30 seconds (empty-loading-error.md *Timing*). */
  timeoutMs?: number;
  /** The caller's own signal, such as TanStack Query's, whose abort is rethrown as it came. */
  signal?: AbortSignal;
};

// The same 10 seconds as autosave and the session ping (empty-loading-error.md *Timing*).
const defaultTimeoutMs = 10_000;

/**
 * Sends a request to the API and gives the JSON of its `2xx` answer, or nothing when that answer has no body. Throws
 * `ApiError` for any other answer, and with status `0` when no whole answer came within `timeoutMs`; a `401` that
 * means the session has ended raises the session-ended event first.
 */
export async function api<T>(path: string, options: ApiOptions = {}): Promise<T> {
  const { method = 'GET', body, ifMatch, timeoutMs = defaultTimeoutMs, signal } = options;
  const headers: Record<string, string> = {};
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (ifMatch !== undefined) {
    headers['If-Match'] = entityTag(ifMatch);
  }

  const { response, text } = await exchange(
    path,
    { method, headers, body: body === undefined ? undefined : JSON.stringify(body) },
    timeoutMs,
    signal,
  );

  if (response.ok) {
    // Several writes answer a bare 200 or 201 with no body (design.md §6.6), on which `response.json()` would throw.
    return (text === '' ? undefined : JSON.parse(text)) as T;
  }

  if (response.status === 401 && endsSession(path)) {
    sessionEnded();
  }

  throw failure(response.status, text);
}

export type Failure = 'offline' | 'signedOut' | 'server';

/**
 * What kind of failure `error` is, by its status: never by `TypeError`, which a bug in a query function throws too
 * (empty-loading-error.md *Implementation notes*).
 */
export function classify(error: unknown): Failure {
  if (!(error instanceof ApiError)) {
    return 'server';
  }

  if (error.status === 0) {
    return 'offline';
  }

  return error.status === 401 ? 'signedOut' : 'server';
}

/**
 * Sends the request and reads its whole body, or throws `ApiError` with status `0` when `fetch` or the reading
 * rejects, or the body is not all read after `timeoutMs`, so that an answer that stalls half-way fails too. An abort
 * by the caller's own `signal` is rethrown as it came.
 */
async function exchange(path: string, init: RequestInit, timeoutMs: number, signal: AbortSignal | undefined) {
  signal?.throwIfAborted();

  // Neither AbortSignal.timeout, which Vitest's fake timers cannot drive, nor AbortSignal.any, which Safari has only
  // from 17.4, newer than the Safari 16 that the build targets.
  const controller = new AbortController();
  const abortWithCaller = () => controller.abort(signal?.reason);
  signal?.addEventListener('abort', abortWithCaller);
  const timer = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(path, { ...init, signal: controller.signal });
    return { response, text: await response.text() };
  } catch (error) {
    if (signal?.aborted) {
      throw error;
    }

    throw new ApiError(0);
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener('abort', abortWithCaller);
  }
}

// One strong entity tag, the token in quotes, as the server's IfMatch.ETag writes it.
function entityTag(token: string): string {
  if (token === '') {
    // The empty tag "" names no version of the row, so the server's IfMatch.ETag refuses it too.
    throw new Error('If-Match needs the row\'s token, and this one is empty.');
  }

  return `"${token}"`;
}

// A 401 from /api/auth/login*, /api/auth/passkey*, /api/auth/setup/* or /api/auth/logout is a failed sign-in, an
// ended setup session or a sign-out that counts as done (sign-in.md, app-shell.md *Account and Sign out*), not a
// session that has ended.
function endsSession(path: string): boolean {
  return !/^\/api\/auth\/(?:login|passkey|setup\/)/.test(path) && path !== '/api/auth/logout';
}

// The problem's code, when it is a string, and a 422's field errors; a body that is not JSON gives neither.
function failure(status: number, text: string): ApiError {
  let problem: unknown;
  try {
    problem = JSON.parse(text);
  } catch {
    return new ApiError(status);
  }

  if (typeof problem !== 'object' || problem === null) {
    return new ApiError(status);
  }

  const { code, errors } = problem as { code?: unknown; errors?: unknown };
  return new ApiError(
    status,
    typeof code === 'string' ? code : undefined,
    status === 422 && isFieldErrors(errors) ? errors : undefined,
  );
}

function isFieldErrors(value: unknown): value is FieldErrors {
  return (
    typeof value === 'object'
    && value !== null
    && Object.values(value).every(
      (messages) => Array.isArray(messages) && messages.every((message) => typeof message === 'string'),
    )
  );
}
