import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../api/queryClient.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import { routes, type AppRoute } from '../routes.tsx';
import { useSetup } from './setupContext.ts';

const antiforgeryPath = '/api/auth/antiforgery';
const mePath = '/api/auth/me';
const startPath = '/api/auth/setup/start';
const keyPath = '/api/auth/setup/authenticator';
const title = 'Grow2Notes – Set up your account';
const hint = 'Use at least 12 characters.';
const enterPassword = 'Enter a password';
const tooShort = 'Password must be 12 characters or more';

// A made-up person and link (A35).
const account = { email: 'sam.lee@example.org', displayName: 'Sam Lee' };
const setupLink = '/setup#u=6f9619ff-8b86-d011-b42d-00cf4fc964ff&t=made-up-token';

const fetchMock = vi.fn<typeof fetch>();

/** The path of each request sent, but the shell's own `GET /api/auth/me` and view G's `GET` of its key, in order. */
function pageRequests(): unknown[] {
  return fetchMock.mock.calls.map(([path]) => path).filter((path) => path !== mePath && path !== keyPath);
}

/** Shows what the setup layout holds: the account's email and the password, as held. */
function SetupMemory() {
  const { account: held, password } = useSetup();
  return (
    <>
      <p data-testid="setup-account">{held?.email ?? ''}</p>
      <p data-testid="setup-password">{password ?? ''}</p>
    </>
  );
}

