import type { QueryClient } from '@tanstack/react-query';
import { QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode } from 'react';
import { createMemoryRouter, parsePath } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, onTestFinished, vi } from 'vitest';
import { ApiError } from '../../api/api.ts';
import { meQuery, type Me } from '../../api/me.ts';
import { createQueryClient } from '../../api/queryClient.ts';
import type { ListRow } from '../../components/ListEditor.tsx';
import { routes } from '../../routes/routes.tsx';
import { commonItemGroupsQuery, type AdminCommonItemGroup } from './commonItemsQuery.ts';

type Router = ReturnType<typeof createMemoryRouter>;

const mePath = '/api/auth/me';
const antiforgeryPath = '/api/auth/antiforgery';
const listPath = '/api/admin/common-item-groups?includeArchived=true';

// common-items.md's made-up people (A35): Sam Lee, the manager signed in, in the JSON that MeEndpointTests asserts,
// and a worker.
const manager: Me = {
  userId: '9d3a61f0-7c2b-4e58-b1a4-0f6e2d8c5b97',
  displayName: 'Sam Lee',
  role: 'Manager',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};
const worker: Me = {
  userId: '5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13',
  displayName: 'Alex Pham',
  role: 'Worker',
  organisationName: 'Riverbend Support',
  today: '2026-10-01',
};

const help =
  'Items in Every note show on every note. For other groups, the writer ticks the ones that happened. Changes apply '
  + 'to notes started from now on.';
const everyNoteLine = 'Always shown first, on every note.';
const emptyLine = 'No items in this group. It does not show on notes until it has one.';
const listChanged = 'Someone else changed this list just now. Check it, then try again.';

/** A made-up common item (A35), active unless it has the time it was archived at. */
function item(id: string, text: string, sortOrder: number, archivedAtUtc: string | null = null): ListRow {
  return { id, text, sortOrder, archivedAtUtc, rowVersion: `AAAAAAAAC${id.slice(0, 3)}=` };
}

/** A made-up group (A35), active unless it has the time it was archived at. */
function group(
  id: string,
  name: string,
  sortOrder: number,
  items: ListRow[],
  archivedAtUtc: string | null = null,
): AdminCommonItemGroup {
  return { id, name, isEveryNote: false, sortOrder, archivedAtUtc, rowVersion: `AAAAAAAAD${id.slice(0, 3)}=`, items };
}

// common-items.md's sketch (*Layout - phone*): Every note with two items and two archived ones, archived in this order
// and so shown the other way round; Community outing with three; Personal care with none; and an archived group,
// which the page leaves out, with an item of its own.
const medication = item('1a4c7e90-2b5d-4f81-9c36-8e0a2d4f6b17', 'Medication prompted', 0);
const meal = item('2b5d8fa1-3c6e-4a92-8d47-9f1b3e5a7c28', 'Meal prepared', 1);
const laundry = item('3c6e9ab2-4d7f-4ba3-9e58-0a2c4f6b8d39', 'Laundry done', 2, '2026-09-21T05:12:00Z');
const bed = item('4d7fabc3-5e80-4cb4-8f69-1b3d5a7c9e4a', 'Bed made', 3, '2026-09-28T05:12:00Z');
const busOrTrain = item('5e80bcd4-6f91-4dc5-9a7a-2c4e6b8dafb5', 'Travelled by bus or train', 0);
const paid = item('6f91cde5-7aa2-4ed6-8b8b-3d5f7c9eb0c6', 'Paid for a ticket at the counter', 1);
const lunch = item('7aa2def6-8bb3-4fe7-9c9c-4e6a8daf1d07', 'Bought lunch', 2);
const packed = item('8bb3ef07-9cc4-4af8-8dad-5f7b9eb02e18', 'Packed an overnight bag', 0);

const everyNote: AdminCommonItemGroup = {
  ...group('0e2a4c6f-1b3d-4e5f-8a7b-9c0d1e2f3a4b', 'Every note', 0, [medication, meal, laundry, bed]),
  isEveryNote: true,
};
const outing = group('1f3b5d7a-2c4e-4f60-9b8c-0d1e2f3a4b5c', 'Community outing', 0, [busOrTrain, paid, lunch]);
const care = group('2a4c6e8b-3d5f-4a71-8c9d-1e2f3a4b5c6d', 'Personal care', 1, []);
const respite = group('3b5d7f9c-4e6a-4b82-9dae-2f3a4b5c6d7e', 'Overnight respite', 2, [packed], '2026-09-14T05:12:00Z');

/** In the API's order: Every note first, then the others in order, the archived one among them by its SortOrder. */
const groups = [everyNote, outing, care, respite];

/** How the server meets a request: an answer, or a failure as `fetch` has one. */
type Answer = (init?: RequestInit) => Promise<Response>;

/** A `200` with a JSON body. */
function json(body: unknown): Answer {
  return () =>
    Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1), with its §6.9 code, if it has one. */
function problem(status: number, code?: string): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status, code });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** A `201` with no body, as an add answers (design.md §6.6). */
const created: Answer = () => Promise.resolve(new Response(null, { status: 201 }));

/** No connection, which `fetch` rejects at once. */
const noConnection: Answer = () => Promise.reject(new TypeError('Failed to fetch'));

/** A server that never answers: `fetch` rejects only when its signal aborts, with the signal's reason. */
const noAnswer: Answer = (init) =>
  new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each request, by its method and path: a new answer for each. */
const answers = new Map<string, Answer>();

/** The query client of the app that the test rendered last, whose cache a test can look into. */
let queryClient: QueryClient;

/** Stubs `fetch` as the server, which answers each `method` request for `path` with `answer` from now on. */
function serve(method: 'GET' | 'POST' | 'PUT', path: string, answer: Answer) {
  answers.set(`${method} ${path}`, answer);
}

/** How many requests have been sent for the groups list. */
function listReads(): number {
  return fetchMock.mock.calls.filter(([path]) => path === listPath).length;
}

/**
 * Renders the app at `path` for `me` on a memory router, under a query client of its own and StrictMode, as `main.tsx`
 * renders it. `['me']` holds `me` already, as after the start-up, so that the screen shows with the first render, from
 * which the fake clock then times its region.
 */
