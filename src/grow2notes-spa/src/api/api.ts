import { holdWhile, requestAccepted, sessionEnded, signedIn, signInCount } from './session.ts';

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

// The antiforgery endpoint, and the header in which its token comes and goes back with each change (design.md §9.8
// item 2).
const antiforgeryPath = '/api/auth/antiforgery';
const antiforgeryHeader = 'X-XSRF-TOKEN';

// The four calls that sign someone in, those that answer 200 Me (design.md §6.2). A token is bound to the user it was
// issued to, so after one of them the next change fetches a token for the person now signed in.
const signIns = new Set([
  '/api/auth/login/totp',
  '/api/auth/passkey',
  '/api/auth/setup/password',
  '/api/auth/setup/passkey',
]);

// The antiforgery token, held here only, never in storage or a cookie that script can read (§9.8 item 2), as the
// promise of its request, so that changes sent together wait on one request.
let antiforgeryToken: Promise<string> | undefined;

type Answer = {
  response: Response;
  text: string;
  /** `signInCount()` as the request went, which says which session cookie it carried. */
  signInsWhenSent: number;
};

// A `2xx` answer: its JSON, or nothing when it has no body, and its headers.
type Reply<T> = { body: T; headers: Headers };

/**
 * Sends a request to the API and gives the JSON of its `2xx` answer, or nothing when that answer has no body. A
 * `POST`, `PUT` or `DELETE` carries the antiforgery token, fetched first when none is held. Throws `ApiError` for any
 * other answer, and with status `0` when no whole answer came within `timeoutMs`; a `401` that means the session has
 * ended raises the session-ended event first.
 */
export function api<T>(path: string, options: ApiOptions = {}): Promise<T> {
  return heldRequest<T>(path, options).then(({ body }) => body);
}

/** A `2xx` answer's body, as `api()` gives it, with the two headers that a screen that saves reads. */
export type WithHeaders<T> = {
  body: T;
  /** The token of the answer's `ETag`, without its quotes, which goes back as `ifMatch`; `undefined` with none. */
  etag: string | undefined;
  /** The answer's `Date`, the server's clock, in UTC, as `time()` takes it; `undefined` with none. */
  dateUtc: string | undefined;
};

/**
 * Sends a request as `api()` does, and fails as it does, but gives its `2xx` answer's `ETag` and `Date` with the body:
 * the row's version for the next change's `If-Match`, and the time a save was done (design.md §6.6, D84;
 * status-messages.md `SaveStatus`).
 */
export function apiWithHeaders<T>(path: string, options: ApiOptions = {}): Promise<WithHeaders<T>> {
  return heldRequest<T>(path, options).then(({ body, headers }) => ({
    body,
    etag: entityTagToken(headers.get('ETag')),
    dateUtc: utcTime(headers.get('Date')),
  }));
}

// The session clock holds while the call is out, as its answer may start the clock afresh or end it
// (session-timeout.md *The session clock*).
function heldRequest<T>(path: string, options: ApiOptions): Promise<Reply<T>> {
  const call = request<T>(path, options);
  holdWhile(call);
  return call;
}