/** The app's route table, with the setup layout's memory shown under each of its pages. */
function withSetupMemory(table: AppRoute[]): AppRoute[] {
  return table.map((route) => {
    if (route.index) {
      return route;
    }

    const children =
      route.path === 'setup'
        ? route.children?.map((child) => ({ ...child, element: <>{child.element}<SetupMemory /></> }))
        : route.children && withSetupMemory(route.children);
    return { ...route, children };
  });
}

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Opens the app at `path` on a memory router, under a query client of its own and StrictMode. */
async function open(path: string) {
  const router = createMemoryRouter(withSetupMemory(routes), { initialEntries: [path] });
  render(
    <StrictMode>
      <QueryClientProvider client={createQueryClient()}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  await wait(0);
  return router;
}

/** Opens the emailed link and follows view E's password link to view F, as a person does. */
async function openViewF() {
  const router = await open(setupLink);
  fireEvent.click(screen.getByRole('link', { name: 'Use a password and authenticator app' }));
  await wait(0);
  expect(router.state.location.pathname).toBe('/setup/password');
  return router;
}

function heading() {
  return screen.getByRole('heading', { level: 1 });
}

function field(): HTMLInputElement {
  return screen.getByLabelText<HTMLInputElement>('Create a password');
}

function toggle() {
  return screen.getByRole('button', { name: /^(Show|Hide) password$/ });
}

function continueButton() {
  return screen.getByRole('button', { name: 'Continue' });
}

function backLink() {
  return screen.getByRole('link', { name: 'Set up your account' });
}

/** The toggle's polite line. */
function toggleLine() {
  const line = screen.getByRole('main').querySelector('[aria-live="polite"]');
  if (line === null) {
    throw new Error('View F rendered no polite line.');
  }

  return line;
}

function usernameField(): HTMLInputElement {
  const username = screen.getByRole('main').querySelector<HTMLInputElement>('input[name="username"]');
  if (username === null) {
    throw new Error('View F rendered no username field.');
  }

  return username;
}

/** Types `value` into the field, as it is, and presses Continue as a keyboard user would. */
function submit(value: string) {
  fireEvent.change(field(), { target: { value } });
  continueButton().focus();
  fireEvent.click(continueButton());
}

/**
 * The elements that Tab reaches, in order: what is focusable, neither hidden nor in a closed dialog, such as the
 * session warning, and not taken out with `tabindex="-1"`.
 */
function tabStops(): Element[] {
  const focusable = document.body.querySelectorAll<HTMLElement>('a[href], button, input, select, textarea, [tabindex]');
  return Array.from(focusable).filter(
    (element) => element.closest('[hidden], dialog:not([open])') === null && element.tabIndex >= 0,
  );
}

/** Each element comes after the one before it in the page. */
function expectInOrder(...elements: Element[]) {
  for (const [index, element] of elements.slice(1).entries()) {
    expect(elements[index].compareDocumentPosition(element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  }
}

beforeEach(() => {
  vi.useFakeTimers();
  fetchMock.mockImplementation((path) => {
    if (path === antiforgeryPath) {
      return Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'made-up' } }));
    }

    // The shell's, for a person with no session.
    if (path === mePath) {
      return Promise.resolve(new Response(null, { status: 401 }));
    }

    if (path === startPath) {
      return Promise.resolve(
        new Response(JSON.stringify(account), { headers: { 'Content-Type': 'application/json' } }),
      );
    }

    // View G's, once Continue has gone on to it.
    if (path === keyPath) {
      return Promise.resolve(
        new Response(JSON.stringify({ sharedKey: 'GROWTWONOTESMADEUPSETUPKEY234567' }), {
          headers: { 'Content-Type': 'application/json' },
        }),
      );
    }

    throw new Error('View F sends no request.');
  });
  vi.stubGlobal('fetch', fetchMock);
  document.title = '';
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('CreatePassword (view F)', () => {
  it('shows the back link, then a form of the h1 labelling the field, the hint, field, Show and Continue', async () => {
    await openViewF();

    const pageBar = document.getElementById('page-bar');
    expect(pageBar).toContainElement(backLink());
    expect(backLink()).toHaveAttribute('href', '/setup');

    const main = within(screen.getByRole('main'));
    const form = heading().closest('form');
    expect(form).not.toBeNull();
    expect(form?.noValidate).toBe(true);
    expect(heading()).toHaveTextContent('Create a password');
    expect(heading()).toHaveFocus();
    expect(heading()).toContainElement(screen.getByText('Create a password', { selector: 'label' }));

    // Not the arrival focus, which only the server's refusal makes it (AuthenticatorSetup.test.tsx).
    expect(field()).not.toHaveAttribute('data-route-focus');
    expect(field()).toHaveAttribute('type', 'password');
    expect(field()).toHaveAttribute('autocomplete', 'new-password');
    expect(field()).toHaveAttribute('minlength', '12');
    expect(field()).toHaveAttribute('spellcheck', 'false');
    expect(field()).toHaveAttribute('autocapitalize', 'none');
    expect(field()).toHaveAttribute('autocorrect', 'off');
    expect(field()).toHaveValue('');
    expect(field()).toHaveAttribute('aria-describedby', 'password-hint');
    expect(field()).toHaveAccessibleDescription(hint);
    expect(field()).not.toHaveAttribute('aria-invalid');

    expect(toggle()).toHaveAccessibleName('Show password');
    expect(toggle()).toHaveTextContent('Show');
    expect(toggle()).toHaveAttribute('type', 'button');
    expect(toggle()).toHaveAttribute('aria-controls', field().id);
    expect(toggle()).toHaveStyle({ minBlockSize: 'var(--target-button)' });
    // There from the start, and saying nothing until Show is pressed.
    expect(toggleLine()).toBeEmptyDOMElement();

    expect(continueButton()).toHaveAttribute('type', 'submit');
    expect(continueButton()).toHaveClass(...buttonClass('primary').split(' '));
    expect(continueButton()).not.toHaveAttribute('aria-disabled');

    expect(main.queryByRole('alert')).not.toBeInTheDocument();
    expectInOrder(
      heading(),
      main.getByText(hint),
      field(),
      toggle(),
      toggleLine(),
      usernameField(),
      continueButton(),
    );
    expect(form).toContainElement(continueButton());
    expect(document.title).toBe(title);
  });

  it('has no confirm field, strength meter, list of rules or maxLength', async () => {
    await openViewF();

    const form = heading().closest('form');
    expect(form?.querySelectorAll('input[type="password"]')).toHaveLength(1);
    expect(form?.querySelectorAll('input:not([hidden])')).toHaveLength(1);
    expect(field()).not.toHaveAttribute('maxlength');
    const main = within(screen.getByRole('main'));
    expect(main.queryByRole('meter')).not.toBeInTheDocument();
    expect(main.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(main.queryByRole('list')).not.toBeInTheDocument();
  });

  it('holds the email in a hidden username field, which takes no focus', async () => {
    await openViewF();

    const username = usernameField();
    expect(username.form).toBe(field().form);
    expect(username).toHaveAttribute('type', 'email');
    expect(username).toHaveAttribute('autocomplete', 'username');
    expect(username).toHaveValue(account.email);
    expect(username).toHaveAttribute('readonly');
    expect(username).toHaveAttribute('hidden');
    expect(username).not.toBeVisible();
    expect(tabStops()).not.toContain(username);
  });

  it('is reached by Tab in the order back link, field, Show, Continue, after the skip link', async () => {
    await openViewF();

    expect(tabStops()).toEqual([
      screen.getByRole('link', { name: 'Skip to main content' }),
      backLink(),
      field(),
      toggle(),
      continueButton(),
    ]);
  });

  describe('on Continue', () => {
    it.each([
      { value: '', kind: 'an empty field', message: enterPassword },
      { value: ' '.repeat(12), kind: 'twelve spaces', message: enterPassword },
      { value: '\t', kind: 'a tab', message: enterPassword },
      { value: ' '.repeat(12), kind: 'twelve no-break spaces', message: enterPassword },
      { value: 'abcdefghijk', kind: '11 characters', message: tooShort },
    ])('says "$message" for $kind, on the field, with focus there, and sends nothing', async ({ value, message }) => {
      const router = await openViewF();
      const requests = pageRequests().length;

      submit(value);
      await wait(0);

      expect(field()).toHaveFocus();
      expect(field()).toHaveAttribute('aria-invalid', 'true');
      expect(field()).toHaveAttribute('aria-describedby', 'password-hint password-error');
      expect(field()).toHaveAccessibleDescription(`${hint} Error: ${message}`);
      expectInOrder(screen.getByText(hint), screen.getByText(message), field());
      expect(field()).toHaveValue(value);
      expect(document.title).toBe(`Error: ${title}`);
      expect(router.state.location.pathname).toBe('/setup/password');
      expect(screen.getByTestId('setup-password')).toBeEmptyDOMElement();
      expect(pageRequests()).toHaveLength(requests);
    });

    it('moves focus to the field again on each press that finds the same message', async () => {
      await openViewF();
      submit('');
      await wait(0);
      expect(field()).toHaveFocus();

      submit('');
      await wait(0);

      expect(field()).toHaveFocus();
      expect(screen.getAllByText(enterPassword)).toHaveLength(1);
    });

    it.each([
      { value: 'abcdefghijkl', kind: '12 lowercase letters' },
      { value: 'éàüøçñåßîôûë', kind: '12 accented letters' },
      { value: ' abcdefghij ', kind: '12 characters with spaces round them, kept' },
      { value: 'abcdefghijk ', kind: '11 letters and a space' },
    ])('puts $kind in setup\'s memory as typed and pushes /setup/app, sending nothing', async ({ value }) => {
      const router = await openViewF();
      const requests = pageRequests();

      submit(value);
      await wait(0);

      expect(router.state.historyAction).toBe('PUSH');
      expect(router.state.location.pathname).toBe('/setup/app');
      expect(screen.getByTestId('setup-password').textContent).toBe(value);
      expect(screen.getByTestId('setup-account')).toHaveTextContent(account.email);
      expect(pageRequests()).toEqual(requests);
      expect(router.state.location.state).toBeNull();
      expect(localStorage).toHaveLength(0);
      expect(sessionStorage).toHaveLength(0);
      expect(document.title).toBe(title);
    });

    it('checks again as the person types after a message, and takes it and the title\'s "Error: " away', async () => {
      await openViewF();
      submit('');
      await wait(0);

      fireEvent.change(field(), { target: { value: 'abc' } });
      expect(screen.getByText(tooShort)).toBeInTheDocument();
      expect(screen.queryByText(enterPassword)).not.toBeInTheDocument();
      expect(document.title).toBe(`Error: ${title}`);

      fireEvent.change(field(), { target: { value: 'abcdefghijkl' } });
      await wait(0);

      expect(screen.queryByText(tooShort)).not.toBeInTheDocument();
      expect(field()).not.toHaveAttribute('aria-invalid');
      expect(field()).toHaveAttribute('aria-describedby', 'password-hint');
      expect(document.title).toBe(title);
    });

    it('shows no message as the person types before the first press', async () => {
      await openViewF();

      fireEvent.change(field(), { target: { value: 'abc' } });
      await wait(0);

      expect(screen.queryByText(tooShort)).not.toBeInTheDocument();
      expect(field()).not.toHaveAttribute('aria-invalid');
    });
  });

  describe('Show and Hide', () => {
    it('reveal and hide the password, keep focus and say so in the polite line', async () => {
      await openViewF();
      fireEvent.change(field(), { target: { value: 'abcdefghijkl' } });
      const button = toggle();
      button.focus();

      fireEvent.click(button);

      expect(field()).toHaveAttribute('type', 'text');
      expect(button).toHaveAccessibleName('Hide password');
      expect(button).toHaveTextContent('Hide');
      expect(toggleLine()).toHaveTextContent('Your password is visible');
      expect(button).toHaveFocus();

      fireEvent.click(button);

      expect(field()).toHaveAttribute('type', 'password');
      expect(button).toHaveAccessibleName('Show password');
      expect(button).toHaveTextContent('Show');
      expect(toggleLine()).toHaveTextContent('Your password is hidden');
      expect(button).toHaveFocus();
      expect(field()).toHaveValue('abcdefghijkl');
    });

    it('hide the password again when Continue is pressed', async () => {
      await openViewF();
      fireEvent.click(toggle());
      expect(field()).toHaveAttribute('type', 'text');

      submit('abcdefghijk');
      await wait(0);

      expect(field()).toHaveAttribute('type', 'password');
      expect(toggle()).toHaveAccessibleName('Show password');
      expect(toggleLine()).toHaveTextContent('Your password is hidden');
      expect(field()).toHaveFocus();
    });
  });

  it('goes back one entry to view E on the back link, adding none, with the h1 focused', async () => {
    const router = await openViewF();
    submit('');
    await wait(0);

    fireEvent.click(backLink());
    await wait(0);

    expect(router.state.historyAction).toBe('POP');
    expect(router.state.location.pathname).toBe('/setup');
    expect(heading()).toHaveTextContent('Set up your account');
    expect(heading()).toHaveFocus();
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Choose how you’ll sign in');
    expect(document.getElementById('page-bar')).toBeEmptyDOMElement();
    expect(document.title).toBe(title);
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/setup/password');
  });

  it('starts with the password in memory when the person comes back from /setup/app', async () => {
    const router = await openViewF();
    submit(' abcdefghij ');
    await wait(0);
    expect(router.state.location.pathname).toBe('/setup/app');

    await act(() => router.navigate(-1));
    await wait(0);

    expect(router.state.location.pathname).toBe('/setup/password');
    expect(field()).toHaveValue(' abcdefghij ');
    expect(field()).toHaveAttribute('type', 'password');
    expect(heading()).toHaveFocus();
    expect(toggleLine()).toBeEmptyDOMElement();
  });

  it('shows the no-link message with nothing in memory, with no back link or form, and sends nothing', async () => {
    await open('/setup/password');
    await wait(5000);

    expect(heading()).toHaveTextContent('Set up your account');
    expect(heading()).toHaveFocus();
    expect(heading().nextElementSibling).toHaveTextContent(
      'To set up your account, open the link from your email again.',
    );
    expect(document.getElementById('page-bar')).toBeEmptyDOMElement();
    expect(screen.getByRole('main').querySelector('form, input')).toBeNull();
    expect(pageRequests()).toEqual([]);
    expect(document.title).toBe(title);
  });
});