function renderApp(path: string, me: Me = manager): Router {
  serve('GET', mePath, json(me));
  queryClient = createQueryClient();
  queryClient.setQueryData(meQuery.queryKey, me);
  // With a key of its own, as a browser gives it, where a memory router gives every test's first entry the key
  // "default", under which the shell's memory would carry a position from one test into the next.
  const router = createMemoryRouter(routes, { initialEntries: [{ ...parsePath(path), key: crypto.randomUUID() }] });
  render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
  return router;
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Lets TanStack hand the query's new state to the page, on a `setTimeout` of 0 that the fake clock runs 1 ms on. */
async function settle() {
  await wait(1);
}

/** Opens Common items with `list` served, and lets it load. */
async function openPage(list: readonly AdminCommonItemGroup[] = groups): Promise<Router> {
  serve('GET', listPath, json(list));
  const router = renderApp('/manage/common-items');
  await settle();
  return router;
}

function main(): HTMLElement {
  return screen.getByRole('main');
}

/** The page's `<h1>`, which must be in `<main>`. */
function pageHeading(): HTMLElement {
  return within(main()).getByRole('heading', { level: 1 });
}

/** The help text, by its id, which every item field names in its description. */
function helpText(): HTMLElement | null {
  return document.getElementById('items-help');
}

/** Each group's section, a region named by its heading, in page order. */
function sections(): HTMLElement[] {
  return within(main()).queryAllByRole('region');
}

/** The section of the group named `name`. */
function sectionOf(name: string): HTMLElement {
  return within(main()).getByRole('region', { name });
}

/** The load region's line: the status region in `<main>` that shows, beside the page's hidden one. */
function line(): HTMLElement {
  const shown = within(main())
    .getAllByRole('status')
    .filter((region) => !region.classList.contains('visually-hidden'));
  expect(shown).toHaveLength(1);
  return shown[0];
}

/** The page's own status region, visually hidden, which says what a change did. */
function pageStatus(): HTMLElement {
  const hidden = within(main())
    .getAllByRole('status')
    .filter((region) => region.classList.contains('visually-hidden'));
  expect(hidden).toHaveLength(1);
  return hidden[0];
}

/** A section's active items, as the list items of its one numbered list, or none while it has no list. */
function activeItems(section: HTMLElement): HTMLLIElement[] {
  const lists = section.querySelectorAll('ol');
  expect(lists.length).toBeLessThanOrEqual(1);
  return Array.from(lists[0]?.children ?? [], (element) => element as HTMLLIElement);
}

/** A section's archived section, or `null` while it is left out. */
function archivedSection(section: HTMLElement): HTMLDetailsElement | null {
  return section.querySelector('details');
}

/** A section's archived items, in the order shown. */
function archivedItems(section: HTMLElement): HTMLLIElement[] {
  return Array.from(archivedSection(section)?.querySelectorAll<HTMLLIElement>('ul > li') ?? []);
}

/** A row's text, the first paragraph of its list item. */
function textOf(row: HTMLElement): string | null | undefined {
  return row.querySelector('p')?.textContent;
}

/** Expects a row's buttons to be exactly those of `names`, in that DOM order, each by its accessible name. */
function expectButtons(row: HTMLElement, names: string[]) {
  expect(within(row).getAllByRole('button')).toEqual(names.map((name) => within(row).getByRole('button', { name })));
}

/** Each element of the header that has `aria-current`, as its text and the attribute's value. */
function currentItems(): string[] {
  const current = screen.getByRole('banner').querySelectorAll('[aria-current]');
  return Array.from(current, (element) => `${element.textContent} ${element.getAttribute('aria-current')}`);
}

/** Focuses Try again and presses it, as a keyboard user would, and returns it. */
function pressTryAgain(): HTMLElement {
  const button = within(main()).getByRole('button', { name: 'Try again' });
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Expects Page not found in `<main>`, titled "Page not found", with nothing of the screen left. */
function expectPageNotFound() {
  expect(pageHeading()).toHaveAccessibleName('Page not found');
  expect(within(main()).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
  expect(helpText()).toBeNull();
  expect(sections()).toEqual([]);
  expect(document.title).toBe('Grow2Notes – Page not found');
  expect(currentItems()).toEqual([]);
}

/** Lets React and React Router report the refusal of `status` that the screens' boundary catches, and only that. */
function expectRefusalReported(status: number) {
  const report = console.error;
  const reported = vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
    if (!args.some((arg) => arg instanceof ApiError && arg.status === status)) {
      report(...args);
    }
  });
  onTestFinished(() => {
    reported.mockRestore();
  });
}

/** Whether `after` comes after `before` in the document. */
function follows(before: Node, after: Node): boolean {
  return (before.compareDocumentPosition(after) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
}

beforeEach(() => {
  vi.useFakeTimers();
  serve('GET', antiforgeryPath, () =>
    Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } })),
  );
  fetchMock.mockImplementation((path, init) => {
    const answer = typeof path === 'string' ? answers.get(`${init?.method ?? 'GET'} ${path}`) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
  // Emptied, so that a title a test finds is one the app set.
  document.title = '';
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('Common items', () => {
  it("shows its <h1>, the help text, then each active group's section in the list's order", async () => {
    await openPage();

    expect(document.title).toBe('Grow2Notes – Common items');
    expect(currentItems()).toEqual(['Manage true']);
    expect(pageHeading()).toHaveAccessibleName('Common items');
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(helpText()?.tagName).toBe('P');
    expect(helpText()).toHaveTextContent(help);
    expect(sections().map((section) => section.tagName)).toEqual(['SECTION', 'SECTION', 'SECTION']);
    expect(within(main()).getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual([
      'Every note',
      'Community outing',
      'Personal care',
    ]);
    expect(follows(pageHeading(), helpText()!)).toBe(true);
    expect(follows(helpText()!, sections()[0])).toBe(true);
  });

  it('names each section by its <h2>, the group\'s name as stored, wrapping anywhere', async () => {
    await openPage();

    for (const { id, name } of [everyNote, outing, care]) {
      const section = sectionOf(name);
      const heading = within(section).getByRole('heading', { level: 2 });
      expect(heading.id).toBe(`group-${id}-heading`);
      expect(section).toHaveAttribute('aria-labelledby', heading.id);
      expect(section.firstElementChild).toBe(heading);
      expect(heading).toHaveStyle({ overflowWrap: 'anywhere' });
    }
  });

  it('leaves out an archived group, with its items', async () => {
    await openPage();

    expect(within(main()).queryByText(respite.name)).not.toBeInTheDocument();
    expect(within(main()).queryByText(packed.text)).not.toBeInTheDocument();
  });

  it('gives Every note its line under its <h2> and no group buttons, not even unavailable ones', async () => {
    await openPage();
    const section = sectionOf('Every note');

    const everyNoteParagraph = within(section).getByText(everyNoteLine);
    expect(everyNoteParagraph.tagName).toBe('P');
    expect(within(section).getByRole('heading', { level: 2 }).nextElementSibling).toBe(everyNoteParagraph);
    expect(within(main()).queryByRole('button', { name: /^Rename/ })).not.toBeInTheDocument();
    expect(within(main()).queryAllByRole('button', { name: /, Every note group$/ })).toEqual([]);
    // Each button in the section is an item row's, its add form's or an archived item's.
    const rowButtons = [...activeItems(section), ...archivedItems(section)].flatMap((row) =>
      within(row).getAllByRole('button'),
    );
    expect(within(section).getAllByRole('button')).toEqual([
      ...rowButtons.slice(0, 8),
      within(section).getByRole('button', { name: 'Add item' }),
      ...rowButtons.slice(8),
    ]);
    // Only Every note's.
    expect(within(main()).getAllByText(everyNoteLine)).toHaveLength(1);
  });

  it("lists each group's active items in a numbered list of its own, from 1, each row's text in full", async () => {
    await openPage();

    for (const { name, items } of [everyNote, outing]) {
      const section = sectionOf(name);
      const rows = activeItems(section);
      expect(rows.map((row) => row.tagName)).toEqual(items.filter((row) => row.archivedAtUtc === null).map(() => 'LI'));
      expect(rows.map(textOf)).toEqual(items.filter((row) => row.archivedAtUtc === null).map((row) => row.text));
      // Native numbering, from 1 in each list.
      expect(section.querySelector('ol')).not.toHaveAttribute('start');
    }
  });

  it('gives each active row Move up, Move down, Edit and Archive, its text hidden after each', async () => {
    await openPage();
    const rows = activeItems(sectionOf('Community outing'));

    for (const row of rows) {
      const text = textOf(row)!;
      expectButtons(row, [`Move up, ${text}`, `Move down, ${text}`, `Edit, ${text}`, `Archive, ${text}`]);
    }

    expect(within(rows[0]).getByRole('button', { name: /^Move up/ })).toHaveAttribute('aria-disabled', 'true');
    expect(within(rows[0]).getByRole('button', { name: /^Move down/ })).not.toHaveAttribute('aria-disabled');
    expect(within(rows[2]).getByRole('button', { name: /^Move up/ })).not.toHaveAttribute('aria-disabled');
    expect(within(rows[2]).getByRole('button', { name: /^Move down/ })).toHaveAttribute('aria-disabled', 'true');
  });

  it("shows a group's archived items newest first, each its text and Restore alone, in a closed section", async () => {
    await openPage();
    const section = sectionOf('Every note');

    const details = archivedSection(section)!;
    expect(details.open).toBe(false);
    expect(details).not.toHaveAttribute('open');
    expect(follows(within(section).getByRole('button', { name: 'Add item' }), details)).toBe(true);
    const rows = archivedItems(section);
    expect(rows.map(textOf)).toEqual([bed.text, laundry.text]);
    for (const row of rows) {
      expectButtons(row, [`Restore, ${textOf(row)}`]);
    }

    expect(archivedSection(sectionOf('Community outing'))).toBeNull();
    expect(archivedSection(sectionOf('Personal care'))).toBeNull();
  });

  it('summarises the archived section as "Archived items", then, for screen readers, " in [group name]"', async () => {
    await openPage();

    const summary = archivedSection(sectionOf('Every note'))!.querySelector('summary')!;
    expect(summary.textContent).toBe('Archived items in Every note');
    const hidden = summary.querySelector('.visually-hidden')!;
    expect(hidden.textContent).toBe(' in Every note');
    expect(summary.firstChild?.textContent).toBe('Archived items');
  });

  // Uncontrolled, so that React never closes what the manager opened.
  it('leaves an archived section as the manager left it when the list is read again', async () => {
    await openPage();
    archivedSection(sectionOf('Every note'))!.open = true;

    await act(() => queryClient.refetchQueries({ queryKey: commonItemGroupsQuery.queryKey }));
    await settle();

    expect(archivedSection(sectionOf('Every note'))!.open).toBe(true);
  });

  it('shows the empty line for a group with no active items, and never "No common items yet."', async () => {
    await openPage([everyNote, outing, { ...care, items: [{ ...packed, archivedAtUtc: '2026-09-30T00:00:00Z' }] }]);
    const section = sectionOf('Personal care');

    expect(within(section).getByText(emptyLine).tagName).toBe('P');
    expect(activeItems(section)).toEqual([]);
    expect(archivedItems(section).map(textOf)).toEqual([packed.text]);
    expect(within(main()).getAllByText(emptyLine)).toHaveLength(1);
    expect(main().textContent).not.toContain('No common items yet.');
  });

  it('shows Every note alone, with its line, the empty line and its add form, before other groups', async () => {
    await openPage([{ ...everyNote, items: [] }]);

    expect(sections()).toEqual([sectionOf('Every note')]);
    const section = sectionOf('Every note');
    expect(within(section).getByText(everyNoteLine)).toBeInTheDocument();
    expect(within(section).getByText(emptyLine)).toBeInTheDocument();
    expect(within(section).getByRole('textbox', { name: 'New item in Every note' })).toBeInTheDocument();
    expect(within(section).getByRole('button', { name: 'Add item' })).toBeInTheDocument();
  });

  it("gives each group's add form the group's name in its label", async () => {
    await openPage();

    for (const { name } of [everyNote, outing, care]) {
      expect(within(sectionOf(name)).getByRole('textbox', { name: `New item in ${name}` })).toBeInTheDocument();
    }
  });

  it('keeps the page status region in the page, empty, beside the load region\'s line', async () => {
    await openPage();

    expect(pageStatus()).toBeEmptyDOMElement();
    expect(line()).toBeEmptyDOMElement();
  });

  it('keeps every item and group out of the address, the title, the router state, the keys and storage', async () => {
    const router = await openPage();

    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe('/manage/common-items');
    expect(document.title).toBe('Grow2Notes – Common items');
    expect(JSON.stringify(router.state.location.state ?? null)).toBe('null');
    const keys = JSON.stringify(queryClient.getQueryCache().getAll().map(({ queryKey }) => queryKey));
    expect(keys).toBe(JSON.stringify([['me'], ['admin', 'common-item-groups']]));
    for (const words of [medication.text, outing.name, bed.text]) {
      expect(keys).not.toContain(words);
    }

    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });

  // The frame is the page's: a write in any group's list shows the one conflict, above every group.
  it("shows one list conflict, above the groups, focused, after a write in any group's list", async () => {
    await openPage();
    const archivedMeanwhile = { ...outing, items: [{ ...paid, archivedAtUtc: '2026-10-01T06:00:00Z' }] };
    serve('POST', `/api/admin/common-items/${paid.id}/archive`, problem(412));
    serve('GET', listPath, json([everyNote, archivedMeanwhile, care]));

    fireEvent.click(within(sectionOf('Community outing')).getByRole('button', { name: `Archive, ${paid.text}` }));
    await settle();
    await settle();

    const conflicts = within(main()).getAllByText(listChanged);
    expect(conflicts).toHaveLength(1);
    expect(conflicts[0].tagName).toBe('P');
    expect(conflicts[0]).toHaveAttribute('tabindex', '-1');
    expect(conflicts[0]).toHaveFocus();
    expect(follows(helpText()!, conflicts[0])).toBe(true);
    expect(follows(conflicts[0], sections()[0])).toBe(true);
  });
});

describe('Common items while the list loads', () => {
  it('shows its <h1> and the help text at once, then nothing more until "Loading common items…" at 1 s', async () => {
    serve('GET', listPath, noAnswer);
    renderApp('/manage/common-items');

    expect(pageHeading()).toHaveAccessibleName('Common items');
    expect(helpText()).toHaveTextContent(help);

    await wait(999);
    expect(line()).toBeEmptyDOMElement();
    // No section, no empty line, no add form.
    expect(main().textContent).toBe(`Common items${help}`);
    expect(sections()).toEqual([]);
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
    expect(document.querySelector('form')).toBeNull();

    await wait(1);
    expect(line()).toHaveTextContent('Loading common items…');
    expect(main().textContent).toBe(`Common items${help}Loading common items…`);
    expect(within(main()).queryByRole('textbox')).not.toBeInTheDocument();
    expect(document.querySelector('form')).toBeNull();
  });

  it('says the items did not load, with Try again, once its silent retry has found no connection too', async () => {
    serve('GET', listPath, noConnection);
    renderApp('/manage/common-items');

    await wait(999);
    expect(within(main()).queryByRole('button')).not.toBeInTheDocument();
    await wait(1);
    await settle();

    expect(line().textContent).toBe('Common items did not load: no connection. Try again.');
    expect(within(main()).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(pageHeading()).toHaveAccessibleName('Common items');
    expect(helpText()).toHaveTextContent(help);
    expect(sections()).toEqual([]);
    expect(document.querySelector('form')).toBeNull();
  });

  it('says the items still did not load after a Try again that fails, with focus on Try again', async () => {
    serve('GET', listPath, noConnection);
    renderApp('/manage/common-items');
    await wait(1000);
    await settle();

    const button = pressTryAgain();
    // Its one silent retry, as on the first load.
    await wait(1000);
    await settle();

    expect(line().textContent).toBe('Common items still did not load: no connection. Try again.');
    expect(button).toHaveFocus();
  });

  it('says something went wrong for a 503, after its silent retry', async () => {
    serve('GET', listPath, problem(503));
    renderApp('/manage/common-items');

    await wait(1000);
    await settle();

    expect(line().textContent).toBe('Common items did not load: something went wrong. Try again.');
  });

  it('shows the groups after a Try again that loads, with focus on the <h1>', async () => {
    serve('GET', listPath, noConnection);
    renderApp('/manage/common-items');
    await wait(1000);
    await settle();
    serve('GET', listPath, json(groups));

    pressTryAgain();
    await settle();

    expect(sections().map((section) => section.getAttribute('aria-labelledby'))).toEqual([
      `group-${everyNote.id}-heading`,
      `group-${outing.id}-heading`,
      `group-${care.id}-heading`,
    ]);
    expect(line()).toBeEmptyDOMElement();
    expect(within(main()).queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
    expect(pageHeading()).toHaveFocus();
  });

  it('shows the empty line only once the list has come', async () => {
    serve('GET', listPath, noAnswer);
    renderApp('/manage/common-items');
    await wait(5000);

    expect(within(main()).queryByText(emptyLine)).not.toBeInTheDocument();
  });

  it('leaves the page as it was, saying nothing, when a read in the background fails', async () => {
    await openPage();
    const before = main().innerHTML;
    serve('GET', listPath, noConnection);

    act(() => {
      void queryClient.refetchQueries({ queryKey: commonItemGroupsQuery.queryKey });
    });
    await wait(60_000);

    expect(queryClient.getQueryState(commonItemGroupsQuery.queryKey)?.status).toBe('error');
    expect(main().innerHTML).toBe(before);
    expect(line()).toBeEmptyDOMElement();
    expect(pageStatus()).toBeEmptyDOMElement();
  });
});

describe('Common items that cannot be shown', () => {
  it.each([403, 404])('shows Page not found in place of the screen for a %i on the list, at the same address', async (
    status,
  ) => {
    expectRefusalReported(status);
    serve('GET', listPath, problem(status));
    const router = renderApp('/manage/common-items');

    await settle();

    expectPageNotFound();
    expect(router.state.location.pathname).toBe('/manage/common-items');
  });

  it('shows a worker Page not found, and sends no request for the list', async () => {
    serve('GET', listPath, json(groups));
    renderApp('/manage/common-items', worker);

    await wait(60_000);

    expectPageNotFound();
    expect(listReads()).toBe(0);
  });
});

const hint = 'You can enter up to 200 characters';
const enterTheItem = 'Enter the common item';
const tooLong = 'Common item must be 200 characters or less';
const added = 'Item added to the end of the list';

/** The path that an add to the group of `groupId` posts to (design.md §6.6). */
function itemsPath(groupId: string): string {
  return `/api/admin/common-item-groups/${groupId}/items`;
}

/** The bodies of the adds sent to the group of `groupId`, in the order sent. */
function addsTo(groupId: string): unknown[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    path === itemsPath(groupId) && init?.method === 'POST' ? [JSON.parse(init.body as string) as unknown] : [],
  );
}

/** The path that the item of `itemId` is reworded at (design.md §6.6). */
function itemPath(itemId: string): string {
  return `/api/admin/common-items/${itemId}`;
}

/** A `200` with no body and no `ETag`, as a reword answers (design.md §6.6). */
const reworded: Answer = () => Promise.resolve(new Response(null, { status: 200 }));

/** The path that the order of the items of the group of `groupId` is put to (design.md §6.6). */
function orderPath(groupId: string): string {
  return `${itemsPath(groupId)}/order`;
}

/** A `204` with no body, as a reorder answers (design.md §6.6). */
const reordered: Answer = () => Promise.resolve(new Response(null, { status: 204 }));

/**
 * Has the groups list, each group's adds, reorders and each item's rewords answered by a server that holds `list`, and
 * gives what it holds, which a test may change as another manager would: an add puts an item, with an ID and a version
 * of its own, at the end of its group, one after the highest active `sortOrder` (A2), and answers `201` with no body; a
 * reword refuses an `If-Match` that is not the item's version (`412`), then an item of an archived group (`409
 * config.group_archived`), or else gives the item its text and a new version, and answers `200` with no body; a
 * reorder refuses IDs that are not the group's active items, each once (`422 config.order_mismatch`), or else gives
 * them `sortOrder` 0 to n-1, with a new version for each item whose `sortOrder` changed, and answers `204` with no
 * body; and a read gives what it holds.
 */
function serveGroups(list: readonly AdminCommonItemGroup[]): AdminCommonItemGroup[] {
  const held = list.map((listed) => ({ ...listed, items: [...listed.items] }));
  let versions = 0;
  const nextVersion = () => {
    versions += 1;
    return `AAAAAAAAE${String(versions).padStart(3, '0')}=`;
  };
  const serveReword = (heldGroup: AdminCommonItemGroup, itemId: string) => {
    serve('PUT', itemPath(itemId), (init) => {
      const at = heldGroup.items.findIndex((listed) => listed.id === itemId);
      if (new Headers(init?.headers).get('If-Match') !== `"${heldGroup.items[at].rowVersion}"`) {
        return problem(412)();
      }

      if (heldGroup.archivedAtUtc !== null) {
        return problem(409, 'config.group_archived')();
      }

      const { text } = JSON.parse(init?.body as string) as { text: string };
      heldGroup.items[at] = { ...heldGroup.items[at], text, rowVersion: nextVersion() };
      return reworded();
    });
  };
  serve('GET', listPath, json(held));
  for (const heldGroup of held) {
    serve('PUT', orderPath(heldGroup.id), (init) => {
      const { commonItemIds } = JSON.parse(init?.body as string) as { commonItemIds: string[] };
      const active = heldGroup.items.filter((listed) => listed.archivedAtUtc === null);
      const archived = heldGroup.items.filter((listed) => listed.archivedAtUtc !== null);
      if (
        new Set(commonItemIds).size !== commonItemIds.length
        || commonItemIds.length !== active.length
        || !active.every(({ id }) => commonItemIds.includes(id))
      ) {
        return problem(422, 'config.order_mismatch')();
      }

      const moved = commonItemIds.map((id, sortOrder) => {
        const listed = active.find((row) => row.id === id)!;
        return listed.sortOrder === sortOrder ? listed : { ...listed, sortOrder, rowVersion: nextVersion() };
      });
      heldGroup.items.splice(0, heldGroup.items.length, ...moved, ...archived);
      return reordered();
    });
    serve('POST', itemsPath(heldGroup.id), (init) => {
      const { text } = JSON.parse(init?.body as string) as { text: string };
      const active = heldGroup.items.flatMap((listed) => (listed.archivedAtUtc === null ? [listed.sortOrder] : []));
      const id = `9e8d7c6b-5a49-4382-a1b0-${String(heldGroup.items.length).padStart(12, '0')}`;
      heldGroup.items.push(item(id, text, Math.max(-1, ...active) + 1));
      serveReword(heldGroup, id);
      return created();
    });
    for (const { id } of heldGroup.items) {
      serveReword(heldGroup, id);
    }
  }

  return held;
}

/** The group's New item field, by its label, which names the group. */
function newItem(name: string): HTMLTextAreaElement {
  return within(sectionOf(name)).getByRole('textbox', { name: `New item in ${name}` });
}

/** The group's add form. */
function addForm(name: string): HTMLFormElement {
  return newItem(name).closest('form')!;
}

/** The group's Add item, by its name, which its busy label leaves as it is. */
function addItemButton(name: string): HTMLElement {
  return within(addForm(name)).getByRole('button', { name: 'Add item' });
}

/** The alert above the group's Add item. */
function addAlert(name: string): HTMLElement {
  return within(addForm(name)).getByRole('alert');
}

/** The count under the group's New item field. */
function shownCount(name: string): HTMLElement | null {
  return addForm(name).querySelector('p[aria-hidden="true"]');
}

/** The message on the group's New item field, without its hidden "Error: ", or `null` while it shows none. */
function fieldMessage(name: string): string | null {
  const message = addForm(name).querySelector('[id$="-error"]');
  return message === null ? null : (message.textContent ?? '').replace(/^Error: /, '');
}

/** The list conflict above the groups, or `null` while it is not shown. */
function listConflict(): HTMLElement | null {
  return within(main()).queryByText(listChanged);
}

/** Puts `text` in `field`, as typing or pasting it does. */
function type(field: HTMLElement, text: string) {
  fireEvent.change(field, { target: { value: text } });
}

/** Focuses `button` and presses it, as a person does, and lets the write it starts, and the read after it, run. */
async function press(button: HTMLElement) {
  button.focus();
  fireEvent.click(button);
  await settle();
  await settle();
}

describe('Common items, adding an item', () => {
  it("gives each group's add form its own field, labelled by its group, then the count and Add item", async () => {
    await openPage();

    for (const { id, name } of [everyNote, outing, care]) {
      const field = newItem(name);
      expect(field.id).toBe(`item-new-${id}`);
      expect(document.querySelector(`label[for="item-new-${id}"]`)?.textContent).toBe(`New item in ${name}`);
      expect(field.tagName).toBe('TEXTAREA');
      expect(field).toHaveAttribute('rows', '3');
      expect(field).toHaveValue('');
      for (const attribute of ['maxlength', 'required', 'aria-invalid']) {
        expect(field).not.toHaveAttribute(attribute);
      }

      // The hidden hint, then the page's help text, never the count.
      expect(field.getAttribute('aria-describedby')).toBe(`item-new-${id}-hint items-help`);
      expect(field).toHaveAccessibleDescription(`${hint} ${help}`);
      expect(addForm(name)).toHaveAttribute('novalidate');
      expect(shownCount(name)?.textContent).toBe(hint);
      expect(addAlert(name)).toBeEmptyDOMElement();
      const button = addItemButton(name);
      expect(button).toHaveAttribute('type', 'submit');
      expect(button.className).toMatch(/primary/);
      expect(follows(field, shownCount(name)!)).toBe(true);
      expect(follows(shownCount(name)!, addAlert(name))).toBe(true);
      expect(follows(addAlert(name), button)).toBe(true);
    }
  });

  it('on Add item with nothing typed, says "Enter the common item", focusing the field, sending nothing', async () => {
    await openPage();
    const field = newItem('Community outing');

    await press(addItemButton('Community outing'));

    expect(fieldMessage('Community outing')).toBe(enterTheItem);
    expect(field).toHaveFocus();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field.getAttribute('aria-describedby')).toBe(
      `item-new-${outing.id}-error item-new-${outing.id}-hint items-help`,
    );
    expect(field).toHaveAccessibleDescription(`Error: ${enterTheItem} ${hint} ${help}`);
    expect(addsTo(outing.id)).toEqual([]);
    expect(addAlert('Community outing')).toBeEmptyDOMElement();
    // A one-field form: no error summary, and nothing in front of the page's title (form-validation.md rule 6).
    expect(screen.queryByRole('heading', { name: 'There is a problem' })).toBeNull();
    expect(document.title).toBe('Grow2Notes – Common items');
  });

  it.each([
    ['only spaces and line breaks', ' \n\t \n ', enterTheItem],
    ['201 characters once its line break is a space', `${'a'.repeat(100)}\n${'b'.repeat(100)}`, tooLong],
  ])('on Add item with %s, shows its message, focused, and sends nothing', async (_, text, message) => {
    await openPage();
    type(newItem('Every note'), text);

    await press(addItemButton('Every note'));

    expect(fieldMessage('Every note')).toBe(message);
    expect(newItem('Every note')).toHaveFocus();
    expect(newItem('Every note')).toHaveAttribute('aria-invalid', 'true');
    expect(newItem('Every note')).toHaveValue(text);
    expect(addsTo(everyNote.id)).toEqual([]);
  });

  it('keeps 210 characters typed whole, counting 10 too many, and refuses them on Add item', async () => {
    await openPage();
    const typed = 'x'.repeat(210);

    type(newItem('Personal care'), typed);
    expect(newItem('Personal care')).toHaveValue(typed);
    expect(shownCount('Personal care')?.textContent).toBe('You have 10 characters too many');
    expect(fieldMessage('Personal care')).toBeNull();
    await press(addItemButton('Personal care'));

    expect(newItem('Personal care')).toHaveValue(typed);
    expect(fieldMessage('Personal care')).toBe(tooLong);
    expect(addsTo(care.id)).toEqual([]);
  });

  it('once the message shows, checks the text at each input, and takes it away silently once it passes', async () => {
    await openPage();
    await press(addItemButton('Community outing'));
    const field = newItem('Community outing');

    type(field, 'a'.repeat(201));
    expect(fieldMessage('Community outing')).toBe(tooLong);
    type(field, 'Visited the library');

    expect(fieldMessage('Community outing')).toBeNull();
    expect(field).not.toHaveAttribute('aria-invalid');
    expect(field).toHaveAccessibleDescription(`${hint} ${help}`);
    expect(addForm('Community outing').querySelector('[data-invalid]')).toBeNull();
    expect(pageStatus()).toBeEmptyDOMElement();
  });

  it('adds the item to the end of its own group alone, empties the field, keeps focus in it, says so', async () => {
    const router = await openPage();
    serveGroups(groups);
    type(newItem('Community outing'), 'Visited the library');
    const reads = listReads();

    await press(addItemButton('Community outing'));

    expect(addsTo(outing.id)).toEqual([{ text: 'Visited the library' }]);
    expect(listReads()).toBe(reads + 1);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([
      busOrTrain.text,
      paid.text,
      lunch.text,
      'Visited the library',
    ]);
    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([medication.text, meal.text]);
    expect(activeItems(sectionOf('Personal care'))).toEqual([]);
    expect(newItem('Community outing')).toHaveValue('');
    expect(newItem('Community outing')).toHaveFocus();
    expect(addAlert('Community outing')).toBeEmptyDOMElement();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(added);
    // Nothing the manager typed is kept where it could outlive the page (D22).
    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe('/manage/common-items');
    expect(document.title).toBe('Grow2Notes – Common items');
    expect(JSON.stringify(router.state.location.state ?? null)).toBe('null');
    expect(JSON.stringify(queryClient.getQueryCache().getAll().map(({ queryKey }) => queryKey))).toBe(
      JSON.stringify([['me'], ['admin', 'common-item-groups']]),
    );
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });

  it('adds five items to Every note in a row, with focus in its field after each (design.md §14 M1)', async () => {
    await openPage([{ ...everyNote, items: [] }]);
    serveGroups([{ ...everyNote, items: [] }]);
    const texts = ['Medication prompted', 'Meal prepared', 'Laundry done', 'Bed made', 'Showered'];

    for (const [index, text] of texts.entries()) {
      type(newItem('Every note'), text);
      await press(addItemButton('Every note'));

      expect(newItem('Every note')).toHaveFocus();
      expect(newItem('Every note')).toHaveValue('');
      expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual(texts.slice(0, index + 1));
    }

    expect(addsTo(everyNote.id)).toEqual(texts.map((text) => ({ text })));
  });

  it('sends a pasted line break as one space, with the ends trimmed', async () => {
    await openPage();
    serveGroups(groups);

    fireEvent.paste(newItem('Personal care'));
    type(newItem('Personal care'), '  Showered\r\n  and dressed ');
    await press(addItemButton('Personal care'));

    expect(addsTo(care.id)).toEqual([{ text: 'Showered and dressed' }]);
    expect(activeItems(sectionOf('Personal care')).map(textOf)).toEqual(['Showered and dressed']);
  });

  it('shows Add item busy at once, "Adding…" after 400 ms, and a press while it is sends nothing more', async () => {
    await openPage();
    serve('POST', itemsPath(outing.id), noAnswer);
    type(newItem('Community outing'), 'Visited the library');
    const button = addItemButton('Community outing');

    button.focus();
    fireEvent.click(button);
    await settle();
    expect(button).toHaveAttribute('aria-disabled', 'true');
    await wait(398);
    expect(button).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(button).toHaveAttribute('data-busy');
    expect(within(button).getByText('Adding…')).toBeInTheDocument();
    fireEvent.click(button);
    await settle();

    expect(addsTo(outing.id)).toHaveLength(1);
    expect(newItem('Community outing')).toHaveValue('Visited the library');
  });

  it.each([
    ['no connection', noConnection, 'Not added: no connection. Try again.'],
    ['a 503', problem(503), 'Not added: something went wrong. Try again.'],
  ])('on %s, says so above Add item, keeping the text and focus on Add item, and reads nothing', async (
    _,
    answer,
    words,
  ) => {
    await openPage();
    serve('POST', itemsPath(outing.id), answer);
    type(newItem('Community outing'), 'Visited the library');
    const button = addItemButton('Community outing');
    const reads = listReads();

    await press(button);

    expect(addAlert('Community outing')).toHaveTextContent(words);
    expect(button).toHaveFocus();
    expect(newItem('Community outing')).toHaveValue('Visited the library');
    expect(newItem('Community outing')).not.toHaveAttribute('aria-invalid');
    expect(addsTo(outing.id)).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(pageStatus()).not.toHaveTextContent(added);
  });

  // The add has no idempotency key (design.md §6.6), so one sent again after a lost answer could add the item twice.
  it('sends one add for an answer that never comes, saying "no connection" after 10 s, and never again', async () => {
    await openPage();
    serve('POST', itemsPath(outing.id), noAnswer);
    type(newItem('Community outing'), 'Visited the library');

    await press(addItemButton('Community outing'));
    await wait(10_000);
    await settle();

    expect(addAlert('Community outing')).toHaveTextContent('Not added: no connection. Try again.');
    expect(addItemButton('Community outing')).toHaveFocus();
    expect(newItem('Community outing')).toHaveValue('Visited the library');
    await wait(60_000);
    expect(addsTo(outing.id)).toHaveLength(1);
  });

  it('on a 409 for a group archived meanwhile, reads the list again and focuses the list conflict', async () => {
    await openPage();
    serve('POST', itemsPath(outing.id), problem(409, 'config.group_archived'));
    serve('GET', listPath, json([everyNote, { ...outing, archivedAtUtc: '2026-10-01T06:00:00Z' }, care]));
    type(newItem('Community outing'), 'Visited the library');
    const reads = listReads();

    await press(addItemButton('Community outing'));

    expect(listReads()).toBe(reads + 1);
    expect(listConflict()).toHaveFocus();
    expect(sections()).toEqual([sectionOf('Every note'), sectionOf('Personal care')]);
    expect(addsTo(outing.id)).toHaveLength(1);
  });

  it('keeps the text, with the list conflict focused, for a 409 whose re-read still lists the group', async () => {
    await openPage();
    serve('POST', itemsPath(outing.id), problem(409, 'config.group_archived'));
    type(newItem('Community outing'), 'Visited the library');

    await press(addItemButton('Community outing'));

    expect(listConflict()).toHaveFocus();
    expect(newItem('Community outing')).toHaveValue('Visited the library');
    expect(addAlert('Community outing')).toBeEmptyDOMElement();
  });

  it('hands a 401 to sign-in in place, keeping the text, and reads the list again once signed in', async () => {
    await openPage();
    serve('POST', itemsPath(outing.id), problem(401));
    serve('POST', '/api/auth/passkey/options', json({ challenge: 'Y2hhbGxlbmdl', userVerification: 'required' }));
    serve('POST', '/api/auth/login', json({ next: 'totp' }));
    serve('POST', '/api/auth/login/totp', json(manager));
    type(newItem('Community outing'), 'Visited the library');
    // Found now, as the page leaves the accessibility tree once the shell's sign-in in place hides it.
    const page = { field: newItem('Community outing'), alert: addAlert('Community outing') };

    await press(addItemButton('Community outing'));

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    expect(page.field).not.toBeVisible();
    expect(page.field).toHaveValue('Visited the library');
    expect(page.alert).toBeEmptyDOMElement();
    const reads = listReads();

    type(screen.getByRole('textbox', { name: 'Email address' }), 'sam.lee@example.org');
    type(screen.getByLabelText('Password'), 'correct horse battery staple');
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    await settle();
    await settle();
    type(screen.getByRole('textbox', { name: 'Enter the 6-digit code from your authenticator app' }), '123456');
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await settle();
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Common items');
    expect(listReads()).toBe(reads + 1);
    expect(newItem('Community outing')).toHaveValue('Visited the library');
    expect(addAlert('Community outing')).toBeEmptyDOMElement();
    expect(addsTo(outing.id)).toHaveLength(1);
  });
});

const saved = 'Item saved';

/** The rewords sent to the item of `itemId`, each as its `If-Match` and the JSON of its body, in the order sent. */
function rewordsOf(itemId: string): { ifMatch: string | null; body: unknown }[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    path === itemPath(itemId) && init?.method === 'PUT'
      ? [{ ifMatch: new Headers(init.headers).get('If-Match'), body: JSON.parse(init.body as string) as unknown }]
      : [],
  );
}

/** The Edit of the item whose text is `text`, in the group named `name`, by its name, which ends with the text. */
function editButton(name: string, text: string): HTMLElement {
  return within(sectionOf(name)).getByRole('button', { name: `Edit, ${text}` });
}

/** The item's Edit item field, by the id that the page gives it, or `null` while its row is not being edited. */
function editField(itemId: string): HTMLTextAreaElement | null {
  return document.getElementById(`item-${itemId}-text`) as HTMLTextAreaElement | null;
}

/** The item's edit form. */
function editForm(itemId: string): HTMLFormElement {
  return editField(itemId)!.closest('form')!;
}

/** The item's Save, by its name, which its busy label leaves as it is. */
function saveButton(itemId: string): HTMLElement {
  return within(editForm(itemId)).getByRole('button', { name: 'Save' });
}

function cancelButton(itemId: string): HTMLElement {
  return within(editForm(itemId)).getByRole('button', { name: 'Cancel' });
}

/** The alert above the item's Save. */
function editAlert(itemId: string): HTMLElement {
  return within(editForm(itemId)).getByRole('alert');
}

/** The message on the item's Edit item field, without its hidden "Error: ", or `null` while it shows none. */
function editMessage(itemId: string): string | null {
  const message = editForm(itemId).querySelector('[id$="-error"]');
  return message === null ? null : (message.textContent ?? '').replace(/^Error: /, '');
}

/** Focuses the Edit of `row` in the group named `name` and presses it, and gives the field that takes its place. */
function openEdit(name: string, row: ListRow): HTMLTextAreaElement {
  const button = editButton(name, row.text);
  button.focus();
  fireEvent.click(button);
  return editField(row.id)!;
}

describe('Common items, rewording an item', () => {
  it('turns the row into a form in place, still numbered: "Edit item", its text, caret at the end', async () => {
    await openPage();
    const section = sectionOf('Community outing');

    const field = openEdit('Community outing', paid);

    const row = activeItems(section)[1];
    expect(activeItems(section)).toHaveLength(3);
    expect(field.closest('li')).toBe(row);
    expect(editForm(paid.id)).toHaveAttribute('novalidate');
    expect(field.tagName).toBe('TEXTAREA');
    expect(document.querySelector(`label[for="item-${paid.id}-text"]`)?.textContent).toBe('Edit item');
    expect(field).toHaveAccessibleName('Edit item');
    expect(field).toHaveAttribute('rows', '3');
    for (const attribute of ['maxlength', 'required', 'readonly', 'aria-invalid']) {
      expect(field).not.toHaveAttribute(attribute);
    }

    expect(field).toHaveValue(paid.text);
    expect(field).toHaveFocus();
    expect([field.selectionStart, field.selectionEnd]).toEqual([paid.text.length, paid.text.length]);
    // Described as the add's field is: the hidden hint, then the page's help text, never the count.
    expect(field.getAttribute('aria-describedby')).toBe(`item-${paid.id}-text-hint items-help`);
    expect(field).toHaveAccessibleDescription(`${hint} ${help}`);
    expect(within(row).queryByRole('button', { name: /^Edit/ })).toBeNull();
    expect(within(row).queryByText(paid.text, { selector: 'p' })).toBeNull();
    expect(editButton('Community outing', busOrTrain.text)).toBeInTheDocument();
  });

  it('shows the count, then the empty alert, then Save, primary, and Cancel, both label width', async () => {
    await openPage();

    const field = openEdit('Community outing', paid);

    const count = editForm(paid.id).querySelector<HTMLElement>('p[aria-hidden="true"]')!;
    expect(count.textContent).toBe(`You have ${200 - paid.text.length} characters remaining`);
    expect(editAlert(paid.id)).toBeEmptyDOMElement();
    const save = saveButton(paid.id);
    const cancel = cancelButton(paid.id);
    expect(follows(field, count)).toBe(true);
    expect(follows(count, editAlert(paid.id))).toBe(true);
    expect(follows(editAlert(paid.id), save)).toBe(true);
    expect(follows(save, cancel)).toBe(true);
    expect(save).toHaveAttribute('type', 'submit');
    expect(save.className).toMatch(/primary/);
    expect(cancel).toHaveAttribute('type', 'button');
    expect(cancel.className).toMatch(/secondary/);
    for (const button of [save, cancel]) {
      expect(button.className).toMatch(/rowButton/);
    }
  });

  it("on Cancel, drops what was typed, sends nothing, and puts focus on the row's Edit", async () => {
    await openPage();
    type(openEdit('Community outing', paid), 'Paid at the machine');

    await press(cancelButton(paid.id));

    expect(rewordsOf(paid.id)).toEqual([]);
    expect(editField(paid.id)).toBeNull();
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    expect(editButton('Community outing', paid.text)).toHaveFocus();
    expect(openEdit('Community outing', paid)).toHaveValue(paid.text);
  });

  it('on Save of the text unchanged once cleaned, closes as Cancel does and sends nothing', async () => {
    await openPage();
    type(openEdit('Community outing', paid), '  Paid for a ticket\n  at the counter ');

    await press(saveButton(paid.id));
    await wait(16);

    expect(rewordsOf(paid.id)).toEqual([]);
    expect(editField(paid.id)).toBeNull();
    expect(editButton('Community outing', paid.text)).toHaveFocus();
    expect(pageStatus()).toBeEmptyDOMElement();
  });

  it.each([
    ['only spaces and line breaks', ' \n\t \n ', enterTheItem],
    ['201 characters once its line break is a space', `${'a'.repeat(100)}\n${'b'.repeat(100)}`, tooLong],
  ])('on Save with %s, shows its message above the field, focused, and sends nothing', async (_, text, message) => {
    await openPage();
    const field = openEdit('Every note', meal);
    type(field, text);

    await press(saveButton(meal.id));

    expect(rewordsOf(meal.id)).toEqual([]);
    expect(editMessage(meal.id)).toBe(message);
    expect(field).toHaveFocus();
    expect(field).toHaveValue(text);
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field.getAttribute('aria-describedby')).toBe(
      `item-${meal.id}-text-error item-${meal.id}-text-hint items-help`,
    );
    expect(field).toHaveAccessibleDescription(`Error: ${message} ${hint} ${help}`);
    expect(follows(within(editForm(meal.id)).getByText(message), field)).toBe(true);
    expect(editAlert(meal.id)).toBeEmptyDOMElement();
    // A one-field form: no error summary, and nothing in front of the page's title (form-validation.md rule 6).
    expect(screen.queryByRole('heading', { name: 'There is a problem' })).toBeNull();
    expect(document.title).toBe('Grow2Notes – Common items');
  });

  it("sends the text on one line with the item's version, reads the list again, shows it, focusing Edit", async () => {
    const router = await openPage();
    serveGroups(groups);
    type(openEdit('Community outing', paid), ' Paid at\n  the machine ');
    const reads = listReads();

    await press(saveButton(paid.id));

    expect(rewordsOf(paid.id)).toEqual([{ ifMatch: `"${paid.rowVersion}"`, body: { text: 'Paid at the machine' } }]);
    expect(listReads()).toBe(reads + 1);
    expect(editField(paid.id)).toBeNull();
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([
      busOrTrain.text,
      'Paid at the machine',
      lunch.text,
    ]);
    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([medication.text, meal.text]);
    expect(editButton('Community outing', 'Paid at the machine')).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(saved);
    // Nothing the manager typed is kept where it could outlive the page (D22).
    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe('/manage/common-items');
    expect(document.title).toBe('Grow2Notes – Common items');
    expect(JSON.stringify(router.state.location.state ?? null)).toBe('null');
    expect(JSON.stringify(queryClient.getQueryCache().getAll().map(({ queryKey }) => queryKey))).toBe(
      JSON.stringify([['me'], ['admin', 'common-item-groups']]),
    );
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });

  it('shows Save busy, "Saving…" after 400 ms, the field read-only; a press while it is sends no more', async () => {
    await openPage();
    serve('PUT', itemPath(paid.id), noAnswer);
    const field = openEdit('Community outing', paid);
    type(field, 'Paid at the machine');
    const save = saveButton(paid.id);

    save.focus();
    fireEvent.click(save);
    await settle();
    expect(save).toHaveAttribute('aria-disabled', 'true');
    expect(field).toHaveAttribute('readonly');
    await wait(398);
    expect(save).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(save).toHaveAttribute('data-busy');
    expect(within(save).getByText('Saving…')).toBeInTheDocument();
    fireEvent.click(save);
    await settle();

    expect(rewordsOf(paid.id)).toHaveLength(1);
    expect(field).toHaveValue('Paid at the machine');
  });

  it.each([
    ['no connection', noConnection, 'Not saved: no connection. Try again.'],
    ['a 503', problem(503), 'Not saved: something went wrong. Try again.'],
  ])('on %s, says so above Save, keeping the text and focus on Save, and reads nothing', async (_, answer, words) => {
    await openPage();
    serve('PUT', itemPath(paid.id), answer);
    const field = openEdit('Community outing', paid);
    type(field, 'Paid at the machine');
    const save = saveButton(paid.id);
    const reads = listReads();

    await press(save);

    expect(editAlert(paid.id)).toHaveTextContent(words);
    expect(save).toHaveFocus();
    expect(field).toHaveValue('Paid at the machine');
    expect(field).not.toHaveAttribute('readonly');
    expect(field).not.toHaveAttribute('aria-invalid');
    expect(rewordsOf(paid.id)).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(listConflict()).toBeNull();
    expect(pageStatus()).not.toHaveTextContent(saved);
  });

  it('on a 412 finding the item active, keeps the text, quotes what it says now, saves at the next Save', async () => {
    await openPage();
    const held = serveGroups(groups);
    const field = openEdit('Community outing', paid);
    type(field, 'Paid at the machine');
    const save = saveButton(paid.id);
    // Another manager rewords it meanwhile.
    held[1].items[1] = { ...paid, text: 'Paid for a ticket online', rowVersion: 'AAAAAAAAE999=' };

    await press(save);

    expect(rewordsOf(paid.id)).toEqual([{ ifMatch: `"${paid.rowVersion}"`, body: { text: 'Paid at the machine' } }]);
    expect(editAlert(paid.id)).toHaveTextContent(
      'Someone else changed this item while you were editing. It now says: ‘Paid for a ticket online’. Save to '
        + 'replace it with your wording, or Cancel to keep it.',
    );
    expect(field).toHaveValue('Paid at the machine');
    expect(save).toHaveFocus();
    expect(listConflict()).toBeNull();

    await press(save);

    expect(rewordsOf(paid.id).map(({ ifMatch }) => ifMatch)).toEqual([`"${paid.rowVersion}"`, '"AAAAAAAAE999="']);
    expect(editField(paid.id)).toBeNull();
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([
      busOrTrain.text,
      'Paid at the machine',
      lunch.text,
    ]);
    expect(editButton('Community outing', 'Paid at the machine')).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(saved);
  });

  it('on a 412 finding the item archived, shows the list conflict, focused, the row gone with its form', async () => {
    await openPage();
    const held = serveGroups(groups);
    type(openEdit('Community outing', paid), 'Paid at the machine');
    // Another manager archives it meanwhile.
    held[1].items[1] = { ...paid, archivedAtUtc: '2026-10-01T06:00:00Z', rowVersion: 'AAAAAAAAE999=' };

    await press(saveButton(paid.id));

    expect(listConflict()).toHaveFocus();
    expect(editField(paid.id)).toBeNull();
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, lunch.text]);
    expect(archivedItems(sectionOf('Community outing')).map(textOf)).toEqual([paid.text]);
    expect(rewordsOf(paid.id)).toHaveLength(1);
  });

  it('on a 409 for a group archived meanwhile, reads the list again and focuses the list conflict', async () => {
    await openPage();
    const held = serveGroups(groups);
    type(openEdit('Community outing', paid), 'Paid at the machine');
    held[1].archivedAtUtc = '2026-10-01T06:00:00Z';
    const reads = listReads();

    await press(saveButton(paid.id));

    expect(listReads()).toBe(reads + 1);
    expect(listConflict()).toHaveFocus();
    expect(sections()).toEqual([sectionOf('Every note'), sectionOf('Personal care')]);
    expect(rewordsOf(paid.id)).toHaveLength(1);
  });

  it('keeps the text, with the list conflict focused, for a 409 whose re-read still lists the group', async () => {
    await openPage();
    serve('PUT', itemPath(paid.id), problem(409, 'config.group_archived'));
    const field = openEdit('Community outing', paid);
    type(field, 'Paid at the machine');

    await press(saveButton(paid.id));

    expect(listConflict()).toHaveFocus();
    expect(field).toHaveValue('Paid at the machine');
    expect(editAlert(paid.id)).toBeEmptyDOMElement();
  });

  it('keeps rows of two groups in edit mode at once, each as typed, through a save in the other', async () => {
    await openPage();
    serveGroups(groups);
    type(openEdit('Every note', medication), 'Medication given');
    type(openEdit('Community outing', lunch), 'Bought lunch at the café');

    await press(saveButton(medication.id));

    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual(['Medication given', meal.text]);
    expect(editField(lunch.id)).toHaveValue('Bought lunch at the café');
    expect(editButton('Every note', 'Medication given')).toHaveFocus();

    await press(saveButton(lunch.id));

    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([
      busOrTrain.text,
      paid.text,
      'Bought lunch at the café',
    ]);
    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual(['Medication given', meal.text]);
    expect(rewordsOf(medication.id).map(({ body }) => body)).toEqual([{ text: 'Medication given' }]);
    expect(rewordsOf(lunch.id).map(({ body }) => body)).toEqual([{ text: 'Bought lunch at the café' }]);
  });

  it('hands a 401 on Save to sign-in in place, keeping the text, and reads the list again once signed in', async () => {
    await openPage();
    serve('PUT', itemPath(paid.id), problem(401));
    serve('POST', '/api/auth/passkey/options', json({ challenge: 'Y2hhbGxlbmdl', userVerification: 'required' }));
    serve('POST', '/api/auth/login', json({ next: 'totp' }));
    serve('POST', '/api/auth/login/totp', json(manager));
    const field = openEdit('Community outing', paid);
    type(field, 'Paid at the machine');
    // Found now, as the page leaves the accessibility tree once the shell's sign-in in place hides it.
    const alert = editAlert(paid.id);

    await press(saveButton(paid.id));

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    expect(field).not.toBeVisible();
    expect(field).toHaveValue('Paid at the machine');
    expect(alert).toBeEmptyDOMElement();
    const reads = listReads();

    type(screen.getByRole('textbox', { name: 'Email address' }), 'sam.lee@example.org');
    type(screen.getByLabelText('Password'), 'correct horse battery staple');
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    await settle();
    await settle();
    type(screen.getByRole('textbox', { name: 'Enter the 6-digit code from your authenticator app' }), '123456');
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await settle();
    await settle();

    expect(pageHeading()).toHaveAccessibleName('Common items');
    expect(listReads()).toBe(reads + 1);
    expect(editField(paid.id)).toHaveValue('Paid at the machine');
    expect(editAlert(paid.id)).toBeEmptyDOMElement();
    expect(rewordsOf(paid.id)).toHaveLength(1);
  });
});