async function request<T>(path: string, options: ApiOptions): Promise<Reply<T>> {
  const { method = 'GET', body, ifMatch, timeoutMs = defaultTimeoutMs, signal } = options;
  const headers: Record<string, string> = {};
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (ifMatch !== undefined) {
    headers['If-Match'] = entityTag(ifMatch);
  }

  const json = body === undefined ? undefined : JSON.stringify(body);
  const send = (token?: string) =>
    exchange(
      path,
      { method, headers: token === undefined ? headers : { ...headers, [antiforgeryHeader]: token }, body: json },
      timeoutMs,
      signal,
    );
  const { response, text, signInsWhenSent } = method === 'GET' ? await send() : await sendChange(send);

  if (response.ok) {
    if (signIns.has(path)) {
      antiforgeryToken = undefined;
      signedIn();
    }

    // Several writes answer a bare 200 or 201 with no body (design.md §6.6), on which `response.json()` would throw.
    return { body: (text === '' ? undefined : JSON.parse(text)) as T, headers: response.headers };
  }

  // Not for a request sent before the newest sign-in, which went with the cookie from before, so that its 401 neither
  // ends the clock nor shows the signed-out view over the new session (T01's Notes).
  if (response.status === 401 && endsSession(path) && signInsWhenSent === signInCount()) {
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
 * Whether `error` is a refusal that Page not found shows in place of the screen (app-shell.md component 4): a `404`,
 * or a `403` from a manager-only request, which the app tells apart from a `404` no more than the API does (design.md
 * §9.2). A `403 note.not_editable` is not one, as the note exists and the person may read it (D20).
 */
export function isNotFound(error: unknown): error is ApiError {
  return (
    error instanceof ApiError && (error.status === 404 || (error.status === 403 && error.code !== 'note.not_editable'))
  );
}

/**
 * Sends a change with the antiforgery token. A `400` with no `code` is how the `/api` filter refuses a missing or
 * stale token, so such a change is sent once more, with the same body and a fresh token.
 */
async function sendChange(send: (token: string) => Promise<Answer>): Promise<Answer> {
  const token = heldToken();
  const answer = await send(await token);
  if (answer.response.status !== 400 || failure(400, answer.text).code !== undefined) {
    return answer;
  }

  forgetToken(token);
  return send(await heldToken());
}

// The token held, or the request for one. A request that fails is not kept, so that the next change asks again.
function heldToken(): Promise<string> {
  if (antiforgeryToken === undefined) {
    const request = requestToken();
    antiforgeryToken = request;
    void request.catch(() => {
      forgetToken(request);
    });
  }

  return antiforgeryToken;
}

// Forgets `token` only while it is still the one held, so that changes refused together share one fresh request: a
// token request sent with no valid antiforgery cookie sets a new one, which another such request's token cannot match.
function forgetToken(token: Promise<string>): void {
  if (antiforgeryToken === token) {
    antiforgeryToken = undefined;
  }
}

// From the SPA's own origin, as the endpoint refuses a request from another (AntiforgeryEndpoint), and with a timeout
// of its own. A request that fails fails its change with its own status.
async function requestToken(): Promise<string> {
  const { response, text } = await exchange(antiforgeryPath, { method: 'GET' }, defaultTimeoutMs, undefined);
  if (!response.ok) {
    throw failure(response.status, text);
  }

  const token = response.headers.get(antiforgeryHeader);
  if (!token) {
    throw new Error('The antiforgery endpoint answered with no token.');
  }

  return token;
}

/**
 * Sends the request and reads its whole body, or throws `ApiError` with status `0` when `fetch` or the reading
 * rejects, or the body is not all read after `timeoutMs`, so that an answer that stalls half-way fails too. An abort
 * by the caller's own `signal` is rethrown as it came. A request answered with anything but a `401` is activity to
 * the session clock, as the server accepted it.
 */
async function exchange(
  path: string,
  init: RequestInit,
  timeoutMs: number,
  signal: AbortSignal | undefined,
): Promise<Answer> {
  signal?.throwIfAborted();

  // Neither AbortSignal.timeout, which Vitest's fake timers cannot drive, nor AbortSignal.any, which Safari has only
  // from 17.4, newer than the Safari 16 that the build targets.
  const controller = new AbortController();
  const abortWithCaller = () => controller.abort(signal?.reason);
  signal?.addEventListener('abort', abortWithCaller);
  const timer = setTimeout(() => controller.abort(), timeoutMs);

  try {
    // The send time, not the answer's, as the server's 30 minutes start no earlier, so that a request that renewed the
    // session can make the clock end early but never late (session-timeout.md *The session clock* step 1).
    const sentAt = Date.now();
    const signInsWhenSent = signInCount();
    const response = await fetch(path, { ...init, signal: controller.signal });
    // Counted once its head has come, whatever the body does: the session check runs before the endpoint and renews
    // the session cookie in the head (design.md §8.4), so a 5xx counts too. A 5xx that renewed nothing, from App
    // Service's front end or from a database failure in the session check itself, cannot be told apart and counts as
    // well, and the server's next 401 then puts the clock right (A57).
    if (response.status !== 401) {
      requestAccepted(sentAt);
    }

    return { response, text: await response.text(), signInsWhenSent };
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

// The token of one strong entity tag, the inverse of entityTag(). A weak tag, W/"…", names no version that If-Match
// takes, so it gives none.
function entityTagToken(header: string | null): string | undefined {
  return header?.match(/^"([^"]+)"$/)?.[1];
}

// An HTTP date, such as "Thu, 01 Oct 2026 06:12:00 GMT", as an ISO 8601 time in UTC, or none for one that is not a
// date, so that a save that was done is never shown as failed.
function utcTime(header: string | null): string | undefined {
  if (header === null) {
    return undefined;
  }

  const ms = Date.parse(header);
  return Number.isNaN(ms) ? undefined : new Date(ms).toISOString();
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
