import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode, type ComponentProps } from 'react';
import { createMemoryRouter, useParams } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Me } from '../api/me.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { PageHeading } from '../components/PageHeading.tsx';
import { pageName } from '../copy/pageNames.ts';
import { routes, type AppRoute } from './routes.tsx';

// The crash that every made-up screen throws, and that the header throws while a test asks it to.
const made = vi.hoisted(() => ({ crash: new Error('A made-up crash'), headerThrows: false }));

// The header, which throws for a signed-in person while a test asks it to, as one that cannot read /me's answer does,
// and is itself otherwise: the shell's crash page draws it with the wordmark as plain text.
vi.mock('./Header.tsx', async (importOriginal) => {
  const { Header } = await importOriginal<typeof import('./Header.tsx')>();
  return {
    Header(props: ComponentProps<typeof Header>) {
      if (made.headerThrows && props.me !== undefined) {
        throw made.crash;
      }

      return <Header {...props} />;
    },
  };
});

const mePath = '/api/auth/me';

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

// Between a day and its month, so that the two never split across lines (microcopy.md §3).
const nbsp = ' ';

// Made-up IDs: the one that the made-up participant screen cannot render, and another.
const brokenId = '4e7a2c1b-8d35-4f60-9b1e-2a6c0d5f8e73';
const otherId = '7c1f9e24-3b6a-4d08-a5e2-91d4b0c63f58';

// Whether the made-up Flagged screen still throws. Not a count of its renders: React renders a screen that throws once
// more before the boundary takes over, and a screen that threw on its first render alone would never show the crash.
let flaggedThrows = true;

/** A made-up screen that throws every time it renders. */
function Broken(): never {
  throw made.crash;
}

/** A made-up Flagged screen, which throws while rendering until the test mends it. */
function Flagged() {
  if (flaggedThrows) {
    throw made.crash;
  }

  return <PageHeading>Flagged notes</PageHeading>;
}

/** A made-up screen of one participant, which throws while rendering for one made-up ID. */
function Participant() {
  const { id } = useParams();
  if (id === brokenId) {
    throw made.crash;
  }

  return <PageHeading>{`Participant ${id}`}</PageHeading>;
}

// Made-up screens beside the app's: Flagged at the address of the manager's Flagged link, in place of the app's, and a
// route with an ID.
const madeUp: AppRoute[] = [
  { path: 'made-up', element: <Broken />, handle: { pageName: pageName.participants } },
  { path: 'flagged', element: <Flagged />, handle: { pageName: pageName.flaggedNotes } },
  { path: 'made-up/:id', element: <Participant />, handle: { pageName: pageName.participant } },
];

/**
 * `table` with the made-up screens beside its `*` route, inside the screens' error boundary, and without its own
 * Flagged.
 */
function withMadeUp(table: AppRoute[]): AppRoute[] {
  return table.flatMap((route) => {
    if (route.path === '*') {
      return [...madeUp, route];
    }

    if (route.path === 'flagged') {
      return [];
    }

    return route.index || route.children === undefined ? [route] : [{ ...route, children: withMadeUp(route.children) }];
  });
}

/** How the server meets a `GET /api/auth/me`: an answer, or a failure as `fetch` has one. */
type Answer = () => Promise<Response>;

/** A `200` with `me`. */
function answerWith(me: Me): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(me), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem with no `code`, as the API answers a failure (design.md §6.1). */
function problem(status: number): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

const fetchMock = vi.fn<typeof fetch>();

// How the server meets each request from now on.
let server: Answer;

/**
 * Renders the app at `path` on a memory router built from the route table with the made-up screens, under a query
 * client of its own and StrictMode, as `main.tsx` renders it.
 */