/** The reorders sent for the group of `groupId`, each as its `If-Match` and the JSON of its body, in the order sent. */
function reordersOf(groupId: string): { ifMatch: string | null; body: unknown }[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    path === orderPath(groupId) && init?.method === 'PUT'
      ? [{ ifMatch: new Headers(init.headers).get('If-Match'), body: JSON.parse(init.body as string) as unknown }]
      : [],
  );
}

/** The path of each reorder sent, of any group, in the order sent. */
function reorderPaths(): string[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    typeof path === 'string' && path.endsWith('/items/order') && init?.method === 'PUT' ? [path] : [],
  );
}

/** The active row whose text is `text` in the group named `name`. */
function rowOf(name: string, text: string): HTMLLIElement {
  const row = activeItems(sectionOf(name)).find((listed) => textOf(listed) === text);
  expect(row).toBeDefined();
  return row!;
}

/** The row's Move up or Move down, by its name, which ends with the row's text. */
function moveButton(name: string, direction: 'up' | 'down', text: string): HTMLElement {
  return within(rowOf(name, text)).getByRole('button', { name: `Move ${direction}, ${text}` });
}

/** The alert in the row whose text is `text` in the group named `name`, above its buttons. */
function rowAlert(name: string, text: string): HTMLElement {
  return within(rowOf(name, text)).getByRole('alert');
}

