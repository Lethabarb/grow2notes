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
const groupsPath = '/api/admin/common-item-groups';
const listPath = `${groupsPath}?includeArchived=true`;

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

/** A `422 validation.failed` naming each field in `errors` (design.md §6.1). */
function refused(errors: Record<string, string[]>): Answer {
  const body = JSON.stringify({
    type: 'about:blank',
    title: 'Problem',
    status: 422,
    code: 'validation.failed',
    errors,
  });
  return () =>
    Promise.resolve(new Response(body, { status: 422, headers: { 'Content-Type': 'application/problem+json' } }));
}

/** A `201` with no body, as an add answers (design.md §6.6). */
const created: Answer = () => Promise.resolve(new Response(null, { status: 201 }));

/** A `201` with a JSON body, as a group add answers with the new group's ID (design.md §6.6). */
function createdWith(body: unknown): Answer {
  return () =>
    Promise.resolve(
      new Response(JSON.stringify(body), { status: 201, headers: { 'Content-Type': 'application/json' } }),
    );
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
      // First in the section, on its own or first in its heading row.
      expect(heading.previousElementSibling).toBeNull();
      expect(section.firstElementChild?.contains(heading)).toBe(true);
      expect(heading).toHaveStyle({ overflowWrap: 'anywhere' });
    }
  });

  it('gives an archived group no section and none of its items, only its name in Archived groups', async () => {
    await openPage();

    expect(within(main()).queryByRole('region', { name: respite.name })).not.toBeInTheDocument();
    expect(within(main()).queryByText(packed.text)).not.toBeInTheDocument();
    expect(within(main()).getAllByText(respite.name)).toEqual([archivedGroupRow(respite.name).querySelector('p')]);
  });

  it('gives Every note its line under its <h2> and no group buttons, not even unavailable ones', async () => {
    await openPage();
    const section = sectionOf('Every note');

    const everyNoteParagraph = within(section).getByText(everyNoteLine);
    expect(everyNoteParagraph.tagName).toBe('P');
    expect(within(section).getByRole('heading', { level: 2 }).nextElementSibling).toBe(everyNoteParagraph);
    expect(within(section).queryByRole('button', { name: /^Rename/ })).not.toBeInTheDocument();
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

  it('gives each other group its <h2>, alert, Move up, Move down, Rename and Archive, each named by it', async () => {
    await openPage();

    for (const { name } of [outing, care]) {
      const section = sectionOf(name);
      const heading = within(section).getByRole('heading', { level: 2 });
      const head = heading.parentElement!;
      expect(head.parentElement).toBe(section);
      expect(head.firstElementChild).toBe(heading);
      expect(head).toHaveStyle({ display: 'flex', flexWrap: 'wrap' });
      expect(within(section).queryByText(everyNoteLine)).toBeNull();
      expectButtons(head, [
        `Move up, ${name} group`,
        `Move down, ${name} group`,
        `Rename, ${name} group`,
        `Archive, ${name} group`,
      ]);
      const buttons = within(head).getAllByRole('button');
      // In two pairs, Move up and Move down, then Rename and Archive.
      expect(buttons[0].parentElement).toBe(buttons[1].parentElement);
      expect(buttons[2].parentElement).toBe(buttons[3].parentElement);
      expect(buttons[1].parentElement).not.toBe(buttons[2].parentElement);
      for (const button of buttons) {
        expect(button).toHaveAttribute('type', 'button');
        expect(button).not.toHaveAttribute('aria-label');
        expect(button.className).toMatch(/secondary/);
        // Label width, at least 44 px wide, as an item row's buttons are (common-items.md *Conflicts resolved* 6).
        expect(button).toHaveStyle({ inlineSize: 'auto', minInlineSize: 'var(--target-min)' });
        const context = within(button).getByText(`, ${name} group`);
        expect(context).toHaveClass('visually-hidden');
      }

      const alert = within(head).getByRole('alert');
      expect(alert).toBeEmptyDOMElement();
      expect(follows(heading, alert)).toBe(true);
      expect(follows(alert, buttons[0])).toBe(true);
      // Then the group's items, or its empty line.
      const items = section.querySelector('ol') ?? within(section).getByText(emptyLine);
      expect(follows(head, items)).toBe(true);
      expect(head.contains(items)).toBe(false);
    }
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

/** A `200` with no body and no `ETag`, as a reword and a group's rename answer (design.md §6.6). */
const reworded: Answer = () => Promise.resolve(new Response(null, { status: 200 }));

/** The path that the group of `groupId` is renamed at (design.md §6.6). */
function groupPath(groupId: string): string {
  return `${groupsPath}/${groupId}`;
}

/** The path that the order of the groups is put to (design.md §6.6). */
const groupOrderPath = `${groupsPath}/order`;

/** The path that the order of the items of the group of `groupId` is put to (design.md §6.6). */
function orderPath(groupId: string): string {
  return `${itemsPath(groupId)}/order`;
}

/** A `204` with no body, as a reorder answers (design.md §6.6). */
const reordered: Answer = () => Promise.resolve(new Response(null, { status: 204 }));

/** The path that the item of `itemId` is archived or restored at (design.md §6.6). */
function placePath(itemId: string, action: 'archive' | 'restore'): string {
  return `${itemPath(itemId)}/${action}`;
}

/** A `204` with no body, as an archive and a restore answer (design.md §6.6). */
const placed: Answer = () => Promise.resolve(new Response(null, { status: 204 }));

/** The path that the group of `groupId` is archived or restored at (design.md §6.6). */
function groupPlacePath(groupId: string, action: 'archive' | 'restore'): string {
  return `${groupPath(groupId)}/${action}`;
}

/**
 * Has the groups list, the group adds and reorders, each group's renames, archives, restores, adds and reorders, and
 * each item's rewords, archives and restores answered by a server that holds `list`, and gives what it holds, which a
 * test may change as another manager would: a group add puts a group, with no items and an ID and a version of its own,
 * at the end of the active groups other than Every note, one after the highest active `sortOrder` (A2), in the list's
 * order, and answers `201` with its ID; a group reorder refuses IDs that are not the active groups other than Every
 * note, each once (`422 config.order_mismatch`), or else gives them `sortOrder` 0 to n-1, with a new version for each
 * group whose `sortOrder` changed, puts the list in its order, Every note first, and answers `204` with no body; a
 * rename refuses an `If-Match` that is not the group's version (`412`), then Every note (`422
 * config.every_note_fixed`), or else gives the group its name and a new version, and answers `200` with no body; a
 * group archive or restore refuses an `If-Match` that is not the group's version (`412`), then Every note (`422
 * config.every_note_fixed`), or else archives the group in its place, with its items as they were, later than anything
 * archived before it, or restores it, with its items, to the end of the active groups other than Every note, one after
 * the highest active `sortOrder` (A2), in the list's order, each with a new version, and answers `204` with no body;
 * an add puts an item, with an ID and a version of its own, at
 * the end of its group, one after the highest active `sortOrder` (A2), and answers `201` with no body; a reword refuses
 * an `If-Match` that is not the item's version (`412`), then an item of an archived group (`409
 * config.group_archived`), or else gives the item its text and a new version, and answers `200` with no body; a reorder
 * refuses IDs that are not the group's active items, each once (`422 config.order_mismatch`), or else gives them
 * `sortOrder` 0 to n-1, with a new version for each item whose `sortOrder` changed, and answers `204` with no body; an
 * archive or a restore refuses an `If-Match` that is not the item's version (`412`), or else archives the item in its
 * place, later than anything archived before it, or restores it to the end of its group, one after the highest active
 * `sortOrder` (A2), each with a new version, and answers `204` with no body; and a read gives what it holds.
 */
function serveGroups(list: readonly AdminCommonItemGroup[]): AdminCommonItemGroup[] {
  const held = list.map((listed) => ({ ...listed, items: [...listed.items] }));
  let versions = 0;
  const nextVersion = () => {
    versions += 1;
    return `AAAAAAAAE${String(versions).padStart(3, '0')}=`;
  };
  let archives = 0;
  const servePlace = (heldGroup: AdminCommonItemGroup, itemId: string) => {
    for (const action of ['archive', 'restore'] as const) {
      serve('POST', placePath(itemId, action), (init) => {
        const at = heldGroup.items.findIndex((listed) => listed.id === itemId);
        const listed = heldGroup.items[at];
        if (new Headers(init?.headers).get('If-Match') !== `"${listed.rowVersion}"`) {
          return problem(412)();
        }

        if (action === 'archive' && listed.archivedAtUtc === null) {
          archives += 1;
          const archivedAtUtc = `2026-10-01T06:${String(archives).padStart(2, '0')}:00Z`;
          heldGroup.items[at] = { ...listed, archivedAtUtc, rowVersion: nextVersion() };
        } else if (action === 'restore' && listed.archivedAtUtc !== null) {
          const active = heldGroup.items.flatMap((row) => (row.archivedAtUtc === null ? [row.sortOrder] : []));
          heldGroup.items.splice(at, 1);
          heldGroup.items.push({
            ...listed,
            sortOrder: Math.max(-1, ...active) + 1,
            archivedAtUtc: null,
            rowVersion: nextVersion(),
          });
        }

        return placed();
      });
    }
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
  // One after the highest active sortOrder of the groups other than Every note (A2).
  const endOfGroups = () => {
    const active = held.filter((listed) => !listed.isEveryNote && listed.archivedAtUtc === null);
    return Math.max(-1, ...active.map((listed) => listed.sortOrder)) + 1;
  };
  // In the list's order: after every group but Every note of its SortOrder or less, as it was added or restored last.
  const putInListOrder = (heldGroup: AdminCommonItemGroup) => {
    const after = held.findIndex((listed) => !listed.isEveryNote && listed.sortOrder > heldGroup.sortOrder);
    held.splice(after === -1 ? held.length : after, 0, heldGroup);
  };
  const serveGroup = (heldGroup: AdminCommonItemGroup) => {
    serve('PUT', groupPath(heldGroup.id), (init) => {
      if (new Headers(init?.headers).get('If-Match') !== `"${heldGroup.rowVersion}"`) {
        return problem(412)();
      }

      if (heldGroup.isEveryNote) {
        return problem(422, 'config.every_note_fixed')();
      }

      const { name } = JSON.parse(init?.body as string) as { name: string };
      heldGroup.name = name;
      heldGroup.rowVersion = nextVersion();
      return reworded();
    });
    for (const action of ['archive', 'restore'] as const) {
      serve('POST', groupPlacePath(heldGroup.id, action), (init) => {
        if (new Headers(init?.headers).get('If-Match') !== `"${heldGroup.rowVersion}"`) {
          return problem(412)();
        }

        if (heldGroup.isEveryNote) {
          return problem(422, 'config.every_note_fixed')();
        }

        if (action === 'archive' && heldGroup.archivedAtUtc === null) {
          archives += 1;
          heldGroup.archivedAtUtc = `2026-10-01T06:${String(archives).padStart(2, '0')}:00Z`;
          heldGroup.rowVersion = nextVersion();
        } else if (action === 'restore' && heldGroup.archivedAtUtc !== null) {
          heldGroup.sortOrder = endOfGroups();
          heldGroup.archivedAtUtc = null;
          heldGroup.rowVersion = nextVersion();
          held.splice(held.indexOf(heldGroup), 1);
          putInListOrder(heldGroup);
        }

        return placed();
      });
    }
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
      servePlace(heldGroup, id);
      return created();
    });
    for (const { id } of heldGroup.items) {
      serveReword(heldGroup, id);
      servePlace(heldGroup, id);
    }
  };
  serve('GET', listPath, json(held));
  serve('POST', groupsPath, (init) => {
    const { name } = JSON.parse(init?.body as string) as { name: string };
    const added = group(`5d7f9b1e-6a8c-4d93-8eb0-${String(held.length).padStart(12, '0')}`, name, endOfGroups(), []);
    putInListOrder(added);
    serveGroup(added);
    return createdWith({ id: added.id })();
  });
  serve('PUT', groupOrderPath, (init) => {
    const { groupIds } = JSON.parse(init?.body as string) as { groupIds: string[] };
    const active = held.filter((listed) => !listed.isEveryNote && listed.archivedAtUtc === null);
    if (
      new Set(groupIds).size !== groupIds.length
      || groupIds.length !== active.length
      || !active.every(({ id }) => groupIds.includes(id))
    ) {
      return problem(422, 'config.order_mismatch')();
    }

    groupIds.forEach((id, sortOrder) => {
      const listed = active.find((heldGroup) => heldGroup.id === id)!;
      if (listed.sortOrder !== sortOrder) {
        listed.sortOrder = sortOrder;
        listed.rowVersion = nextVersion();
      }
    });
    // In the list's order: Every note first, then the others by SortOrder, the archived ones keeping theirs.
    held.sort((a, b) => Number(b.isEveryNote) - Number(a.isEveryNote) || a.sortOrder - b.sortOrder);
    return reordered();
  });
  for (const heldGroup of held) {
    serveGroup(heldGroup);
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

      // Emptied, then written on the next frame, so each add is said afresh and not by the words the last one left.
      expect(pageStatus()).toBeEmptyDOMElement();
      await wait(16);
      expect(pageStatus()).toHaveTextContent(added);
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

const enterTheGroupName = 'Enter the group name';
const groupNameTooLong = 'Group name must be 200 characters or less';
const groupAdded = 'Group added at the end of the list';

/**
 * An answer that waits until the test gives it, and the way to give it: each request sent meanwhile, the one StrictMode
 * sends again among them, then gets an answer of its own from `given`, whose body it can read.
 */
function answerLater(): { answer: Answer; give: (given: Answer) => void } {
  const waiting: ((given: Answer) => void)[] = [];
  return {
    answer: (init) => new Promise<Answer>((resolve) => waiting.push(resolve)).then((given) => given(init)),
    give: (given) => {
      for (const resolve of waiting.splice(0)) {
        resolve(given);
      }
    },
  };
}

/** The bodies of the group adds sent, in the order sent. */
function groupAdds(): unknown[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    path === groupsPath && init?.method === 'POST' ? [JSON.parse(init.body as string) as unknown] : [],
  );
}

/** The New group field, by its label. */
function newGroupField(): HTMLInputElement {
  return within(main()).getByRole('textbox', { name: 'New group' });
}

/** The New group form. */
function newGroupForm(): HTMLFormElement {
  return newGroupField().closest('form')!;
}

/** Add group, by its name, which its busy label leaves as it is. */
function addGroupButton(): HTMLElement {
  return within(newGroupForm()).getByRole('button', { name: 'Add group' });
}

/** The alert above Add group. */
function newGroupAlert(): HTMLElement {
  return within(newGroupForm()).getByRole('alert');
}

/** The count under the New group field. */
function newGroupCount(): HTMLElement | null {
  return newGroupForm().querySelector('p[aria-hidden="true"]');
}

/** The message on the New group field, without its hidden "Error: ", or `null` while it shows none. */
function newGroupMessage(): string | null {
  const message = newGroupForm().querySelector('[id$="-error"]');
  return message === null ? null : (message.textContent ?? '').replace(/^Error: /, '');
}

describe('Common items, adding a group', () => {
  it('shows the New group form after the last section: its label, input, count, alert, then Add group', async () => {
    await openPage();

    const field = newGroupField();
    expect(field.id).toBe('group-new-name');
    expect(document.querySelector('label[for="group-new-name"]')?.textContent).toBe('New group');
    expect(field.tagName).toBe('INPUT');
    expect(field).toHaveAttribute('type', 'text');
    expect(field).toHaveAttribute('autocomplete', 'off');
    expect(field).toHaveValue('');
    for (const attribute of ['maxlength', 'required', 'aria-invalid']) {
      expect(field).not.toHaveAttribute(attribute);
    }

    // The 48 px input, in the reading column.
    expect(field).toHaveStyle({ minBlockSize: 'var(--target-button)' });
    expect(newGroupForm()).toHaveStyle({ maxInlineSize: 'var(--measure)' });
    // The hidden hint alone: never the help text, which would bury it, nor the count.
    expect(field.getAttribute('aria-describedby')).toBe('group-new-name-hint');
    expect(field).toHaveAccessibleDescription(hint);
    expect(newGroupForm()).toHaveAttribute('novalidate');
    expect(newGroupCount()?.textContent).toBe(hint);
    expect(newGroupAlert()).toBeEmptyDOMElement();
    const button = addGroupButton();
    expect(button).toHaveAttribute('type', 'submit');
    expect(button.className).toMatch(/secondary/);
    expect(button.className).not.toMatch(/primary/);
    // Label width, at least 44 px wide, on a phone and a laptop alike (common-items.md *Conflicts resolved* 17).
    expect(button).toHaveStyle({ inlineSize: 'auto', minInlineSize: 'var(--target-min)' });
    expect(follows(sections().at(-1)!, newGroupForm())).toBe(true);
    expect(sections().every((section) => !section.contains(newGroupForm()))).toBe(true);
    expect(follows(field, newGroupCount()!)).toBe(true);
    expect(follows(newGroupCount()!, newGroupAlert())).toBe(true);
    expect(follows(newGroupAlert(), button)).toBe(true);
  });

  it('shows no New group form, nor Add group, until the groups have come', async () => {
    const list = answerLater();
    serve('GET', listPath, list.answer);
    renderApp('/manage/common-items');

    await wait(5000);
    expect(document.getElementById('group-new-name')).toBeNull();
    expect(within(main()).queryByRole('button', { name: 'Add group' })).not.toBeInTheDocument();
    list.give(json(groups));
    await settle();

    expect(newGroupField()).toBeInTheDocument();
    expect(addGroupButton()).toBeInTheDocument();
  });

  it.each([
    ['nothing typed', '', enterTheGroupName],
    ['only spaces', ' \t  ', enterTheGroupName],
    ['201 characters once its ends are trimmed', `  ${'a'.repeat(201)}  `, groupNameTooLong],
  ])('on Add group with %s, shows its message, focusing the field, and sends nothing', async (_, name, message) => {
    await openPage();
    type(newGroupField(), name);

    await press(addGroupButton());

    expect(newGroupMessage()).toBe(message);
    expect(newGroupField()).toHaveFocus();
    expect(newGroupField()).toHaveAttribute('aria-invalid', 'true');
    expect(newGroupField().getAttribute('aria-describedby')).toBe('group-new-name-error group-new-name-hint');
    expect(newGroupField()).toHaveAccessibleDescription(`Error: ${message} ${hint}`);
    expect(newGroupField()).toHaveValue(name);
    expect(groupAdds()).toEqual([]);
    expect(newGroupAlert()).toBeEmptyDOMElement();
    // A one-field form: no error summary, and nothing in front of the page's title (form-validation.md rule 6).
    expect(screen.queryByRole('heading', { name: 'There is a problem' })).toBeNull();
    expect(document.title).toBe('Grow2Notes – Common items');
  });

  it('once the message shows, checks the name at each input, and takes it away silently once it passes', async () => {
    await openPage();
    await press(addGroupButton());

    type(newGroupField(), 'a'.repeat(201));
    expect(newGroupMessage()).toBe(groupNameTooLong);
    type(newGroupField(), 'Respite');

    expect(newGroupMessage()).toBeNull();
    expect(newGroupField()).not.toHaveAttribute('aria-invalid');
    expect(newGroupField()).toHaveAccessibleDescription(hint);
    expect(newGroupForm().querySelector('[data-invalid]')).toBeNull();
    expect(pageStatus()).toBeEmptyDOMElement();
  });

  it('keeps 210 characters typed whole, counting 10 too many, and refuses them on Add group', async () => {
    await openPage();
    const typed = 'x'.repeat(210);

    type(newGroupField(), typed);
    expect(newGroupField()).toHaveValue(typed);
    expect(newGroupCount()?.textContent).toBe('You have 10 characters too many');
    expect(newGroupMessage()).toBeNull();
    await press(addGroupButton());

    expect(newGroupField()).toHaveValue(typed);
    expect(newGroupMessage()).toBe(groupNameTooLong);
    expect(groupAdds()).toEqual([]);
  });

  it("adds the group last, empty, above New group, emptying the field, focusing its New item field", async () => {
    const router = await openPage();
    serveGroups(groups);
    type(newGroupField(), 'Respite');
    const reads = listReads();

    await press(addGroupButton());

    expect(groupAdds()).toEqual([{ name: 'Respite' }]);
    expect(listReads()).toBe(reads + 1);
    expect(sections().map((section) => within(section).getByRole('heading', { level: 2 }).textContent)).toEqual([
      'Every note',
      'Community outing',
      'Personal care',
      'Respite',
    ]);
    const added = sectionOf('Respite');
    expect(within(added).getByText(emptyLine)).toBeInTheDocument();
    expect(activeItems(added)).toEqual([]);
    expect(follows(added, newGroupForm())).toBe(true);
    expect(newGroupField()).toHaveValue('');
    expect(newItem('Respite')).toHaveFocus();
    expect(newGroupAlert()).toBeEmptyDOMElement();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(groupAdded);
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

  it('adds each group after the last, with focus in its New item field after each', async () => {
    await openPage([{ ...everyNote, items: [] }]);
    serveGroups([{ ...everyNote, items: [] }]);

    for (const name of ['Community outing', 'Personal care']) {
      type(newGroupField(), name);
      await press(addGroupButton());

      expect(newItem(name)).toHaveFocus();
      expect(sections().at(-1)).toBe(sectionOf(name));
    }

    expect(groupAdds()).toEqual([{ name: 'Community outing' }, { name: 'Personal care' }]);
  });

  // The input holds one line, as HTML's value sanitization for a text input strips line breaks, so the server's rule
  // for them (AddCommonItemGroupEndpointTests) has nothing to meet from here.
  it('sends the name with its ends trimmed, and 200 characters once so cleaned', async () => {
    await openPage();
    serveGroups(groups);

    type(newGroupField(), '  Overnight respite\t');
    await press(addGroupButton());
    type(newGroupField(), `  ${'a'.repeat(200)}  `);
    await press(addGroupButton());

    expect(groupAdds()).toEqual([{ name: 'Overnight respite' }, { name: 'a'.repeat(200) }]);
    expect(sectionOf('Overnight respite')).toBeInTheDocument();
    expect(sectionOf('a'.repeat(200))).toBeInTheDocument();
  });

  it('keeps a name typed while the add was out, as it is not the name sent', async () => {
    await openPage();
    const held = serveGroups(groups);
    const add = answerLater();
    serve('POST', groupsPath, add.answer);
    type(newGroupField(), 'Respite');

    await press(addGroupButton());
    type(newGroupField(), 'School holidays');
    held.push(group('6e8a0c2f-7b9d-4ea4-9fc1-000000000001', 'Respite', 2, []));
    add.give(createdWith({ id: held.at(-1)!.id }));
    await settle();
    await settle();

    expect(sectionOf('Respite')).toBeInTheDocument();
    expect(newGroupField()).toHaveValue('School holidays');
  });

  it('shows Add group busy at once, "Adding…" after 400 ms, and a press while it is sends nothing more', async () => {
    await openPage();
    serve('POST', groupsPath, noAnswer);
    type(newGroupField(), 'Respite');
    const button = addGroupButton();

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

    expect(groupAdds()).toHaveLength(1);
    expect(newGroupField()).toHaveValue('Respite');
  });

  it.each([
    ['no connection', noConnection, 'Not added: no connection. Try again.'],
    ['a 503', problem(503), 'Not added: something went wrong. Try again.'],
    ['a 422 that names no name', refused({ isEveryNote: ['Refused'] }), 'Not added: something went wrong. Try again.'],
  ])('on %s, says so above Add group, keeping the name and focus on Add group, and reads nothing', async (
    _,
    answer,
    words,
  ) => {
    await openPage();
    serve('POST', groupsPath, answer);
    type(newGroupField(), 'Respite');
    const button = addGroupButton();
    const reads = listReads();

    await press(button);

    expect(newGroupAlert()).toHaveTextContent(words);
    expect(button).toHaveFocus();
    expect(newGroupField()).toHaveValue('Respite');
    expect(newGroupField()).not.toHaveAttribute('aria-invalid');
    expect(groupAdds()).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(sections()).toHaveLength(3);
    expect(pageStatus()).not.toHaveTextContent(groupAdded);
  });

  // The add has no idempotency key (design.md §6.6), so one sent again after a lost answer could add the group twice.
  it('sends one add for an answer that never comes, saying "no connection" after 10 s, and never again', async () => {
    await openPage();
    serve('POST', groupsPath, noAnswer);
    type(newGroupField(), 'Respite');

    await press(addGroupButton());
    await wait(10_000);
    await settle();

    expect(newGroupAlert()).toHaveTextContent('Not added: no connection. Try again.');
    expect(addGroupButton()).toHaveFocus();
    expect(newGroupField()).toHaveValue('Respite');
    await wait(60_000);
    expect(groupAdds()).toHaveLength(1);
  });

  it("on the server's 422 for the name, shows its message on the field, focused, keeping the name", async () => {
    await openPage();
    serve('POST', groupsPath, refused({ name: [groupNameTooLong] }));
    type(newGroupField(), 'Respite');
    const reads = listReads();

    await press(addGroupButton());

    expect(newGroupMessage()).toBe(groupNameTooLong);
    expect(newGroupField()).toHaveFocus();
    expect(newGroupField()).toHaveAttribute('aria-invalid', 'true');
    expect(newGroupField()).toHaveAccessibleDescription(`Error: ${groupNameTooLong} ${hint}`);
    expect(newGroupField()).toHaveValue('Respite');
    expect(newGroupAlert()).toBeEmptyDOMElement();
    expect(listReads()).toBe(reads);
    expect(groupAdds()).toHaveLength(1);
  });

  it('hands a 401 to sign-in in place, keeping the name, and reads the list again once signed in', async () => {
    await openPage();
    serve('POST', groupsPath, problem(401));
    serve('POST', '/api/auth/passkey/options', json({ challenge: 'Y2hhbGxlbmdl', userVerification: 'required' }));
    serve('POST', '/api/auth/login', json({ next: 'totp' }));
    serve('POST', '/api/auth/login/totp', json(manager));
    type(newGroupField(), 'Respite');
    // Found now, as the page leaves the accessibility tree once the shell's sign-in in place hides it.
    const page = { field: newGroupField(), alert: newGroupAlert() };

    await press(addGroupButton());

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    expect(page.field).not.toBeVisible();
    expect(page.field).toHaveValue('Respite');
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
    expect(newGroupField()).toHaveValue('Respite');
    expect(newGroupAlert()).toBeEmptyDOMElement();
    expect(groupAdds()).toHaveLength(1);
  });
});

const groupRenamed = 'Group renamed';
const notSavedWentWrong = 'Not saved: something went wrong. Try again.';

/** The renames sent to the group of `groupId`, each as its `If-Match` and the JSON of its body, in the order sent. */
function renamesOf(groupId: string): { ifMatch: string | null; body: unknown }[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    path === groupPath(groupId) && init?.method === 'PUT'
      ? [{ ifMatch: new Headers(init.headers).get('If-Match'), body: JSON.parse(init.body as string) as unknown }]
      : [],
  );
}

/** The group button labelled `label` of the group named `name`, by its name, which ends with ", [name] group". */
function groupButton(name: string, label: 'Move up' | 'Move down' | 'Rename' | 'Archive'): HTMLElement {
  return within(sectionOf(name)).getByRole('button', { name: `${label}, ${name} group` });
}

/** The group's Group name field, by the id that the page gives it, or `null` while the group is not being renamed. */
function renameField(groupId: string): HTMLInputElement | null {
  return document.getElementById(`group-${groupId}-name`) as HTMLInputElement | null;
}

/** The group's rename form. */
function renameForm(groupId: string): HTMLFormElement {
  return renameField(groupId)!.closest('form')!;
}

/** The rename's Save, by its name, which its busy label leaves as it is. */
function renameSave(groupId: string): HTMLElement {
  return within(renameForm(groupId)).getByRole('button', { name: 'Save' });
}

function renameCancel(groupId: string): HTMLElement {
  return within(renameForm(groupId)).getByRole('button', { name: 'Cancel' });
}

/** The alert above the rename's Save. */
function renameAlert(groupId: string): HTMLElement {
  return within(renameForm(groupId)).getByRole('alert');
}

/** The message on the group's Group name field, without its hidden "Error: ", or `null` while it shows none. */
function renameMessage(groupId: string): string | null {
  const message = renameForm(groupId).querySelector('[id$="-error"]');
  return message === null ? null : (message.textContent ?? '').replace(/^Error: /, '');
}

/** Focuses the Rename of `renamed` and presses it, and gives the field that takes the group buttons' place. */
function openRename(renamed: AdminCommonItemGroup): HTMLInputElement {
  const button = groupButton(renamed.name, 'Rename');
  button.focus();
  fireEvent.click(button);
  return renameField(renamed.id)!;
}

/** The headings of the sections, in page order. */
function headings(): (string | null)[] {
  return sections().map((section) => within(section).getByRole('heading', { level: 2 }).textContent);
}

describe('Common items, renaming a group', () => {
  it("puts the form under the group's <h2> in its buttons' place: Group name, the name, caret at the end", async () => {
    await openPage();
    const section = sectionOf('Community outing');
    const heading = within(section).getByRole('heading', { level: 2 });

    const field = openRename(outing);

    // The <h2> stays, so the region keeps its name, and its items stay below.
    expect(sectionOf('Community outing')).toBe(section);
    expect(within(section).getByRole('heading', { level: 2 })).toBe(heading);
    expect(within(heading.parentElement!).queryAllByRole('button')).toEqual([]);
    expect(within(heading.parentElement!).queryByRole('alert')).toBeNull();
    expect(follows(heading, renameForm(outing.id))).toBe(true);
    expect(follows(renameForm(outing.id), section.querySelector('ol')!)).toBe(true);
    expect(activeItems(section).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    expect(renameForm(outing.id)).toHaveAttribute('novalidate');
    expect(document.querySelector(`label[for="group-${outing.id}-name"]`)?.textContent).toBe('Group name');
    expect(field).toHaveAccessibleName('Group name');
    expect(field.tagName).toBe('INPUT');
    expect(field).toHaveAttribute('type', 'text');
    expect(field).toHaveAttribute('autocomplete', 'off');
    for (const attribute of ['maxlength', 'required', 'readonly', 'aria-invalid']) {
      expect(field).not.toHaveAttribute(attribute);
    }

    expect(field).toHaveValue(outing.name);
    expect(field).toHaveFocus();
    expect([field.selectionStart, field.selectionEnd]).toEqual([outing.name.length, outing.name.length]);
    // The 48 px input, in the reading column, described by the hidden hint alone, never the help text.
    expect(field).toHaveStyle({ minBlockSize: 'var(--target-button)' });
    expect(renameForm(outing.id)).toHaveStyle({ maxInlineSize: 'var(--measure)' });
    expect(field.getAttribute('aria-describedby')).toBe(`group-${outing.id}-name-hint`);
    expect(field).toHaveAccessibleDescription(hint);
    // The other group keeps its buttons.
    expect(groupButton('Personal care', 'Rename')).toBeInTheDocument();
  });

  it('shows the count, then the empty alert, then Save, primary, and Cancel, both label width', async () => {
    await openPage();

    const field = openRename(outing);

    const count = renameForm(outing.id).querySelector<HTMLElement>('p[aria-hidden="true"]')!;
    expect(count.textContent).toBe(`You have ${200 - outing.name.length} characters remaining`);
    expect(renameAlert(outing.id)).toBeEmptyDOMElement();
    const save = renameSave(outing.id);
    const cancel = renameCancel(outing.id);
    expect(follows(field, count)).toBe(true);
    expect(follows(count, renameAlert(outing.id))).toBe(true);
    expect(follows(renameAlert(outing.id), save)).toBe(true);
    expect(follows(save, cancel)).toBe(true);
    expect(save).toHaveAttribute('type', 'submit');
    expect(save.className).toMatch(/primary/);
    expect(cancel).toHaveAttribute('type', 'button');
    expect(cancel.className).toMatch(/secondary/);
    for (const button of [save, cancel]) {
      expect(button).toHaveStyle({ inlineSize: 'auto', minInlineSize: 'var(--target-min)' });
    }
  });

  it("on Cancel, drops the typed name, sends nothing, and puts focus on the group's Rename", async () => {
    await openPage();
    type(openRename(outing), 'Outings');

    await press(renameCancel(outing.id));

    expect(renamesOf(outing.id)).toEqual([]);
    expect(renameField(outing.id)).toBeNull();
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care']);
    expect(groupButton('Community outing', 'Rename')).toHaveFocus();
    expect(openRename(outing)).toHaveValue(outing.name);
  });

  it('on Save of the name unchanged once its ends are trimmed, closes as Cancel does and sends nothing', async () => {
    await openPage();
    type(openRename(outing), '  Community outing\t');

    await press(renameSave(outing.id));
    await wait(16);

    expect(renamesOf(outing.id)).toEqual([]);
    expect(renameField(outing.id)).toBeNull();
    expect(groupButton('Community outing', 'Rename')).toHaveFocus();
    expect(pageStatus()).toBeEmptyDOMElement();
  });

  it.each([
    ['nothing typed', '', enterTheGroupName],
    ['only spaces', ' \t  ', enterTheGroupName],
    ['201 characters once its ends are trimmed', `  ${'a'.repeat(201)}  `, groupNameTooLong],
  ])('on Save with %s, shows its message above the field, focused, and sends nothing', async (_, name, message) => {
    await openPage();
    const field = openRename(outing);
    type(field, name);

    await press(renameSave(outing.id));

    expect(renamesOf(outing.id)).toEqual([]);
    expect(renameMessage(outing.id)).toBe(message);
    expect(field).toHaveFocus();
    expect(field).toHaveValue(name);
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field.getAttribute('aria-describedby')).toBe(`group-${outing.id}-name-error group-${outing.id}-name-hint`);
    expect(field).toHaveAccessibleDescription(`Error: ${message} ${hint}`);
    expect(follows(within(renameForm(outing.id)).getByText(message), field)).toBe(true);
    expect(renameAlert(outing.id)).toBeEmptyDOMElement();
    // A one-field form: no error summary, and nothing in front of the page's title (form-validation.md rule 6).
    expect(screen.queryByRole('heading', { name: 'There is a problem' })).toBeNull();
    expect(document.title).toBe('Grow2Notes – Common items');
  });

  it('once the message shows, checks the name at each input, and takes it away silently once it passes', async () => {
    await openPage();
    const field = openRename(outing);
    type(field, '');
    await press(renameSave(outing.id));

    type(field, 'a'.repeat(201));
    expect(renameMessage(outing.id)).toBe(groupNameTooLong);
    type(field, 'Outings');

    expect(renameMessage(outing.id)).toBeNull();
    expect(field).not.toHaveAttribute('aria-invalid');
    expect(field).toHaveAccessibleDescription(hint);
    expect(pageStatus()).toBeEmptyDOMElement();
  });

  it("sends the name with the group's version, reads the list again, shows it, focusing Rename", async () => {
    const router = await openPage();
    serveGroups(groups);
    type(openRename(outing), '  Outings and trips ');
    const reads = listReads();

    await press(renameSave(outing.id));

    expect(renamesOf(outing.id)).toEqual([{ ifMatch: `"${outing.rowVersion}"`, body: { name: 'Outings and trips' } }]);
    expect(listReads()).toBe(reads + 1);
    expect(renameField(outing.id)).toBeNull();
    // The heading, the region's name, the group buttons' names and the New item label follow the new name.
    expect(headings()).toEqual(['Every note', 'Outings and trips', 'Personal care']);
    expect(activeItems(sectionOf('Outings and trips')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    expect(newItem('Outings and trips')).toBeInTheDocument();
    expect(groupButton('Outings and trips', 'Rename')).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(groupRenamed);
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
    serve('PUT', groupPath(outing.id), noAnswer);
    const field = openRename(outing);
    type(field, 'Outings');
    const save = renameSave(outing.id);

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

    expect(renamesOf(outing.id)).toHaveLength(1);
    expect(field).toHaveValue('Outings');
  });

  // Every note's refusal and a missing If-Match are the screen's own bugs, as it never offers the one or sends the
  // other (common-items.md *Failures*), so each says something went wrong.
  it.each([
    ['no connection', noConnection, 'Not saved: no connection. Try again.'],
    ['a 503', problem(503), notSavedWentWrong],
    ['a 422 config.every_note_fixed', problem(422, 'config.every_note_fixed'), notSavedWentWrong],
    ['a 428', problem(428, 'precondition.required'), notSavedWentWrong],
    ['a 422 that names no name', refused({ text: ['Refused'] }), notSavedWentWrong],
  ])('on %s, says so above Save, keeping the name and focus on Save, and reads nothing', async (_, answer, words) => {
    await openPage();
    serve('PUT', groupPath(outing.id), answer);
    const field = openRename(outing);
    type(field, 'Outings');
    const save = renameSave(outing.id);
    const reads = listReads();

    await press(save);

    expect(renameAlert(outing.id)).toHaveTextContent(words);
    expect(save).toHaveFocus();
    expect(field).toHaveValue('Outings');
    expect(field).not.toHaveAttribute('readonly');
    expect(field).not.toHaveAttribute('aria-invalid');
    expect(renamesOf(outing.id)).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(listConflict()).toBeNull();
    expect(pageStatus()).not.toHaveTextContent(groupRenamed);
  });

  it("on the server's 422 for the name, shows its message on the field, focused, keeping the name", async () => {
    await openPage();
    serve('PUT', groupPath(outing.id), refused({ name: [groupNameTooLong] }));
    const field = openRename(outing);
    type(field, 'Outings');
    const reads = listReads();

    await press(renameSave(outing.id));

    expect(renameMessage(outing.id)).toBe(groupNameTooLong);
    expect(field).toHaveFocus();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveAccessibleDescription(`Error: ${groupNameTooLong} ${hint}`);
    expect(field).toHaveValue('Outings');
    expect(renameAlert(outing.id)).toBeEmptyDOMElement();
    expect(listReads()).toBe(reads);
    expect(renamesOf(outing.id)).toHaveLength(1);
  });

  it('on a 412, focuses the list conflict over the re-read list, the form as typed, the next Save new', async () => {
    await openPage();
    const held = serveGroups(groups);
    const field = openRename(outing);
    type(field, 'Outings and trips');
    // Another manager renames it meanwhile.
    held[1].name = 'Day trips';
    held[1].rowVersion = 'AAAAAAAAE999=';
    const reads = listReads();

    await press(renameSave(outing.id));

    expect(renamesOf(outing.id)).toEqual([{ ifMatch: `"${outing.rowVersion}"`, body: { name: 'Outings and trips' } }]);
    expect(listReads()).toBe(reads + 1);
    expect(listConflict()).toHaveFocus();
    expect(follows(listConflict()!, sections()[0])).toBe(true);
    expect(headings()).toEqual(['Every note', 'Day trips', 'Personal care']);
    expect(renameField(outing.id)).toBe(field);
    expect(field).toHaveValue('Outings and trips');
    expect(renameAlert(outing.id)).toBeEmptyDOMElement();

    await press(renameSave(outing.id));

    expect(renamesOf(outing.id).map(({ ifMatch }) => ifMatch)).toEqual([`"${outing.rowVersion}"`, '"AAAAAAAAE999="']);
    expect(renameField(outing.id)).toBeNull();
    expect(listConflict()).toBeNull();
    expect(headings()).toEqual(['Every note', 'Outings and trips', 'Personal care']);
    expect(groupButton('Outings and trips', 'Rename')).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(groupRenamed);
  });

  it('on a 412 finding the group archived, focuses the list conflict, the section gone with its form', async () => {
    await openPage();
    const held = serveGroups(groups);
    type(openRename(outing), 'Outings');
    // Another manager archives it meanwhile.
    held[1].archivedAtUtc = '2026-10-01T06:00:00Z';
    held[1].rowVersion = 'AAAAAAAAE999=';

    await press(renameSave(outing.id));

    expect(listConflict()).toHaveFocus();
    expect(renameField(outing.id)).toBeNull();
    expect(headings()).toEqual(['Every note', 'Personal care']);
    expect(renamesOf(outing.id)).toHaveLength(1);
  });

  it("keeps a group's typed name through a write in another group", async () => {
    await openPage();
    serveGroups(groups);
    type(openRename(outing), 'Outings');
    type(openRename(care), 'Personal care and hygiene');

    await press(renameSave(care.id));

    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care and hygiene']);
    expect(renameField(outing.id)).toHaveValue('Outings');
    expect(groupButton('Personal care and hygiene', 'Rename')).toHaveFocus();
    expect(renamesOf(outing.id)).toEqual([]);
  });

  it('hands a 401 on Save to sign-in in place, keeping the name, and reads the list again once signed in', async () => {
    await openPage();
    serve('PUT', groupPath(outing.id), problem(401));
    serve('POST', '/api/auth/passkey/options', json({ challenge: 'Y2hhbGxlbmdl', userVerification: 'required' }));
    serve('POST', '/api/auth/login', json({ next: 'totp' }));
    serve('POST', '/api/auth/login/totp', json(manager));
    const field = openRename(outing);
    type(field, 'Outings');
    // Found now, as the page leaves the accessibility tree once the shell's sign-in in place hides it.
    const alert = renameAlert(outing.id);

    await press(renameSave(outing.id));

    expect(pageHeading()).toHaveAccessibleName('You’ve been signed out');
    expect(field).not.toBeVisible();
    expect(field).toHaveValue('Outings');
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
    expect(renameField(outing.id)).toHaveValue('Outings');
    expect(renameAlert(outing.id)).toBeEmptyDOMElement();
    expect(renamesOf(outing.id)).toHaveLength(1);
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
 * Holds back the answer to each `method` request for `path`, as `serveGroups` gives it, until the test lets it go, as a
 * slow server would; the request is sent at once.
 */
function hold(method: 'POST' | 'PUT', path: string): () => void {
  const answer = answers.get(`${method} ${path}`)!;
  let release: () => void = () => {};
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  serve(method, path, async (init) => {
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
    const release = hold('PUT', orderPath(outing.id));
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
    const release = hold('PUT', orderPath(everyNote.id));
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

/** The group reorders sent, each as its `If-Match` and the JSON of its body, in the order sent. */
function groupReorders(): { ifMatch: string | null; body: unknown }[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    path === groupOrderPath && init?.method === 'PUT'
      ? [{ ifMatch: new Headers(init.headers).get('If-Match'), body: JSON.parse(init.body as string) as unknown }]
      : [],
  );
}

/** The path of each order sent, of the groups or of any group's items, in the order sent. */
function orderPaths(): string[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    typeof path === 'string' && path.endsWith('/order') && init?.method === 'PUT' ? [path] : [],
  );
}

/** The alert in the heading row of the group named `name`, above its buttons. */
function groupAlert(name: string): HTMLElement {
  const heading = within(sectionOf(name)).getByRole('heading', { level: 2 });
  return within(heading.parentElement!).getByRole('alert');
}

// A third group to move, after the archived one, so that a move sends every active group but Every note, the archived
// one left out, and Personal care sits between the ends.
const shopping = group('4c6e8a0d-5f7b-4c93-8eb0-3a4b5c6d7e8f', 'Shopping', 3, []);
const threeGroups = [everyNote, outing, care, respite, shopping];

describe('Common items, moving a group', () => {
  it("on Move down on the first group, sends each group's ID but Every note's, swapped, focus on it", async () => {
    await openPage(threeGroups);
    serveGroups(threeGroups);
    const reads = listReads();

    await press(groupButton('Community outing', 'Move down'));

    // The active groups but Every note, which always stays first, and not the archived one; with no If-Match, as the
    // set of IDs is the check (design.md §6.7); and nothing of any group's items.
    expect(groupReorders()).toEqual([{ ifMatch: null, body: { groupIds: [care.id, outing.id, shopping.id] } }]);
    expect(orderPaths()).toEqual([groupOrderPath]);
    expect(listReads()).toBe(reads + 1);
    expect(headings()).toEqual(['Every note', 'Personal care', 'Community outing', 'Shopping']);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    // Its whole section moved in the DOM, which a focused button does not survive by itself (common-items.md *Group
    // actions*).
    expect(document.activeElement).toBe(groupButton('Community outing', 'Move down'));
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Group moved down');
  });

  it('says "Group moved up" and "Group moved down", focus on the pressed button of the moved group', async () => {
    await openPage(threeGroups);
    serveGroups(threeGroups);
    const announced = recordAnnouncements();

    await press(groupButton('Shopping', 'Move up'));
    await wait(16);
    expect(document.activeElement).toBe(groupButton('Shopping', 'Move up'));
    await press(groupButton('Community outing', 'Move down'));
    await wait(16);

    expect(announced()).toEqual(['Group moved up', 'Group moved down']);
    expect(groupReorders().map(({ body }) => body)).toEqual([
      { groupIds: [outing.id, shopping.id, care.id] },
      { groupIds: [shopping.id, outing.id, care.id] },
    ]);
    expect(headings()).toEqual(['Every note', 'Shopping', 'Community outing', 'Personal care']);
    expect(document.activeElement).toBe(groupButton('Community outing', 'Move down'));
  });

  it('keeps the sections in order while the PUT is out, busy at once and "Moving…" after 400 ms', async () => {
    await openPage(threeGroups);
    serveGroups(threeGroups);
    const release = hold('PUT', groupOrderPath);
    const down = groupButton('Community outing', 'Move down');

    down.focus();
    fireEvent.click(down);
    await settle();
    expect(groupReorders()).toHaveLength(1);
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care', 'Shopping']);
    expect(down).toHaveAttribute('aria-disabled', 'true');
    await wait(398);
    expect(down).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(down).toHaveAttribute('data-busy');
    expect(within(down).getByText('Moving…')).toBeInTheDocument();
    await press(down);
    expect(groupReorders()).toHaveLength(1);
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care', 'Shopping']);

    release();
    await settle();
    await settle();

    expect(headings()).toEqual(['Every note', 'Personal care', 'Community outing', 'Shopping']);
    expect(down).not.toHaveAttribute('aria-disabled');
    expect(down).toHaveFocus();
  });

  it('makes Move up on the first group after Every note and Move down on the last unavailable in place', async () => {
    await openPage(threeGroups);
    const announced = recordAnnouncements();
    const top = groupButton('Community outing', 'Move up');
    const bottom = groupButton('Shopping', 'Move down');

    for (const end of [top, bottom]) {
      expect(end).toHaveAttribute('aria-disabled', 'true');
      expect(end).toHaveAttribute('data-unavailable');
      expect(end).toBeEnabled();
      expect(getComputedStyle(end).display).not.toBe('none');
    }

    for (const free of [
      groupButton('Community outing', 'Move down'),
      groupButton('Personal care', 'Move up'),
      groupButton('Personal care', 'Move down'),
      groupButton('Shopping', 'Move up'),
    ]) {
      expect(free).not.toHaveAttribute('aria-disabled');
    }

    await press(top);
    await wait(16);
    expect(announced()).toEqual(['Already at the top']);
    expect(top).toHaveFocus();
    await press(bottom);
    await wait(16);

    expect(announced()).toEqual(['Already at the top', 'Already at the bottom']);
    expect(bottom).toHaveFocus();
    expect(orderPaths()).toEqual([]);
    expect(queryClient.getMutationCache().getAll()).toEqual([]);
  });

  it('makes both moves of the one group besides Every note unavailable', async () => {
    await openPage([everyNote, outing, respite]);

    expect(groupButton('Community outing', 'Move up')).toHaveAttribute('aria-disabled', 'true');
    expect(groupButton('Community outing', 'Move down')).toHaveAttribute('aria-disabled', 'true');
  });

  it("gives a group's Move up and Move down the item rows' arrows, before their words", async () => {
    await openPage(threeGroups);

    for (const [direction, label] of [
      ['up', 'Move up'],
      ['down', 'Move down'],
    ] as const) {
      const button = groupButton('Personal care', label);
      const arrow = button.querySelector('svg');
      expect(arrow).toHaveAttribute('aria-hidden', 'true');
      const rowArrow = moveButton('Community outing', direction, paid.text).querySelector('svg');
      expect(arrow?.outerHTML).toBe(rowArrow?.outerHTML);
      // First, then the words, in the button's label.
      expect(within(button).getByText(label).firstChild).toBe(arrow);
    }
  });

  it('sends nothing for a queued move whose group the move before it took to the top, and says so', async () => {
    await openPage(threeGroups);
    serveGroups(threeGroups);
    const release = hold('PUT', groupOrderPath);

    await press(groupButton('Community outing', 'Move down'));
    await press(groupButton('Personal care', 'Move up'));
    release();
    await settle();
    await settle();
    await settle();
    await wait(16);

    expect(groupReorders().map(({ body }) => body)).toEqual([{ groupIds: [care.id, outing.id, shopping.id] }]);
    expect(headings()).toEqual(['Every note', 'Personal care', 'Community outing', 'Shopping']);
    expect(groupButton('Personal care', 'Move up')).toHaveFocus();
    expect(pageStatus()).toHaveTextContent('Already at the top');
  });

  it("runs an item move and a group move pressed at once in turn, the group's from the item's re-read", async () => {
    await openPage(threeGroups);
    const held = serveGroups(threeGroups);
    const release = hold('PUT', orderPath(outing.id));
    const reads = listReads();

    await press(moveButton('Community outing', 'up', lunch.text));
    await press(groupButton('Community outing', 'Move down'));
    expect(orderPaths()).toEqual([orderPath(outing.id)]);
    // Another manager archives Shopping meanwhile, which the item move's re-read brings.
    held[4].archivedAtUtc = '2026-10-01T06:00:00Z';
    release();
    await settle();
    await settle();
    await settle();

    expect(orderPaths()).toEqual([orderPath(outing.id), groupOrderPath]);
    expect(reordersOf(outing.id).map(({ body }) => body)).toEqual([
      { commonItemIds: [busOrTrain.id, lunch.id, paid.id] },
    ]);
    expect(groupReorders().map(({ body }) => body)).toEqual([{ groupIds: [care.id, outing.id] }]);
    expect(listReads()).toBe(reads + 2);
    expect(headings()).toEqual(['Every note', 'Personal care', 'Community outing']);
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, lunch.text, paid.text]);
    expect(groupButton('Community outing', 'Move down')).toHaveFocus();
  });

  it('on a 422, reads the list again and focuses the list conflict, until a write succeeds', async () => {
    await openPage(threeGroups);
    const held = serveGroups(threeGroups);
    // Another manager archives Shopping meanwhile.
    held[4].archivedAtUtc = '2026-10-01T06:00:00Z';
    held[4].rowVersion = 'AAAAAAAAE999=';
    const reads = listReads();

    await press(groupButton('Community outing', 'Move down'));

    expect(groupReorders().map(({ body }) => body)).toEqual([{ groupIds: [care.id, outing.id, shopping.id] }]);
    expect(listReads()).toBe(reads + 1);
    expect(listConflict()).toHaveFocus();
    expect(follows(listConflict()!, sections()[0])).toBe(true);
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care']);
    expect(groupAlert('Community outing')).toBeEmptyDOMElement();

    await press(groupButton('Community outing', 'Move down'));

    expect(listConflict()).toBeNull();
    expect(groupReorders().map(({ body }) => body)).toEqual([
      { groupIds: [care.id, outing.id, shopping.id] },
      { groupIds: [care.id, outing.id] },
    ]);
    expect(headings()).toEqual(['Every note', 'Personal care', 'Community outing']);
  });

  it.each([
    ['no connection', noConnection, 'Not moved: no connection. Try again.'],
    ['a 503', problem(503), 'Not moved: something went wrong. Try again.'],
  ])("on %s, says so in the group's alert, keeping the order and focus, reading nothing", async (_, answer, words) => {
    await openPage(threeGroups);
    serve('PUT', groupOrderPath, answer);
    const down = groupButton('Community outing', 'Move down');
    const reads = listReads();

    await press(down);

    expect(groupAlert('Community outing')).toHaveTextContent(words);
    expect(down).toHaveFocus();
    expect(down).not.toHaveAttribute('aria-disabled');
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care', 'Shopping']);
    expect(groupReorders()).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(listConflict()).toBeNull();
    expect(groupAlert('Personal care')).toBeEmptyDOMElement();
  });

  it("empties the group's alert as Rename opens, so that it comes back empty", async () => {
    await openPage(threeGroups);
    serve('PUT', groupOrderPath, noConnection);
    await press(groupButton('Community outing', 'Move down'));
    expect(groupAlert('Community outing')).toHaveTextContent('Not moved: no connection. Try again.');

    openRename(outing);
    fireEvent.click(renameCancel(outing.id));
    await settle();

    expect(groupAlert('Community outing')).toBeEmptyDOMElement();
    expect(groupButton('Community outing', 'Rename')).toHaveFocus();
  });
});

const archived = 'Item archived';
const restored = 'Item restored to the end of the list';

/** The archives or restores sent for the item of `itemId`, each as its `If-Match` and its body, in the order sent. */
function placesOf(itemId: string, action: 'archive' | 'restore'): { ifMatch: string | null; body: unknown }[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    path === placePath(itemId, action) && init?.method === 'POST'
      ? [{ ifMatch: new Headers(init.headers).get('If-Match'), body: init.body }]
      : [],
  );
}

/** The path of each archive and restore sent, of any item, in the order sent. */
function placePaths(): string[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    typeof path === 'string' && /\/(?:archive|restore)$/.test(path) && init?.method === 'POST' ? [path] : [],
  );
}

/** The Archive of the item whose text is `text`, in the group named `name`, by its name, which ends with the text. */
function archiveButton(name: string, text: string): HTMLElement {
  return within(rowOf(name, text)).getByRole('button', { name: `Archive, ${text}` });
}

/** The archived item whose text is `text` in the group named `name`. */
function archivedRowOf(name: string, text: string): HTMLLIElement {
  const row = archivedItems(sectionOf(name)).find((listed) => textOf(listed) === text);
  expect(row).toBeDefined();
  return row!;
}

/** The archived item's Restore, by its name, which ends with the item's text. */
function restoreButton(name: string, text: string): HTMLElement {
  return within(archivedRowOf(name, text)).getByRole('button', { name: `Restore, ${text}` });
}

/** The alert in the archived item whose text is `text` in the group named `name`, above its Restore. */
function archivedRowAlert(name: string, text: string): HTMLElement {
  return within(archivedRowOf(name, text)).getByRole('alert');
}

/** Opens the group's Archived items, as the manager does with its summary. */
function openArchived(name: string) {
  archivedSection(sectionOf(name))!.open = true;
}

// Personal care with two archived items, so that a Restore there leaves its Archived items in place.
const showered = item('1ee6b23a-cf1d-4d2b-9a0a-8cae1e35b4ab', 'Showered', 0, '2026-09-25T05:12:00Z');
const dressed = item('2ff7c34b-d02e-4e3c-8b1b-9dbf2f46c5bc', 'Dressed', 1, '2026-09-26T05:12:00Z');
const careWithArchived = { ...care, items: [showered, dressed] };

describe('Common items, archiving and restoring an item', () => {
  it("on Archive, sends the item's version, then the row goes, the numbers close up, focus on Restore", async () => {
    await openPage();
    serveGroups(groups);
    const reads = listReads();
    expect(archivedSection(sectionOf('Community outing'))).toBeNull();

    await press(archiveButton('Community outing', paid.text));

    expect(placesOf(paid.id, 'archive')).toEqual([{ ifMatch: `"${paid.rowVersion}"`, body: undefined }]);
    expect(placePaths()).toEqual([placePath(paid.id, 'archive')]);
    expect(listReads()).toBe(reads + 1);
    const section = sectionOf('Community outing');
    expect(activeItems(section).map(textOf)).toEqual([busOrTrain.text, lunch.text]);
    // Numbered by the list itself, so the numbers close up, with no number of the row's own.
    expect(section.querySelector('ol')).not.toHaveAttribute('start');
    for (const row of activeItems(section)) {
      expect(row).not.toHaveAttribute('value');
    }

    expect(archivedSection(section)!.open).toBe(true);
    expect(archivedItems(section).map(textOf)).toEqual([paid.text]);
    expect(document.activeElement).toBe(restoreButton('Community outing', paid.text));
    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([medication.text, meal.text]);
    expect(archivedSection(sectionOf('Every note'))!.open).toBe(false);
    await wait(16);
    expect(pageStatus()).toHaveTextContent(archived);
  });

  it("opens the group's closed Archived items with the item just archived first, the newest archived", async () => {
    await openPage();
    serveGroups(groups);
    expect(archivedSection(sectionOf('Every note'))!.open).toBe(false);

    await press(archiveButton('Every note', meal.text));

    const section = sectionOf('Every note');
    expect(activeItems(section).map(textOf)).toEqual([medication.text]);
    expect(archivedSection(section)!.open).toBe(true);
    expect(archivedItems(section).map(textOf)).toEqual([meal.text, bed.text, laundry.text]);
    expect(restoreButton('Every note', meal.text)).toHaveFocus();
  });

  it("shows the group's empty line once its last item is archived, then its add form and Archived items", async () => {
    const list = [everyNote, { ...outing, items: [busOrTrain] }, care];
    await openPage(list);
    serveGroups(list);

    await press(archiveButton('Community outing', busOrTrain.text));

    const section = sectionOf('Community outing');
    expect(activeItems(section)).toEqual([]);
    expect(within(section).getByText(emptyLine).tagName).toBe('P');
    expect(follows(within(section).getByText(emptyLine), newItem('Community outing'))).toBe(true);
    expect(archivedItems(section).map(textOf)).toEqual([busOrTrain.text]);
    expect(archivedSection(section)!.open).toBe(true);
    expect(restoreButton('Community outing', busOrTrain.text)).toHaveFocus();
  });

  it('keeps the row while the archive is out, busy at once, "Archiving…" after 400 ms, sent once', async () => {
    await openPage();
    serveGroups(groups);
    const release = hold('POST', placePath(paid.id, 'archive'));
    const button = archiveButton('Community outing', paid.text);

    button.focus();
    fireEvent.click(button);
    await settle();
    expect(placesOf(paid.id, 'archive')).toHaveLength(1);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    await wait(398);
    expect(button).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(button).toHaveAttribute('data-busy');
    expect(within(button).getByText('Archiving…')).toBeInTheDocument();
    await press(button);
    expect(placesOf(paid.id, 'archive')).toHaveLength(1);

    release();
    await settle();
    await settle();

    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, lunch.text]);
    expect(restoreButton('Community outing', paid.text)).toHaveFocus();
  });

  it("on Restore, sends the item's version, then the item is last in its own group, focus on Archive", async () => {
    await openPage();
    serveGroups(groups);
    openArchived('Every note');
    const reads = listReads();

    await press(restoreButton('Every note', laundry.text));

    expect(placesOf(laundry.id, 'restore')).toEqual([{ ifMatch: `"${laundry.rowVersion}"`, body: undefined }]);
    expect(placePaths()).toEqual([placePath(laundry.id, 'restore')]);
    expect(listReads()).toBe(reads + 1);
    const section = sectionOf('Every note');
    expect(activeItems(section).map(textOf)).toEqual([medication.text, meal.text, laundry.text]);
    expect(archivedItems(section).map(textOf)).toEqual([bed.text]);
    expect(archivedSection(section)!.open).toBe(true);
    expect(document.activeElement).toBe(archiveButton('Every note', laundry.text));
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    await wait(16);
    expect(pageStatus()).toHaveTextContent(restored);
  });

  it('takes the Archived items away once Restore empties it, and the empty line with it', async () => {
    const list = [everyNote, outing, { ...care, items: [showered] }];
    await openPage(list);
    serveGroups(list);
    openArchived('Personal care');

    await press(restoreButton('Personal care', showered.text));

    const section = sectionOf('Personal care');
    expect(archivedSection(section)).toBeNull();
    expect(within(section).queryByText(emptyLine)).not.toBeInTheDocument();
    expect(activeItems(section).map(textOf)).toEqual([showered.text]);
    expect(archiveButton('Personal care', showered.text)).toHaveFocus();
  });

  it('keeps the item archived while the restore is out, busy at once, "Restoring…" after 400 ms', async () => {
    await openPage();
    serveGroups(groups);
    openArchived('Every note');
    const release = hold('POST', placePath(bed.id, 'restore'));
    const button = restoreButton('Every note', bed.text);

    button.focus();
    fireEvent.click(button);
    await settle();
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(archivedItems(sectionOf('Every note')).map(textOf)).toEqual([bed.text, laundry.text]);
    await wait(398);
    expect(button).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(button).toHaveAttribute('data-busy');
    expect(within(button).getByText('Restoring…')).toBeInTheDocument();
    await press(button);
    expect(placesOf(bed.id, 'restore')).toHaveLength(1);

    release();
    await settle();
    await settle();

    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([medication.text, meal.text, bed.text]);
    expect(archiveButton('Every note', bed.text)).toHaveFocus();
  });

  it.each([
    ['Archive', placePath(paid.id, 'archive'), () => archiveButton('Community outing', paid.text)],
    ['Restore', placePath(showered.id, 'restore'), () => restoreButton('Personal care', showered.text)],
  ])('on a 412 from %s, reads the list again and focuses the list conflict, until a write succeeds', async (
    _,
    path,
    button,
  ) => {
    const list = [everyNote, outing, careWithArchived];
    await openPage(list);
    const held = serveGroups(list);
    openArchived('Personal care');
    // Another manager rewords both meanwhile.
    held[1].items[1] = { ...paid, text: 'Paid at the machine', rowVersion: 'AAAAAAAAE998=' };
    held[2].items[0] = { ...showered, text: 'Showered and shaved', rowVersion: 'AAAAAAAAE999=' };
    const reads = listReads();

    await press(button());

    expect(placePaths()).toEqual([path]);
    expect(listReads()).toBe(reads + 1);
    expect(listConflict()).toHaveFocus();
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([
      busOrTrain.text,
      'Paid at the machine',
      lunch.text,
    ]);
    expect(archivedItems(sectionOf('Personal care')).map(textOf)).toEqual([dressed.text, 'Showered and shaved']);
    expect(rowAlert('Community outing', 'Paid at the machine')).toBeEmptyDOMElement();
    expect(archivedRowAlert('Personal care', 'Showered and shaved')).toBeEmptyDOMElement();
    await wait(16);
    expect(pageStatus()).toBeEmptyDOMElement();

    await press(archiveButton('Community outing', 'Paid at the machine'));

    expect(listConflict()).toBeNull();
    expect(archivedItems(sectionOf('Community outing')).map(textOf)).toEqual(['Paid at the machine']);
  });

  it.each([
    ['no connection', noConnection, 'Not archived: no connection. Try again.'],
    ['a 503', problem(503), 'Not archived: something went wrong. Try again.'],
  ])("on %s, says so in the row's alert, keeping the row and focus on Archive, and reads nothing", async (
    _,
    answer,
    words,
  ) => {
    await openPage();
    serve('POST', placePath(paid.id, 'archive'), answer);
    const button = archiveButton('Community outing', paid.text);
    const reads = listReads();

    await press(button);

    expect(rowAlert('Community outing', paid.text)).toHaveTextContent(words);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(activeItems(sectionOf('Community outing')).map(textOf)).toEqual([busOrTrain.text, paid.text, lunch.text]);
    expect(archivedSection(sectionOf('Community outing'))).toBeNull();
    expect(placesOf(paid.id, 'archive')).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(listConflict()).toBeNull();
    expect(pageStatus()).not.toHaveTextContent(archived);
  });

  it.each([
    ['no connection', noConnection, 'Not restored: no connection. Try again.'],
    ['a 503', problem(503), 'Not restored: something went wrong. Try again.'],
  ])("on %s, says so in the archived row's alert, keeping it and focus on Restore, and reads nothing", async (
    _,
    answer,
    words,
  ) => {
    await openPage();
    serve('POST', placePath(bed.id, 'restore'), answer);
    openArchived('Every note');
    const button = restoreButton('Every note', bed.text);
    const reads = listReads();

    await press(button);

    expect(archivedRowAlert('Every note', bed.text)).toHaveTextContent(words);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([medication.text, meal.text]);
    expect(archivedItems(sectionOf('Every note')).map(textOf)).toEqual([bed.text, laundry.text]);
    expect(placesOf(bed.id, 'restore')).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(listConflict()).toBeNull();
    expect(pageStatus()).not.toHaveTextContent(restored);
  });

  // Uncontrolled, so that what the manager opened or closed stays so, whichever group a write is in.
  it('keeps a closed Archived items closed, and an opened one open, after a write in another group', async () => {
    const list = [everyNote, outing, careWithArchived];
    await openPage(list);
    serveGroups(list);
    openArchived('Personal care');

    await press(archiveButton('Community outing', paid.text));

    expect(archivedSection(sectionOf('Community outing'))!.open).toBe(true);
    expect(archivedSection(sectionOf('Every note'))!.open).toBe(false);
    expect(archivedSection(sectionOf('Personal care'))!.open).toBe(true);

    archivedSection(sectionOf('Community outing'))!.open = false;
    await press(restoreButton('Personal care', dressed.text));

    expect(archivedSection(sectionOf('Community outing'))!.open).toBe(false);
    expect(archivedSection(sectionOf('Every note'))!.open).toBe(false);
    expect(archivedSection(sectionOf('Personal care'))!.open).toBe(true);
    expect(archivedItems(sectionOf('Personal care')).map(textOf)).toEqual([showered.text]);

    type(newItem('Every note'), 'Teeth brushed');
    await press(addItemButton('Every note'));

    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([medication.text, meal.text, 'Teeth brushed']);
    expect(archivedSection(sectionOf('Community outing'))!.open).toBe(false);
    expect(archivedSection(sectionOf('Every note'))!.open).toBe(false);
    expect(archivedSection(sectionOf('Personal care'))!.open).toBe(true);
  });
});

const groupArchived = 'Group archived';
const groupRestored = 'Group restored to the end of the list';

/** The archives or restores sent for the group of `groupId`, each as its `If-Match` and its body, in the order sent. */
function groupPlacesOf(groupId: string, action: 'archive' | 'restore'): { ifMatch: string | null; body: unknown }[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    path === groupPlacePath(groupId, action) && init?.method === 'POST'
      ? [{ ifMatch: new Headers(init.headers).get('If-Match'), body: init.body }]
      : [],
  );
}

/** The path of each archive and restore sent, of any group, in the order sent. */
function groupPlacePaths(): string[] {
  return fetchMock.mock.calls.flatMap(([path, init]) =>
    typeof path === 'string'
    && /^\/api\/admin\/common-item-groups\/[^/]+\/(?:archive|restore)$/.test(path)
    && init?.method === 'POST'
      ? [path]
      : [],
  );
}

/** Archived groups, the one `<details>` in no group's section, or `null` while it is left out. */
function archivedGroups(): HTMLDetailsElement | null {
  const found = Array.from(main().querySelectorAll('details')).filter((details) => details.closest('section') === null);
  expect(found.length).toBeLessThanOrEqual(1);
  return found[0] ?? null;
}

/** The rows of Archived groups, in the order shown, or none while it is left out. */
function archivedGroupRows(): HTMLLIElement[] {
  return Array.from(archivedGroups()?.querySelectorAll<HTMLLIElement>('ul > li') ?? []);
}

/** The row of the archived group named `name` in Archived groups. */
function archivedGroupRow(name: string): HTMLLIElement {
  const row = archivedGroupRows().find((listed) => textOf(listed) === name);
  expect(row).toBeDefined();
  return row!;
}

/** The archived group's Restore, by its name, which ends with ", [name] group". */
function groupRestoreButton(name: string): HTMLElement {
  return within(archivedGroupRow(name)).getByRole('button', { name: `Restore, ${name} group` });
}

/** Opens Archived groups, as the manager does with its summary. */
function openArchivedGroups() {
  archivedGroups()!.open = true;
}

// A second archived group, archived after Overnight respite and so listed before it, with an active item and an
// archived one, so that a Restore brings back the one and leaves the other in its own Archived items.
const swimmingKit = item('4bb8c67d-e19f-4f3a-8b2c-0d1e2f3a4b5c', 'Packed swimming kit', 0);
const towel = item('5cc9d78e-f2a0-4a4b-9c3d-1e2f3a4b5c6d', 'Brought a towel', 1, '2026-09-18T05:12:00Z');
const swimming = group(
  '6dd0e89f-a3b1-4b5c-8d4e-2f3a4b5c6d7e',
  'Swimming',
  3,
  [swimmingKit, towel],
  '2026-09-20T05:12:00Z',
);
const twoArchived = [everyNote, outing, care, respite, swimming];

describe('Common items, archiving and restoring a group', () => {
  it('shows Archived groups after the New group form, closed on load, its groups newest archived first', async () => {
    await openPage(twoArchived);

    const details = archivedGroups()!;
    expect(details.open).toBe(false);
    expect(details).not.toHaveAttribute('open');
    expect(details.previousElementSibling).toBe(newGroupForm());
    const summary = details.firstElementChild!;
    expect(summary.tagName).toBe('SUMMARY');
    expect(summary.textContent).toBe('Archived groups');
    expect(details.querySelector('h1, h2, h3, h4, h5, h6')).toBeNull();
    expect(details.querySelector('ul')?.previousElementSibling).toBe(summary);
    expect(archivedGroupRows().map(textOf)).toEqual([swimming.name, respite.name]);
    for (const row of archivedGroupRows()) {
      const name = textOf(row)!;
      expectButtons(row, [`Restore, ${name} group`]);
      const button = within(row).getByRole('button');
      expect(button).toHaveAttribute('type', 'button');
      expect(button).not.toHaveAttribute('aria-label');
      expect(button).toHaveStyle({ inlineSize: 'auto', minInlineSize: 'var(--target-min)' });
      expect(within(button).getByText(`, ${name} group`)).toHaveClass('visually-hidden');
      const alert = within(row).getByRole('alert');
      expect(alert).toBeEmptyDOMElement();
      expect(follows(row.querySelector('p')!, alert)).toBe(true);
      expect(follows(alert, button)).toBe(true);
    }

    // Name and Restore alone: none of an archived group's items is listed.
    for (const text of [packed.text, swimmingKit.text, towel.text]) {
      expect(within(main()).queryByText(text)).not.toBeInTheDocument();
    }
  });

  it('leaves Archived groups out while no group is archived', async () => {
    await openPage([everyNote, outing, care]);

    expect(archivedGroups()).toBeNull();
    expect(within(main()).queryByText('Archived groups')).not.toBeInTheDocument();
  });

  it("on Archive, asks nothing and sends the group's version, then Archived groups opens with it first", async () => {
    await openPage();
    serveGroups(groups);
    const reads = listReads();
    expect(archivedGroups()!.open).toBe(false);

    await press(groupButton('Community outing', 'Archive'));

    expect(groupPlacesOf(outing.id, 'archive')).toEqual([{ ifMatch: `"${outing.rowVersion}"`, body: undefined }]);
    expect(groupPlacePaths()).toEqual([groupPlacePath(outing.id, 'archive')]);
    expect(listReads()).toBe(reads + 1);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    // The section has gone, with its items.
    expect(headings()).toEqual(['Every note', 'Personal care']);
    expect(within(main()).queryByText(busOrTrain.text)).not.toBeInTheDocument();
    expect(archivedGroups()!.open).toBe(true);
    expect(archivedGroupRows().map(textOf)).toEqual([outing.name, respite.name]);
    expect(document.activeElement).toBe(groupRestoreButton(outing.name));
    // The one group left besides Every note, whose moves are both unavailable now.
    expect(groupButton('Personal care', 'Move up')).toHaveAttribute('aria-disabled', 'true');
    expect(groupButton('Personal care', 'Move down')).toHaveAttribute('aria-disabled', 'true');
    await wait(16);
    expect(pageStatus()).toHaveTextContent(groupArchived);
  });

  it('puts Archived groups after New group once the first group is archived, open, focus on its Restore', async () => {
    const list = [everyNote, outing, care];
    await openPage(list);
    serveGroups(list);
    expect(archivedGroups()).toBeNull();

    await press(groupButton('Personal care', 'Archive'));

    const details = archivedGroups()!;
    expect(details.open).toBe(true);
    expect(details.previousElementSibling).toBe(newGroupForm());
    expect(archivedGroupRows().map(textOf)).toEqual([care.name]);
    expect(groupRestoreButton(care.name)).toHaveFocus();
    expect(headings()).toEqual(['Every note', 'Community outing']);
  });

  it('keeps the section while the archive is out, busy at once, "Archiving…" after 400 ms, sent once', async () => {
    await openPage();
    serveGroups(groups);
    const release = hold('POST', groupPlacePath(outing.id, 'archive'));
    const button = groupButton('Community outing', 'Archive');

    button.focus();
    fireEvent.click(button);
    await settle();
    expect(groupPlacesOf(outing.id, 'archive')).toHaveLength(1);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care']);
    await wait(398);
    expect(button).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(button).toHaveAttribute('data-busy');
    expect(within(button).getByText('Archiving…')).toBeInTheDocument();
    await press(button);
    expect(groupPlacesOf(outing.id, 'archive')).toHaveLength(1);

    release();
    await settle();
    await settle();

    expect(headings()).toEqual(['Every note', 'Personal care']);
    expect(groupRestoreButton(outing.name)).toHaveFocus();
  });

  it('opens Archived groups but leaves focus where the manager went while the archive was out', async () => {
    await openPage();
    serveGroups(groups);
    const release = hold('POST', groupPlacePath(outing.id, 'archive'));
    await press(groupButton('Community outing', 'Archive'));
    newGroupField().focus();

    release();
    await settle();
    await settle();

    expect(archivedGroups()!.open).toBe(true);
    expect(archivedGroupRows().map(textOf)).toEqual([outing.name, respite.name]);
    expect(newGroupField()).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(groupArchived);
  });

  it("on Restore, sends the group's version, then it is last among the groups, with its active items", async () => {
    await openPage(twoArchived);
    serveGroups(twoArchived);
    openArchivedGroups();
    const reads = listReads();

    await press(groupRestoreButton(swimming.name));

    expect(groupPlacesOf(swimming.id, 'restore')).toEqual([{ ifMatch: `"${swimming.rowVersion}"`, body: undefined }]);
    expect(groupPlacePaths()).toEqual([groupPlacePath(swimming.id, 'restore')]);
    expect(listReads()).toBe(reads + 1);
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care', 'Swimming']);
    const section = sectionOf('Swimming');
    expect(activeItems(section).map(textOf)).toEqual([swimmingKit.text]);
    expect(archivedItems(section).map(textOf)).toEqual([towel.text]);
    expect(archivedSection(section)!.open).toBe(false);
    expect(document.activeElement).toBe(groupButton('Swimming', 'Archive'));
    // Last of the groups, so its Move down is unavailable, and Personal care's is not any more.
    expect(groupButton('Swimming', 'Move down')).toHaveAttribute('aria-disabled', 'true');
    expect(groupButton('Personal care', 'Move down')).not.toHaveAttribute('aria-disabled');
    expect(archivedGroups()!.open).toBe(true);
    expect(archivedGroupRows().map(textOf)).toEqual([respite.name]);
    await wait(16);
    expect(pageStatus()).toHaveTextContent(groupRestored);
  });

  it('takes Archived groups away once Restore empties it', async () => {
    await openPage();
    serveGroups(groups);
    openArchivedGroups();

    await press(groupRestoreButton(respite.name));

    expect(archivedGroups()).toBeNull();
    expect(within(main()).queryByText('Archived groups')).not.toBeInTheDocument();
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care', 'Overnight respite']);
    expect(activeItems(sectionOf('Overnight respite')).map(textOf)).toEqual([packed.text]);
    expect(groupButton('Overnight respite', 'Archive')).toHaveFocus();
  });

  it('keeps the group archived while the restore is out, busy at once, "Restoring…" after 400 ms', async () => {
    await openPage(twoArchived);
    serveGroups(twoArchived);
    openArchivedGroups();
    const release = hold('POST', groupPlacePath(respite.id, 'restore'));
    const button = groupRestoreButton(respite.name);

    button.focus();
    fireEvent.click(button);
    await settle();
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(archivedGroupRows().map(textOf)).toEqual([swimming.name, respite.name]);
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care']);
    await wait(398);
    expect(button).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(button).toHaveAttribute('data-busy');
    expect(within(button).getByText('Restoring…')).toBeInTheDocument();
    await press(button);
    expect(groupPlacesOf(respite.id, 'restore')).toHaveLength(1);

    release();
    await settle();
    await settle();

    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care', 'Overnight respite']);
    expect(archivedGroupRows().map(textOf)).toEqual([swimming.name]);
    expect(groupButton('Overnight respite', 'Archive')).toHaveFocus();
  });

  it.each([
    ['Archive', groupPlacePath(outing.id, 'archive'), () => groupButton('Community outing', 'Archive')],
    ['Restore', groupPlacePath(respite.id, 'restore'), () => groupRestoreButton('Overnight respite')],
  ])('on a 412 from %s, reads the list again and focuses the list conflict, until a write succeeds', async (
    _,
    path,
    button,
  ) => {
    await openPage();
    const held = serveGroups(groups);
    openArchivedGroups();
    // Another manager renames both meanwhile.
    held[1].name = 'Day trips';
    held[1].rowVersion = 'AAAAAAAAE998=';
    held[3].name = 'Respite weekends';
    held[3].rowVersion = 'AAAAAAAAE999=';
    const reads = listReads();

    await press(button());

    expect(groupPlacePaths()).toEqual([path]);
    expect(listReads()).toBe(reads + 1);
    expect(listConflict()).toHaveFocus();
    expect(follows(listConflict()!, sections()[0])).toBe(true);
    expect(headings()).toEqual(['Every note', 'Day trips', 'Personal care']);
    expect(archivedGroupRows().map(textOf)).toEqual(['Respite weekends']);
    expect(groupAlert('Day trips')).toBeEmptyDOMElement();
    expect(within(archivedGroupRow('Respite weekends')).getByRole('alert')).toBeEmptyDOMElement();
    await wait(16);
    expect(pageStatus()).toBeEmptyDOMElement();

    await press(groupButton('Day trips', 'Archive'));

    expect(listConflict()).toBeNull();
    expect(archivedGroupRows().map(textOf)).toEqual(['Day trips', 'Respite weekends']);
  });

  it('sends nothing for an Archive whose group was archived elsewhere before its turn: the conflict', async () => {
    await openPage();
    const held = serveGroups(groups);
    const release = hold('POST', itemsPath(everyNote.id));
    type(newItem('Every note'), 'Teeth brushed');
    await press(addItemButton('Every note'));
    await press(groupButton('Community outing', 'Archive'));
    // Another manager archives it meanwhile, which the add's re-read brings.
    held[1].archivedAtUtc = '2026-10-01T06:30:00Z';
    held[1].rowVersion = 'AAAAAAAAE999=';

    release();
    await settle();
    await settle();
    await settle();

    expect(groupPlacePaths()).toEqual([]);
    expect(listConflict()).toHaveFocus();
    expect(headings()).toEqual(['Every note', 'Personal care']);
    expect(archivedGroupRows().map(textOf)).toEqual([outing.name, respite.name]);
  });

  it.each([
    ['no connection', noConnection, 'Not archived: no connection. Try again.'],
    ['a 503', problem(503), 'Not archived: something went wrong. Try again.'],
    ['a 428', problem(428, 'precondition.required'), 'Not archived: something went wrong. Try again.'],
    [
      'a 422 config.every_note_fixed',
      problem(422, 'config.every_note_fixed'),
      'Not archived: something went wrong. Try again.',
    ],
  ])("on %s, says so in the group's alert, keeping the section and focus on Archive, and reads nothing", async (
    _,
    answer,
    words,
  ) => {
    await openPage();
    serve('POST', groupPlacePath(outing.id, 'archive'), answer);
    const button = groupButton('Community outing', 'Archive');
    const reads = listReads();

    await press(button);

    expect(groupAlert('Community outing')).toHaveTextContent(words);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care']);
    expect(archivedGroupRows().map(textOf)).toEqual([respite.name]);
    expect(groupPlacesOf(outing.id, 'archive')).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(listConflict()).toBeNull();
    expect(groupAlert('Personal care')).toBeEmptyDOMElement();
    expect(pageStatus()).not.toHaveTextContent(groupArchived);
  });

  it.each([
    ['no connection', noConnection, 'Not restored: no connection. Try again.'],
    ['a 503', problem(503), 'Not restored: something went wrong. Try again.'],
  ])("on %s, says so in the archived group's alert, keeping it and focus on Restore, and reads nothing", async (
    _,
    answer,
    words,
  ) => {
    await openPage(twoArchived);
    serve('POST', groupPlacePath(respite.id, 'restore'), answer);
    openArchivedGroups();
    const button = groupRestoreButton(respite.name);
    const reads = listReads();

    await press(button);

    expect(within(archivedGroupRow(respite.name)).getByRole('alert')).toHaveTextContent(words);
    expect(within(archivedGroupRow(swimming.name)).getByRole('alert')).toBeEmptyDOMElement();
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(headings()).toEqual(['Every note', 'Community outing', 'Personal care']);
    expect(archivedGroupRows().map(textOf)).toEqual([swimming.name, respite.name]);
    expect(groupPlacesOf(respite.id, 'restore')).toHaveLength(1);
    expect(listReads()).toBe(reads);
    expect(listConflict()).toBeNull();
    expect(pageStatus()).not.toHaveTextContent(groupRestored);
  });

  // Uncontrolled, so that what the manager opened or closed stays so, whatever write comes next, but for a group's
  // Archive, which opens it with the group first.
  it('keeps a closed Archived groups closed, and an opened one open, after another write', async () => {
    await openPage(twoArchived);
    serveGroups(twoArchived);

    type(newItem('Every note'), 'Teeth brushed');
    await press(addItemButton('Every note'));

    expect(activeItems(sectionOf('Every note')).map(textOf)).toEqual([medication.text, meal.text, 'Teeth brushed']);
    expect(archivedGroups()!.open).toBe(false);

    openArchivedGroups();
    await press(groupButton('Community outing', 'Move down'));

    expect(headings()).toEqual(['Every note', 'Personal care', 'Community outing']);
    expect(archivedGroups()!.open).toBe(true);

    await press(groupRestoreButton(respite.name));

    expect(archivedGroupRows().map(textOf)).toEqual([swimming.name]);
    expect(archivedGroups()!.open).toBe(true);

    archivedGroups()!.open = false;
    await press(archiveButton('Community outing', paid.text));

    expect(archivedSection(sectionOf('Community outing'))!.open).toBe(true);
    expect(archivedGroups()!.open).toBe(false);

    await press(groupButton('Personal care', 'Archive'));

    expect(archivedGroups()!.open).toBe(true);
    expect(archivedGroupRows().map(textOf)).toEqual([care.name, swimming.name]);
    expect(groupRestoreButton(care.name)).toHaveFocus();
  });
});