function renderApp(path: string) {
  const router = createMemoryRouter(withMadeUp(routes), { initialEntries: [path] });
  const { container } = render(
    <StrictMode>
      <QueryClientProvider client={createQueryClient()}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return { router, container };
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Lets TanStack hand the query's new state to the page, on a `setTimeout` of 0 that the fake clock runs 1 ms on. */
async function settle() {
  await wait(1);
}

/** `<main>`. */
function main(): HTMLElement {
  return screen.getByRole('main');
}

/**
 * What `<main>` shows: the screen's elements, in the `<div>` that the shell holds the route in, which the
 * signed-out-in-place view hides (app-shell.md component 7), or the shell's own crash page's, which has none.
 */
function mainContent(): Element[] {
  const children = Array.from(main().children);
  return children.length === 1 && children[0].localName === 'div' ? Array.from(children[0].children) : children;
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The main navigation. */
function nav(): HTMLElement {
  return screen.getByRole('navigation', { name: 'Main' });
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/**
 * Expects the crash page's message alone in `<main>`, in order: the page's one `<h1>`, its sentence, Go to Today as a
 * link to `/`, and the page's one status region, empty; titled "There is a problem".
 */
function expectCrashPage() {
  const heading = pageHeading();
  expect(heading).toHaveAccessibleName('There is a problem with Grow2Notes');
  expect(mainContent().map((element) => element.textContent)).toEqual([
    'There is a problem with Grow2Notes',
    'Anything already saved is kept.',
    'Go to Today',
    '',
  ]);
  expect(within(main()).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
  expect(screen.getAllByRole('heading', { level: 1 })).toEqual([heading]);
  const regions = screen.getAllByRole('status');
  expect(regions).toHaveLength(1);
  expect(main()).toContainElement(regions[0]);
  expect(document.title).toBe('Grow2Notes – There is a problem');
}

/** Expects the signed-out frame's header: the wordmark as plain text, and no nav or Account. */
function expectNoNav() {
  expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Account' })).not.toBeInTheDocument();
  expect(screen.queryByRole('link', { name: 'Grow2Notes' })).not.toBeInTheDocument();
  expect(within(screen.getByRole('banner')).getByText('Grow2Notes')).toBeInTheDocument();
}

/** Shows the crash page under the nav, in place of a made-up screen that throws, for a worker. */
async function crashScreen() {
  server = answerWith(worker);
  const app = renderApp('/made-up');
  await settle();
  return app;
}

/** Shows the shell's crash page, in place of a shell whose header throws once /me has answered, for a worker. */
async function crashShell() {
  made.headerThrows = true;
  server = answerWith(worker);
  const app = renderApp('/');
  await settle();
  return app;
}

beforeEach(() => {
  vi.useFakeTimers();
  fetchMock.mockImplementation((path) => {
    if (path !== mePath) {
      throw new Error('The test gave no answer for this request.');
    }

    return server();
  });
  vi.stubGlobal('fetch', fetchMock);
  // React and React Router report each crash that a boundary catches: the made-up one is expected, and any other shows.
  const report = console.error;
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
    if (!args.includes(made.crash)) {
      report(...args);
    }
  });
  made.headerThrows = false;
  flaggedThrows = true;
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('the crash page under the nav', () => {
  it('shows in <main>, under the nav, in place of a screen that throws while rendering, moving no focus', async () => {
    const { router } = await crashScreen();

    expectCrashPage();
    expect(screen.getByRole('banner')).toContainElement(nav());
    expect(screen.getByRole('button', { name: 'Account' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/made-up');
    expect(document.body).toHaveFocus();
    expect(document.querySelectorAll('main')).toHaveLength(1);
  });

  it('keeps the nav and Account there and working', async () => {
    server = answerWith(manager);
    renderApp('/made-up');
    await settle();
    expectCrashPage();

    fireEvent.click(screen.getByRole('button', { name: 'Account' }));

    expect(screen.getByText('Signed in as Sam Lee')).toBeVisible();
    expect(within(nav()).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'Today',
      'Flagged',
      'Report',
      'Manage',
    ]);
  });

  it("goes from a nav link to that link's screen, titled with its page name", async () => {
    server = answerWith(manager);
    const { router } = renderApp('/made-up');
    await settle();

    fireEvent.click(within(nav()).getByRole('link', { name: 'Manage' }));
    await settle();

    expect(router.state.location.pathname).toBe('/manage');
    expect(pageHeading()).toHaveAccessibleName('Manage');
    expect(screen.queryByText('Anything already saved is kept.')).not.toBeInTheDocument();
    expect(document.title).toBe('Grow2Notes – Manage');
  });

  it('leaves the mark as the address gives it, and shows the screen again from a link to its own address', async () => {
    server = answerWith(manager);
    const { router } = renderApp('/flagged');
    await settle();
    expectCrashPage();
    expect(currentItems()).toEqual(['Flagged page']);

    flaggedThrows = false;
    fireEvent.click(within(nav()).getByRole('link', { name: 'Flagged' }));
    await settle();

    expect(router.state.location.pathname).toBe('/flagged');
    expect(pageHeading()).toHaveAccessibleName('Flagged notes');
    expect(screen.queryByText('Anything already saved is kept.')).not.toBeInTheDocument();
    // The route's page name is the one it had under the crash page, which put the route's title back as it went.
    expect(document.title).toBe('Grow2Notes – Flagged notes');
  });

  it('shows the screen again, titled with its page name, on going to another ID of its route', async () => {
    server = answerWith(manager);
    const { router } = renderApp(`/made-up/${brokenId}`);
    await settle();
    expectCrashPage();

    // As a link to it does.
    await act(() => router.navigate(`/made-up/${otherId}`));
    await settle();

    expect(pageHeading()).toHaveAccessibleName(`Participant ${otherId}`);
    expect(document.title).toBe('Grow2Notes – Participant');
  });

  it('keeps its title from one screen that throws to another', async () => {
    server = answerWith(manager);
    const { router } = renderApp('/made-up');
    await settle();

    await act(() => router.navigate(`/made-up/${brokenId}`));
    await settle();

    expectCrashPage();
  });
});

describe('beside the crash page', () => {
  it('shows Page not found in <main> at an unknown address, with the nav and its Today link working', async () => {
    server = answerWith(worker);
    const { router } = renderApp('/no-such-page');
    await settle();
    expect(pageHeading()).toHaveAccessibleName('Page not found');
    expect(document.title).toBe('Grow2Notes – Page not found');

    fireEvent.click(within(nav()).getByRole('link', { name: 'Today' }));
    await settle();

    expect(router.state.location.pathname).toBe('/');
    expect(pageHeading()).toHaveAccessibleName(`Today, Thursday 1${nbsp}October`);
    expect(document.title).toBe('Grow2Notes – Today');
  });
});

describe.each([
  { page: 'the crash page under the nav', crash: crashScreen },
  { page: "the shell's crash page", crash: crashShell },
])('$page', ({ crash }) => {
  it('has Go to Today as a plain link to /, which the router leaves to the browser, for a full page load', async () => {
    const { router } = await crash();
    const { location } = router.state;
    const link = within(main()).getByRole('link', { name: 'Go to Today' });
    // Last of every listener, as the browser: it follows the link unless the page stopped it, which jsdom cannot.
    let followed = false;
    const browser = (event: MouseEvent) => {
      followed = !event.defaultPrevented;
      event.preventDefault();
    };
    window.addEventListener('click', browser);

    fireEvent.click(link);
    window.removeEventListener('click', browser);
    await settle();

    expect(followed).toBe(true);
    expect(router.state.location).toBe(location);
    expectCrashPage();
  });
});

describe("the shell's crash page", () => {
  it('shows the message in place of a shell that throws, with the skip link and the wordmark as text', async () => {
    const { container } = await crashShell();

    expectCrashPage();
    expectNoNav();
    expect(document.querySelectorAll('main')).toHaveLength(1);
    const frame = Array.from(container.children, (element) => element.localName + (element.id && `#${element.id}`));
    expect(frame).toEqual(['a', 'header', 'div#page-bar', 'main#main-content']);
    expect(container.firstElementChild).toBe(screen.getByRole('link', { name: 'Skip to main content' }));
    expect(document.body).toHaveFocus();
  });
});

describe("the shell's messages", () => {
  /** The start-up's message for a `/me` that `answer` fails, and, `again`, its "still" form after Try again. */
  async function startUpFails(answer: Answer, again: boolean) {
    server = answer;
    renderApp('/');
    await wait(2_000);
    if (again) {
      fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
      await wait(2_000);
    }
  }

  /** Page not found, at an address that no route matches, for a worker. */
  async function pageNotFound() {
    server = answerWith(worker);
    renderApp('/no-such-page');
    await settle();
  }

  it.each([
    {
      message: 'Could not connect',
      show: () => startUpFails(noConnection, false),
      says: 'Grow2Notes did not load: no connection. Try again.',
    },
    {
      message: 'Could not connect, still',
      show: () => startUpFails(noConnection, true),
      says: 'Grow2Notes still did not load: no connection. Try again.',
    },
    {
      message: 'There is a problem, at start-up',
      show: () => startUpFails(problem(503), false),
      says: 'Grow2Notes did not load: something went wrong. Try again.',
    },
    {
      message: 'There is a problem, at start-up, still',
      show: () => startUpFails(problem(503), true),
      says: 'Grow2Notes still did not load: something went wrong. Try again.',
    },
    { message: 'Page not found', show: pageNotFound, says: 'check it is correct.' },
    { message: 'the crash page under the nav', show: crashScreen, says: 'Anything already saved is kept.' },
    { message: "the shell's crash page", show: crashShell, says: 'Anything already saved is kept.' },
  ])(
    '$message holds no "oops", "sorry", "error", "invalid", "can\'t", "couldn\'t" or status code',
    async ({ show, says }) => {
      await show();
      const text = `${document.title}\n${main().textContent}`;
      expect(text).toContain(says);

      expect(text).not.toMatch(/oops|sorry|error|invalid|can['’]t|couldn['’]t/i);
      expect(text).not.toMatch(/\b[1-5]\d\d\b/);
      // "something went wrong" only inside the start-up's sentence, which says what failed and what to do.
      expect(text.replaceAll('did not load: something went wrong. Try again.', '')).not.toMatch(/went wrong/i);
    },
  );
});