/** Records each text that the page's status region says from now on, in order, as a screen reader would hear it. */
function recordAnnouncements(): () => string[] {
  const region = pageStatus();
  const written: string[] = [];
  new MutationObserver(() => {
    if (region.textContent !== '') {
      written.push(region.textContent ?? '');
    }
  }).observe(region, { childList: true, characterData: true, subtree: true });
  return () => written;
}

/**
 * Holds back the answer to each reorder of the group of `groupId`, as `serveGroups` gives it, until the test lets it
 * go, as a slow server would; the request is sent at once.
 */
function holdReorders(groupId: string): () => void {
  const answer = answers.get(`PUT ${orderPath(groupId)}`)!;
  let release: () => void = () => {};
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  serve('PUT', orderPath(groupId), async (init) => {
    await released;
    return answer(init);
  });
  return release;
}

// Community outing with five active items, so that a move's announcement counts the five of its group, not the two of
// Every note or its archived ones as well.
const library = item('9cc4f018-adb5-4b09-9ebe-6a8cafc13f29', 'Visited the library', 3);
const swim = item('0dd5a129-bec6-4c1a-8fcf-7b9db0d24a3a', 'Went for a swim', 4);
const longOuting = { ...outing, items: [busOrTrain, paid, lunch, library, swim] };

