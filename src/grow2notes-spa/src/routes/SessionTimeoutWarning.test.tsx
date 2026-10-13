import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import { api } from '../api/api.ts';
import type { Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { getPhase } from '../api/session.ts';
import { buttonClass } from '../components/buttonClass.ts';
import { routes } from './routes.tsx';

const mePath = '/api/auth/me';
const antiforgeryPath = '/api/auth/antiforgery';
const pingPath = '/api/auth/ping';
const optionsPath = '/api/auth/passkey/options';
const antiforgeryToken = 'made-up-token';

const twoMinutes = 'You’ll be signed out in 2 minutes';
const oneMinute = 'You’ll be signed out in 1 minute';
const fortySeconds = 'You’ll be signed out in 40 seconds';
const twentySeconds = 'You’ll be signed out in 20 seconds';
const connecting = 'Connecting…';
const noConnection = 'No connection. Try again.';

// When each test renders the shell, which starts the session clock and asks /me at once, on a whole second.
const startTime = Date.parse('2026-10-12T09:00:00+11:00');

// A made-up worker and manager (A35), in the JSON that MeEndpointTests asserts.
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

const fetchMock = vi.fn<typeof fetch>();

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

// How the server meets each GET /api/auth/me, and each POST /api/auth/ping in turn.
let meAnswer: Answer;
let pings: Answer[];

/** A `200` with `body` as JSON. */
function ok(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An answer with `status`: a `204` with no body, or any other as a problem (design.md §6.1). */
function status(code: number): Answer {
  const problem = JSON.stringify({ type: 'about:blank', title: 'Problem', status: code });
  return () =>
    Promise.resolve(
      code === 204
        ? new Response(null, { status: code })
        : new Response(problem, { status: code, headers: { 'Content-Type': 'application/problem+json' } }),
    );
}

/** No connection, which `fetch` rejects at once. */
const offline: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** A server that never answers: `fetch` rejects only when its signal aborts, with the signal's reason. */
const noAnswer: Answer = (init) =>
  new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });

/** Each ping sent, as its method and the antiforgery token it carried. */
function pingsSent() {
  return fetchMock.mock.calls
    .filter(([path]) => path === pingPath)
    .map(([, init]) => ({ method: init?.method, token: new Headers(init?.headers).get('X-XSRF-TOKEN') }));
}

/** How many requests have gone to `GET /api/auth/me`. */
function meRequests() {
  return fetchMock.mock.calls.filter(([path]) => path === mePath).length;
}

