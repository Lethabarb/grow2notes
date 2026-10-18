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

/** An RFC 9457 problem, as the API answers a failure (design.md §6.1). */
function problem(status: number): Answer {
  const body = JSON.stringify({ type: 'about:blank', title: 'Problem', status });
  return () =>
    Promise.resolve(new Response(body, { status, headers: { 'Content-Type': 'application/problem+json' } }));
}

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
function serve(method: 'GET' | 'POST', path: string, answer: Answer) {
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