describe('Common items, moving an item', () => {
  it("on Move up on row 2, sends its group's active IDs, rows 1 and 2 swapped, focus on its Move up", async () => {
    await openPage();
    serveGroups(groups);
    const reads = listReads();

    await press(moveButton('Every note', 'up', meal.text));

    // Every note's two active items alone: not its archived ones, and nothing of another group; with no If-Match, as
    // the set of IDs is the check (design.md §6.7).
    expect(reordersOf(everyNote.id)).toEqual([{ ifMatch: null, body: { commonItemIds: [meal.id, medication.id] } }]);
    expect(reorderPaths()).toEqual([orderPath(everyNote.id)]);
    expect(listReads()).toBe(reads + 1);
    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([meal.text, medication.text]);
    expect(archivedItems(sectionOf('Every note')).map(textOf)).toEqual([bed.text, laundry.text]);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    expect(document.activeElement).toBe(moveButton('Every note', 'up', meal.text));
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Moved up to number 1 of 2');
  });

  // Move down on row 1 is the case in which React moves the focused button's row (common-items.md Interactions).
  it("after Move down on row 1, leaves focus on the moved item's Move down, never on <body>", async () => {
    await openPage();
    serveGroups(groups);

    await press(moveButton('Community outing', 'down', busOrTrain.text));

    expect(reordersOf(outing.id).map(({ body }) => body)).toEqual([
      { commonItemIds: [paid.id, busOrTrain.id, lunch.id] },
    ]);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([paid.text, busOrTrain.text, lunch.text]);
    expect(document.activeElement).toBe(moveButton('Community outing', 'down', busOrTrain.text));
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Moved down to number 2 of 3');
  });

  it('says where the item went, counted within its group: "Moved up to number 2 of 5", and down', async () => {
    await openPage([everyNote, longOuting, care]);
    serveGroups([everyNote, longOuting, care]);
    const announced = recordAnnouncements();

    await press(moveButton('Community outing', 'up', lunch.text));
    await wait(16);
    await press(moveButton('Community outing', 'down', paid.text));
    await wait(16);

    expect(announced()).toEqual(['Moved up to number 2 of 5', 'Moved down to number 4 of 5']);
    expect(reordersOf(outing.id).map(({ body }) => body)).toEqual([
      { commonItemIds: [busOrTrain.id, lunch.id, paid.id, library.id, swim.id] },
      { commonItemIds: [busOrTrain.id, lunch.id, library.id, paid.id, swim.id] },
    ]);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([
      busOrTrain.text,
      lunch.text,
      library.text,
      paid.text,
      swim.text,
    ]);
    expect(document.activeElement).toBe(moveButton('Community outing', 'down', paid.text));
  });

  it('keeps the order on screen while the PUT is out, busy at once and "Moving…" after 400 ms', async () => {
    await openPage();
    serveGroups(groups);
    const release = holdReorders(outing.id);
    const down = moveButton('Community outing', 'down', busOrTrain.text);

    down.focus();
    fireEvent.click(down);
    await settle();
    expect(reordersOf(outing.id)).toHaveLength(1);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    expect(down).toHaveAttribute('aria-disabled', 'true');
    await wait(398);
    expect(down).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(down).toHaveAttribute('data-busy');
    expect(within(down).getByText('Moving…')).toBeInTheDocument();
    await press(down);
    expect(reordersOf(outing.id)).toHaveLength(1);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);

    release();
    await settle();
    await settle();

    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([paid.text, busOrTrain.text, lunch.text]);
    expect(down).not.toHaveAttribute('aria-disabled');
    expect(down).toHaveFocus();
  });

  it("makes each group's end buttons unavailable in place; a press sends nothing and says why, once", async () => {
    await openPage();
    const announced = recordAnnouncements();

    for (const [name, first, last] of [
      ['Every note', medication, meal],
      ['Community outing', busOrTrain, lunch],
    ] as const) {
      for (const end of [moveButton(name, 'up', first.text), moveButton(name, 'down', last.text)]) {
        expect(end).toHaveAttribute('aria-disabled', 'true');
        expect(end).toHaveAttribute('data-unavailable');
        expect(end).toBeEnabled();
        expect(getComputedStyle(end).display).not.toBe('none');
      }

      for (const free of [moveButton(name, 'down', first.text), moveButton(name, 'up', last.text)]) {
        expect(free).not.toHaveAttribute('aria-disabled');
      }
    }

    await press(moveButton('Community outing', 'up', busOrTrain.text));
    await wait(16);
    expect(announced()).toEqual(['Already at the top']);
    expect(moveButton('Community outing', 'up', busOrTrain.text)).toHaveFocus();
    await press(moveButton('Every note', 'down', meal.text));
    await wait(16);

    expect(announced()).toEqual(['Already at the top', 'Already at the bottom']);
    expect(moveButton('Every note', 'down', meal.text)).toHaveFocus();
    expect(reorderPaths()).toEqual([]);
    expect(queryClient.getMutationCache().getAll()).toEqual([]);
  });

  it("runs two quick presses in two groups one after the other, the second from the first's re-read", async () => {
    await openPage();
    const held = serveGroups(groups);
    const release = holdReorders(everyNote.id);
    const reads = listReads();

    await press(moveButton('Every note', 'down', medication.text));
    await press(moveButton('Community outing', 'up', lunch.text));
    expect(reorderPaths()).toEqual([orderPath(everyNote.id)]);
    // Another manager adds an item to Community outing meanwhile, which the first move's re-read brings.
    held[1].items.push(library);
    release();
    await settle();
    await settle();
    await settle();

    expect(reorderPaths()).toEqual([orderPath(everyNote.id), orderPath(outing.id)]);
    expect(reordersOf(everyNote.id).map(({ body }) => body)).toEqual([{ commonItemIds: [meal.id, medication.id] }]);
    expect(reordersOf(outing.id).map(({ body }) => body)).toEqual([
      { commonItemIds: [busOrTrain.id, lunch.id, paid.id, library.id] },
    ]);
    expect(listReads()).toBe(reads + 2);
    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([meal.text, medication.text]);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([
      busOrTrain.text,
      lunch.text,
      paid.text,
      library.text,
    ]);
    expect(moveButton('Community outing', 'up', lunch.text)).toHaveFocus();
  });

  it('on a 422, reads the list again and focuses the list conflict, until a write succeeds', async () => {
    await openPage();
    const held = serveGroups(groups);
    // Another manager archives an item of the group meanwhile.
    held[1].items[2] = { ...lunch, archivedAtUtc: '2026-10-01T06:00:00Z', rowVersion: 'AAAAAAAAE999=' };
    const reads = listReads();

    await press(moveButton('Community outing', 'down', busOrTrain.text));

    expect(reordersOf(outing.id).map(({ body }) => body)).toEqual([
      { commonItemIds: [paid.id, busOrTrain.id, lunch.id] },
    ]);
    expect(listReads()).toBe(reads + 1);
    expect(listConflict()).toHaveFocus();
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text]);
    expect(archivedItems(sectionOf('Community outing')).map(textOf)).toEqual([lunch.text]);
    expect(rowAlert('Community outing', busOrTrain.text)).toBeEmptyDOMElement();

    await press(moveButton('Community outing', 'down', busOrTrain.text));

    expect(listConflict()).toBeNull();
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([paid.text, busOrTrain.text]);
  });

  it.each([
    ['no connection', noConnection, 'Not moved: no connection. Try again.'],
    ['a 503', problem(503), 'Not moved: something went wrong. Try again.'],
  ])("on %s, says so in the row's alert, keeping the order and focus, and reads nothing", async (_, answer, words) => {
    await openPage();
    serve('PUT', orderPath(outing.id), answer);
    const down = moveButton('Community outing', 'down', busOrTrain.text);
    const reads = listReads();

    await press(down);

    expect(rowAlert('Community outing', busOrTrain.text)).toHaveTextContent(words);
    expect(down).toHaveFocus();
    expect(down).not.toHaveAttribute('aria-disabled');
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    expect(reordersOf(outing.id)).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(listConflict()).toBeNull();
    expect(rowAlert('Every note', medication.text)).toBeEmptyDOMElement();
  });
});