/** Renders the app at `path` on a memory router, under a query client of its own. */
function renderShell(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

/** `clock` as a time since the start, as '28:00', or with hours first, as '12:00:00'. */
function since(clock: string): number {
  return clock.split(':').reduce((total, part) => total * 60 + Number(part), 0) * 1000;
}

/** Moves the fake clock on to `clock` after the start, running each timer, promise and render due by then. */
async function at(clock: string) {
  const ms = startTime + since(clock) - Date.now();
  if (ms < 0) {
    throw new Error(`The clock is past ${clock} already.`);
  }

  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Moves the fake clock on by `ms`, running each timer, promise and render due by then. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** The session warning, which is always in the page. */
function warning(): HTMLDialogElement {
  const dialog = document.querySelector('dialog');
  if (dialog === null) {
    throw new Error('There is no <dialog>.');
  }

  return dialog;
}

// Its parts, found while it is closed too, when the page hides them.
const heading = () => within(warning()).getByRole('heading', { level: 2, hidden: true });
const line = () => within(warning()).getByRole('status', { hidden: true });
const staySignedIn = () => within(warning()).getByRole('button', { name: 'Stay signed in', hidden: true });

/** Renders the shell for the worker at Today, and moves on to 28:00, as the warning opens. */
async function openWarning() {
  meAnswer = ok(worker);
  renderShell('/');
  await at('28:00');
}

/** Presses Stay signed in, with focus on it, as a click on it gives. */
function press() {
  const button = staySignedIn();
  button.focus();
  fireEvent.click(button);
}

let showModal: MockInstance<HTMLDialogElement['showModal']>;
let close: MockInstance<HTMLDialogElement['close']>;

beforeEach(async () => {
  vi.useFakeTimers();
  vi.setSystemTime(startTime);
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockImplementation((path, init) => {
    switch (path) {
      case antiforgeryPath:
        return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': antiforgeryToken } }));
      case mePath:
        return meAnswer(init);
      case optionsPath:
        return ok({ challenge: 'Y2hhbGxlbmdl', userVerification: 'required' })();
      case pingPath: {
        const ping = pings.shift();
        if (ping === undefined) {
          throw new Error('The test gave no answer for this ping.');
        }

        return ping(init);
      }
      default:
        throw new Error('The test gave no answer for this request.');
    }
  });

  // api() holds an antiforgery token from now on, as after any earlier change, so that each Stay signed in sends its
  // ping alone: the token's own request would be an answer that starts the clock afresh by itself. The clock has not
  // started, so this request moves nothing.
  pings = [status(204)];
  await api(pingPath, { method: 'POST' });
  fetchMock.mockClear();

  showModal = vi.spyOn(HTMLDialogElement.prototype, 'showModal');
  close = vi.spyOn(HTMLDialogElement.prototype, 'close');
});

afterEach(() => {
  fetchMock.mockReset();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('SessionTimeoutWarning', () => {
  it('is the last element, after <main>, closed, with its heading, an empty status line and its button', async () => {
    meAnswer = ok(worker);
    const { container } = renderShell('/');
    await wait(0);

    const dialog = warning();
    expect(container.lastElementChild).toBe(dialog);
    expect(dialog.previousElementSibling).toBe(screen.getByRole('main'));
    expect(dialog).not.toHaveAttribute('open');
    expect(Array.from(dialog.children, (child) => child.localName)).toEqual(['h2', 'p', 'button']);

    expect(dialog).toHaveAttribute('aria-labelledby', heading().id);
    expect(heading()).toHaveAttribute('aria-live', 'polite');
    expect(heading()).toHaveAttribute('aria-atomic', 'true');
    // Its words from the first render, so that opening it writes nothing into its live region.
    expect(heading()).toHaveTextContent(twoMinutes);
    expect(dialog.children[1]).toBe(line());
    expect(line()).toBeEmptyDOMElement();

    const button = staySignedIn();
    expect(button).toHaveAttribute('type', 'button');
    expect(button).toHaveClass(...buttonClass('primary').split(' '));
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(button).not.toHaveAttribute('disabled');
    expect(showModal).not.toHaveBeenCalled();
  });

  it('opens at 28:00, steps at 29:00, 29:20 and 29:40 alone, and closes at 30:00 with nothing sent', async () => {
    meAnswer = ok(worker);
    renderShell('/');
    await at('27:59');
    const sent = fetchMock.mock.calls.length;
    const dialog = warning();

    expect(dialog).not.toHaveAttribute('open');

    // Each second from 28:00 to 30:00 at which anything in the dialog changed, with what it then shows.
    const changes: string[] = [];
    let shown = dialog.outerHTML;
    for (let second = 28 * 60; second <= 30 * 60; second += 1) {
      await wait(1_000);
      if (dialog.outerHTML !== shown) {
        shown = dialog.outerHTML;
        const time = `${Math.floor(second / 60)}:${String(second % 60).padStart(2, '0')}`;
        changes.push(`${time} ${dialog.open ? 'open' : 'closed'}: ${heading().textContent}`);
      }
    }

    expect(changes).toEqual([
      `28:00 open: ${twoMinutes}`,
      `29:00 open: ${oneMinute}`,
      `29:20 open: ${fortySeconds}`,
      `29:40 open: ${twentySeconds}`,
      `30:00 closed: ${twoMinutes}`,
    ]);
    expect(showModal).toHaveBeenCalledOnce();
    expect(close).toHaveBeenCalledOnce();
    expect(line()).toBeEmptyDOMElement();
    expect(fetchMock).toHaveBeenCalledTimes(sent);
  });

  it('is named by its heading once open', async () => {
    await openWarning();

    expect(screen.getByRole('dialog')).toHaveAccessibleName(twoMinutes);
    expect(within(screen.getByRole('dialog')).getByRole('button')).toHaveAccessibleName('Stay signed in');
  });

  it.each([
    { role: 'a worker', me: worker },
    { role: 'a manager', me: manager },
  ])('for $role, closes the Account panel and moves focus to Account before it opens', async ({ me }) => {
    meAnswer = ok(me);
    renderShell('/');
    await wait(0);
    const account = screen.getByRole('button', { name: 'Account' });
    fireEvent.click(account);
    const panel = document.getElementById('account-panel');
    if (panel === null) {
      throw new Error('There is no #account-panel.');
    }
    within(panel).getByRole('button', { name: 'Sign out' }).focus();
    // What the page has as the warning opens.
    const seen: { panelHidden: HTMLElement['hidden']; focused: Element | null }[] = [];
    showModal.mockImplementation(function showModal(this: HTMLDialogElement) {
      seen.push({ panelHidden: panel.hidden, focused: document.activeElement });
      this.open = true;
    });

    await at('28:00');

    expect(seen).toEqual([{ panelHidden: true, focused: account }]);
    expect(account).toHaveAttribute('aria-expanded', 'false');
    expect(warning()).toHaveAttribute('open');
  });

  it.each([
    // The passkey options that view A asks for are an answer, which starts the clock afresh after /me's 401 ended it.
    { where: "sign-in's view A", path: '/', me: status(401), webAuthn: true },
    { where: 'the start-up message', path: '/', me: offline, webAuthn: false },
    { where: 'a setup page', path: '/setup', me: ok(worker), webAuthn: false },
  ])('never opens over $where, with the clock in its warning', async ({ path, me, webAuthn }) => {
    if (webAuthn) {
      // jsdom has no WebAuthn, and browserSupportsWebAuthn() asks only for the constructor.
      vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
    }
    meAnswer = me;
    renderShell(path);

    await at('28:00');
    expect(getPhase()).toBe('warn-2m');
    await at('29:59');

    expect(getPhase()).toBe('warn-20s');
    expect(showModal).not.toHaveBeenCalled();
    expect(warning()).not.toHaveAttribute('open');
  });

  describe('Stay signed in', () => {
    it('sends one POST /api/auth/ping with the token, open and aria-disabled while it is out', async () => {
      pings = [noAnswer];
      await openWarning();

      press();
      await wait(0);
      fireEvent.click(staySignedIn());
      fireEvent.click(staySignedIn());
      await wait(5_000);

      expect(pingsSent()).toEqual([{ method: 'POST', token: antiforgeryToken }]);
      expect(warning()).toHaveAttribute('open');
      expect(close).not.toHaveBeenCalled();
      expect(staySignedIn()).toHaveAttribute('aria-disabled', 'true');
      expect(staySignedIn()).not.toHaveAttribute('disabled');
      expect(staySignedIn()).toHaveFocus();
    });

    it('says "Connecting…" from 1,000 ms, and not at 999 ms, with its own label and focus kept', async () => {
      pings = [noAnswer];
      await openWarning();

      press();
      await wait(999);

      expect(line()).toBeEmptyDOMElement();

      await wait(1);

      expect(line()).toHaveTextContent(connecting);
      expect(staySignedIn()).toHaveAccessibleName('Stay signed in');
      expect(staySignedIn()).toHaveFocus();
    });

    // Any answer but a 401 is one the server accepted, so the clock starts afresh from the ping's send time.
    it.each([
      { answer: 'a 204', ping: status(204) },
      { answer: 'a 503', ping: status(503) },
    ])('closes on $answer, saying nothing, and opens again 28 minutes after the ping', async ({ ping }) => {
      pings = [ping];
      await openWarning();

      press();
      await wait(0);

      expect(close).toHaveBeenCalledOnce();
      expect(warning()).not.toHaveAttribute('open');
      expect(getPhase()).toBe('active');
      expect(line()).toBeEmptyDOMElement();

      await at('55:59');
      expect(warning()).not.toHaveAttribute('open');
      await at('56:00');
      expect(warning()).toHaveAttribute('open');
      expect(line()).toBeEmptyDOMElement();
    });

    it('closes on a 401, with the clock ended, and stays closed', async () => {
      pings = [status(401)];
      await openWarning();

      press();
      await wait(0);

      expect(close).toHaveBeenCalledOnce();
      expect(warning()).not.toHaveAttribute('open');
      expect(getPhase()).toBe('expired');
      await at('60:00');
      expect(showModal).toHaveBeenCalledOnce();
    });

    it.each([
      { failure: 'no connection', ping: offline, ms: 0 },
      { failure: 'no answer in 10 seconds', ping: noAnswer, ms: 10_000 },
    ])('stays open on $failure, saying so, ready, with the countdown going on', async ({ ping, ms }) => {
      pings = [ping];
      await openWarning();

      press();
      await wait(ms);

      expect(warning()).toHaveAttribute('open');
      expect(close).not.toHaveBeenCalled();
      expect(line()).toHaveTextContent(noConnection);
      expect(staySignedIn()).not.toHaveAttribute('aria-disabled');
      expect(staySignedIn()).toHaveFocus();
      expect(heading()).toHaveTextContent(twoMinutes);

      await at('29:00');

      expect(heading()).toHaveTextContent(oneMinute);
      expect(line()).toHaveTextContent(noConnection);
    });

    it('empties the line when pressed again, before its request, and says the new failure', async () => {
      const linesWhenSent: (string | null)[] = [];
      /** `ping`, noting the status line's words as the request is sent. */
      function noting(ping: Answer): Answer {
        return (init) => {
          linesWhenSent.push(line().textContent);
          return ping(init);
        };
      }
      pings = [noting(offline), noting(offline)];
      await openWarning();
      press();
      await wait(0);
      expect(line()).toHaveTextContent(noConnection);

      press();

      expect(line()).toBeEmptyDOMElement();
      await wait(0);
      expect(linesWhenSent).toEqual(['', '']);
      expect(line()).toHaveTextContent(noConnection);
    });
  });

  describe('Escape and Android back', () => {
    it('take cancel as Stay signed in, sending one ping however often it comes, with the dialog open', async () => {
      pings = [noAnswer];
      await openWarning();
      const first = new Event('cancel', { cancelable: true });
      const second = new Event('cancel', { cancelable: true });

      act(() => {
        warning().dispatchEvent(first);
      });
      await wait(0);
      // A second Escape or back while the ping is out.
      act(() => {
        warning().dispatchEvent(second);
      });
      await wait(0);

      expect(first.defaultPrevented).toBe(true);
      expect(second.defaultPrevented).toBe(true);
      expect(pingsSent()).toHaveLength(1);
      expect(warning()).toHaveAttribute('open');
      expect(close).not.toHaveBeenCalled();
      expect(staySignedIn()).toHaveAttribute('aria-disabled', 'true');
    });

    // As Chromium closes a dialog that a timer opened, with no `cancel`, when there has been no user activation since.
    it('take a close in a warning phase as Stay signed in, opening again with the error on no connection', async () => {
      pings = [offline];
      await openWarning();

      act(() => {
        warning().close();
      });
      // The `close` event comes in a task of its own.
      await wait(0);

      expect(pingsSent()).toHaveLength(1);
      expect(showModal).toHaveBeenCalledTimes(2);
      expect(warning()).toHaveAttribute('open');
      expect(line()).toHaveTextContent(noConnection);
      expect(heading()).toHaveTextContent(twoMinutes);
    });

    it('leave it closed when the ping for a close in a warning phase gets its 204', async () => {
      pings = [status(204)];
      await openWarning();

      act(() => {
        warning().close();
      });
      await wait(0);

      expect(pingsSent()).toHaveLength(1);
      expect(showModal).toHaveBeenCalledOnce();
      expect(warning()).not.toHaveAttribute('open');
      expect(getPhase()).toBe('active');
    });

    // The browser fires `close` a frame or more after the close, so a clock that runs on at once, as Playwright's does
    // in the end-to-end test, can open the warning again before the event of the close before comes.
    it('take a close that finds it open again as nothing', async () => {
      await openWarning();

      act(() => {
        warning().dispatchEvent(new Event('close'));
      });
      await wait(0);

      expect(pingsSent()).toEqual([]);
      expect(warning()).toHaveAttribute('open');
      expect(close).not.toHaveBeenCalled();
    });
  });

  // The 12-hour limit is the session's whole length, which Stay signed in cannot extend (app-shell.md *Tensions* 2).
  it('never opens with a request accepted every 20 minutes for 12 hours, nor on the 401 after', async () => {
    meAnswer = ok(worker);
    renderShell('/');
    await wait(0);

    for (let minutes = 20; minutes < 12 * 60; minutes += 20) {
      await at(`${minutes}:00`);
      act(() => {
        window.dispatchEvent(new FocusEvent('focus'));
      });
    }
    meAnswer = status(401);
    await at('12:00:00');
    act(() => {
      window.dispatchEvent(new FocusEvent('focus'));
    });
    await at('12:40:00');

    expect(meRequests()).toBe(37);
    expect(getPhase()).toBe('expired');
    expect(showModal).not.toHaveBeenCalled();
    expect(warning()).not.toHaveAttribute('open');
  });
});
