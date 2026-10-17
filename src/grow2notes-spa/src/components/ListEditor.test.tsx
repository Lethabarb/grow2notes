import { QueryClientProvider, queryOptions, type QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/api.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { ListEditor, type ListApi, type ListRow } from './ListEditor.tsx';
import { PageStatus } from './PageStatus.tsx';

const participantId = '3e8b5d1f-9a26-4c70-b4f3-2d6a8e0c1b95';
const queryKey = ['admin', 'goals', participantId];

const help =
  'Changes apply to notes started from now on. Notes already started or submitted keep the wording they were written '
  + 'with.';
const empty = 'No goals yet. Notes will show an empty Goals section.';

/** A made-up goal (A35), active unless it has the time it was archived at. */
function goal(id: string, text: string, sortOrder: number, archivedAtUtc: string | null = null): ListRow {
  return { id, text, sortOrder, archivedAtUtc, rowVersion: `AAAAAAAAB${id}=` };
}

const breakfast = goal('1', 'Makes own breakfast', 0);
const bus = goal('2', 'Catches the bus to day program and phones when she arrives', 1);
const sister = goal('3', 'Phones her sister', 2);
// Archived in this order, and so shown the other way round. The later one is half a second after the earlier, which
// a comparison of the two as text would put first, as "Z" sorts after ".".
const washing = goal('4', 'Uses the washing machine', 3, '2026-10-05T01:00:00Z');
const shop = goal('5', 'Walks to the shop', 4, '2026-10-05T01:00:00.5Z');

type Rendered = {
  client: QueryClient;
  /** The query's function, which reads the list again after each write, and which nothing else may call. */
  queryFn: () => Promise<ListRow[]>;
  /** The adds sent, which nothing answers unless a test gives them an answer. */
  add: (text: string) => Promise<unknown>;
  /** The rewords sent, each with its goal's ID and version, which nothing answers unless a test does. */
  update: (id: string, text: string, rowVersion: string) => Promise<unknown>;
  /** The reorders sent, each with the active goals' IDs in a new order, which nothing answers unless a test does. */
  reorder: (ids: string[]) => Promise<unknown>;
  /** The archives sent, each with its goal's ID and version, which nothing answers unless a test does. */
  archive: (id: string, rowVersion: string) => Promise<unknown>;
  /** The restores sent, each with its goal's ID and version, which nothing answers unless a test does. */
  restore: (id: string, rowVersion: string) => Promise<unknown>;
};

/**
 * Renders the editor for goals in a page's status region, its list in the query already, as its page puts it there,
 * under a query client of its own, beside a button elsewhere on the page; gives the client, the query's function and
 * the writes' functions.
 */
function renderEditor(rows: ListRow[], readOnly = false): Rendered {
  const client = createQueryClient();
  client.setQueryData(queryKey, rows);
  const queryFn = vi.fn<() => Promise<ListRow[]>>();
  const add = vi.fn<(text: string) => Promise<unknown>>();
  const update = vi.fn<(id: string, text: string, rowVersion: string) => Promise<unknown>>();
  const reorder = vi.fn<(ids: string[]) => Promise<unknown>>();
  const archive = vi.fn<(id: string, rowVersion: string) => Promise<unknown>>();
  const restore = vi.fn<(id: string, rowVersion: string) => Promise<unknown>>();
  const api: ListApi = {
    query: queryOptions({ queryKey, queryFn }),
    scopeId: `goals:${participantId}`,
    add,
    update,
    reorder,
    archive,
    restore,
  };
  render(
    <QueryClientProvider client={client}>
      <PageStatus>
        <ListEditor kind="goal" api={api} readOnly={readOnly} />
        <button type="button">Elsewhere</button>
      </PageStatus>
    </QueryClientProvider>,
  );
  return { client, queryFn, add, update, reorder, archive, restore };
}

/** The numbered list of active goals, or `null` while there is none. */
function activeList(): HTMLOListElement | null {
  return document.querySelector('ol');
}

/** Each active goal's list item. */
function activeItems(): HTMLLIElement[] {
  return Array.from(activeList()?.children ?? [], (item) => item as HTMLLIElement);
}

/** Each active goal's text, as its row shows it, the first paragraph of its list item, out of edit mode. */
function activeTexts(): (string | undefined)[] {
  return activeItems().map((item) => item.querySelector('p')?.textContent);
}

/** The archived section, or `null` while it is left out. */
function archivedSection(): HTMLDetailsElement | null {
  return document.querySelector('details');
}

/** Each archived goal's list item, in the order shown. */
function archivedItems(): HTMLLIElement[] {
  return Array.from(archivedSection()?.querySelectorAll<HTMLLIElement>('ul > li') ?? []);
}

/** The archived goals' texts, in the order shown, each its list item's first paragraph. */
function archivedTexts(): (string | undefined)[] {
  return archivedItems().map((item) => item.querySelector('p')?.textContent);
}

describe('ListEditor', () => {
  it('shows the help text, then the active goals as a numbered list, each its text and then its empty alert', () => {
    renderEditor([breakfast, bus, sister]);

    const helpText = screen.getByText(help);
    expect(helpText.tagName).toBe('P');
    expect(helpText.compareDocumentPosition(activeList()!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(activeItems().map((item) => item.tagName)).toEqual(['LI', 'LI', 'LI']);
    for (const [index, item] of activeItems().entries()) {
      const text = within(item).getByText([breakfast, bus, sister][index].text);
      const alert = within(item).getByRole('alert');
      expect(alert).toBeEmptyDOMElement();
      expect(text.compareDocumentPosition(alert) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  // Rows out of SortOrder, as no answer of the API's is, show that the editor keeps the list's order: the API breaks
  // ties in SortOrder as the reorder reads the goals, which the client cannot do again.
  it('keeps the order the list gives, never sorting it', () => {
    renderEditor([{ ...sister, sortOrder: 1 }, { ...breakfast, sortOrder: 0 }, { ...bus, sortOrder: 2 }]);

    expect(activeTexts()).toEqual([sister.text, breakfast.text, bus.text]);
  });

  it("wraps a goal's text anywhere, so that a long word cannot push the page sideways", () => {
    renderEditor([goal('6', 'Wolfeschlegelsteinhausenbergerdorff-and-a-pasted-link', 0)]);

    expect(screen.getByText('Wolfeschlegelsteinhausenbergerdorff-and-a-pasted-link')).toHaveStyle({
      overflowWrap: 'anywhere',
    });
  });

  it('shows the empty sentence in place of the list, and no archived section, when there are no goals', () => {
    renderEditor([]);

    expect(screen.getByText(empty).tagName).toBe('P');
    expect(activeList()).toBeNull();
    expect(archivedSection()).toBeNull();
    expect(screen.getByText(help)).toBeInTheDocument();
  });

  it('shows the empty sentence, then the archived section, when every goal is archived', () => {
    renderEditor([washing, shop]);

    expect(activeList()).toBeNull();
    expect(screen.getByText(empty).compareDocumentPosition(archivedSection()!) & Node.DOCUMENT_POSITION_FOLLOWING)
      .toBeTruthy();
  });

  it('lists the archived goals newest archived first, after the list, in a closed details with a plain summary', () => {
    renderEditor([breakfast, washing, shop, sister]);

    const details = archivedSection()!;
    expect(activeTexts()).toEqual([breakfast.text, sister.text]);
    expect(activeList()!.compareDocumentPosition(details) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(details.open).toBe(false);
    expect(details).not.toHaveAttribute('open');
    const summary = details.querySelector('summary')!;
    expect(summary).toBe(details.firstElementChild);
    expect(summary.textContent).toBe('Archived goals');
    expect(summary.children).toHaveLength(0);
    expect(archivedTexts()).toEqual([shop.text, washing.text]);
  });

  // Uncontrolled, so that React never closes what the person opened.
  it('leaves the archived section as the person left it when the list changes', async () => {
    const { client } = renderEditor([breakfast, washing]);
    archivedSection()!.open = true;

    client.setQueryData(queryKey, [breakfast, washing, shop]);
    // TanStack tells the editor on a timeout of 0.
    await act(() => new Promise((resolve) => setTimeout(resolve, 0)));

    expect(archivedTexts()).toEqual([shop.text, washing.text]);
    expect(archivedSection()!.open).toBe(true);
  });

  it('read-only, shows the goals as numbered text and the archived ones as text, with no help, alert or button', () => {
    renderEditor([breakfast, washing, sister, shop], true);

    expect(screen.queryByText(help)).toBeNull();
    expect(activeTexts()).toEqual([breakfast.text, sister.text]);
    expect(archivedTexts()).toEqual([shop.text, washing.text]);
    expect(screen.queryAllByRole('alert')).toEqual([]);
    expect(screen.getAllByRole('button')).toEqual([screen.getByRole('button', { name: 'Elsewhere' })]);
    expect(screen.queryAllByRole('textbox')).toEqual([]);
    expect(document.querySelector('form')).toBeNull();
  });

  it('read-only, shows the empty sentence when there are no goals', () => {
    renderEditor([], true);

    expect(screen.getByText(empty)).toBeInTheDocument();
    expect(screen.queryByText(help)).toBeNull();
  });

  it('never fetches the list as it mounts, as its page has put it in the query', async () => {
    const { queryFn } = renderEditor([breakfast]);

    await act(() => new Promise((resolve) => setTimeout(resolve, 0)));

    expect(queryFn).not.toHaveBeenCalled();
  });
});

const hint = 'You can enter up to 200 characters';
const enterTheGoal = 'Enter the goal';
const tooLong = 'Goal must be 200 characters or less';
const added = 'Goal added to the end of the list';

function follows(before: Node, after: Node): boolean {
  return (before.compareDocumentPosition(after) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
}

function newGoal(): HTMLTextAreaElement {
  return screen.getByRole('textbox', { name: 'New goal' });
}

function addForm(): HTMLFormElement {
  return newGoal().closest('form')!;
}

/** Add goal, by its name while it is not busy. */
function addButton(): HTMLElement {
  return screen.getByRole('button', { name: 'Add goal' });
}

/** The alert above Add. */
function addAlert(): HTMLElement {
  return within(addForm()).getByRole('alert');
}

/** The page's status region. */
function pageStatus(): HTMLElement {
  return screen.getByRole('status');
}

/** The count under the box, or `null`. */
function shownCount(): HTMLElement | null {
  return addForm().querySelector('p[aria-hidden="true"]');
}

/** The count's visually hidden twin, which screen readers hear. */
function countTwin(): HTMLElement {
  return addForm().querySelector<HTMLElement>('[aria-live="polite"]')!;
}

/** The box's message, without its hidden "Error: ", or `null` while it shows none. */
function fieldMessage(): string | null {
  const message = addForm().querySelector('[id$="-error"]');
  return message === null ? null : (message.textContent ?? '').replace(/^Error: /, '');
}

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** Types `text` in New goal, replacing what was there. */
function type(text: string) {
  fireEvent.change(newGoal(), { target: { value: text } });
}

/** Clicks `element` with focus on it first, as a keyboard or a pointer does, and lets what it starts run. */
async function press(element: HTMLElement) {
  element.focus();
  fireEvent.click(element);
  await wait(1);
}

/**
 * Has the writes and the list's reads answered by a server that holds the list, which it gives back, so that a test
 * can change it as someone else would: an add puts a goal at its end; a reword sent with the goal's version stores the
 * text with a new version, and one sent with another is refused `412`; a reorder of the active goals, each once, puts
 * them in that order, then the archived ones, each moved goal with a new version, and any other is refused `422`; an
 * archive sent with the goal's version gives it, in its place, the time it was archived at, a minute after the last
 * one, from 6 October 2026, after the made-up goals', and a new version, and a restore sent with it puts it at the end
 * of the list, one after the highest active `sortOrder`, active, with a new version, either refused `412` with another
 * version; and a read gives what it holds.
 */
function serveList({ add, update, reorder, archive, restore, queryFn }: Rendered, rows: ListRow[]): ListRow[] {
  const list = [...rows];
  let versions = 0;
  vi.mocked(add).mockImplementation((text) => {
    list.push(goal(`added-${list.length}`, text, list.length));
    return Promise.resolve(undefined);
  });
  vi.mocked(update).mockImplementation((id, text, rowVersion) => {
    const index = list.findIndex((row) => row.id === id);
    if (list[index].rowVersion !== rowVersion) {
      return Promise.reject(new ApiError(412, 'precondition.failed'));
    }

    versions += 1;
    list[index] = { ...list[index], text, rowVersion: `AAAAAAAAD${versions}=` };
    return Promise.resolve(undefined);
  });
  vi.mocked(reorder).mockImplementation((ids) => {
    const active = list.filter((row) => row.archivedAtUtc === null);
    const archived = list.filter((row) => row.archivedAtUtc !== null);
    const eachOnce = new Set(ids).size === ids.length && ids.length === active.length;
    if (!eachOnce || active.some((row) => !ids.includes(row.id))) {
      return Promise.reject(new ApiError(422, 'config.order_mismatch'));
    }

    const reordered = ids.map((id, sortOrder) => {
      const row = active.find((listed) => listed.id === id)!;
      if (row.sortOrder === sortOrder) {
        return row;
      }

      versions += 1;
      return { ...row, sortOrder, rowVersion: `AAAAAAAAD${versions}=` };
    });
    list.splice(0, list.length, ...reordered, ...archived);
    return Promise.resolve(undefined);
  });
  vi.mocked(archive).mockImplementation((id, rowVersion) => {
    const index = list.findIndex((row) => row.id === id);
    if (list[index].rowVersion !== rowVersion) {
      return Promise.reject(new ApiError(412, 'precondition.failed'));
    }

    versions += 1;
    list[index] = {
      ...list[index],
      archivedAtUtc: new Date(Date.UTC(2026, 9, 6) + versions * 60_000).toISOString(),
      rowVersion: `AAAAAAAAD${versions}=`,
    };
    return Promise.resolve(undefined);
  });
  vi.mocked(restore).mockImplementation((id, rowVersion) => {
    const index = list.findIndex((row) => row.id === id);
    if (list[index].rowVersion !== rowVersion) {
      return Promise.reject(new ApiError(412, 'precondition.failed'));
    }

    const end = Math.max(-1, ...list.flatMap((row) => (row.archivedAtUtc === null ? [row.sortOrder] : []))) + 1;
    versions += 1;
    const [restored] = list.splice(index, 1);
    list.push({ ...restored, sortOrder: end, archivedAtUtc: null, rowVersion: `AAAAAAAAD${versions}=` });
    return Promise.resolve(undefined);
  });
  vi.mocked(queryFn).mockImplementation(() => Promise.resolve([...list]));
  return list;
}

/** A promise that the test settles when it chooses. */
function held<T>(): { promise: Promise<T>; resolve: (value: T) => void } {
  let resolve: (value: T) => void = () => {};
  const promise = new Promise<T>((settle) => {
    resolve = settle;
  });
  return { promise, resolve };
}

describe('ListEditor, adding', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    document.title = 'Grow2Notes – Participant';
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the add form after the list: the label, the box, the count, the empty alert, then Add goal', () => {
    renderEditor([breakfast, washing]);

    const form = addForm();
    expect(follows(activeList()!, form)).toBe(true);
    expect(follows(form, archivedSection()!)).toBe(true);
    expect(form).toHaveAttribute('novalidate');
    const field = newGoal();
    expect(field.tagName).toBe('TEXTAREA');
    expect(field).toHaveAttribute('rows', '3');
    expect(field).toHaveValue('');
    for (const attribute of ['maxlength', 'name', 'aria-invalid', 'data-over-limit']) {
      expect(field).not.toHaveAttribute(attribute);
    }
    expect(shownCount()?.textContent).toBe(hint);
    expect(follows(field, shownCount()!)).toBe(true);
    expect(addAlert()).toBeEmptyDOMElement();
    expect(follows(shownCount()!, addAlert())).toBe(true);
    expect(follows(addAlert(), addButton())).toBe(true);
    expect(addButton()).toHaveAttribute('type', 'submit');
    expect(addButton().className).toMatch(/primary/);
  });

  it('is there with no goals, after the empty sentence', () => {
    renderEditor([]);

    expect(follows(screen.getByText(empty), addForm())).toBe(true);
  });

  it('describes the box by the hidden hint, then the help text, never by the count', () => {
    renderEditor([breakfast]);

    expect(newGoal()).toHaveAccessibleDescription(`${hint} ${help}`);
    const [hintId] = newGoal().getAttribute('aria-describedby')!.split(' ');
    const hintText = document.getElementById(hintId);
    expect(hintText).toHaveClass('visually-hidden');
    expect(hintText).not.toBe(shownCount());
  });

  it('counts the text as Add would send it: the limit while empty, then what is left, then how many too many', () => {
    renderEditor([]);

    type('  Makes own\n\n  breakfast  ');
    expect(shownCount()?.textContent).toBe('You have 181 characters remaining');
    expect(shownCount()).toHaveAttribute('aria-hidden', 'true');
    expect(shownCount()).not.toHaveAttribute('data-over-limit');
    type('a'.repeat(199));
    expect(shownCount()?.textContent).toBe('You have 1 character remaining');
    type('a'.repeat(200));
    expect(shownCount()?.textContent).toBe('You have 0 characters remaining');
    expect(newGoal()).not.toHaveAttribute('data-over-limit');

    // Past the limit, bold in the error colour, the box's border too, but no error before Add is pressed.
    type('a'.repeat(203));
    expect(shownCount()?.textContent).toBe('You have 3 characters too many');
    expect(shownCount()).toHaveAttribute('data-over-limit');
    expect(newGoal()).toHaveAttribute('data-over-limit');
    expect(newGoal()).not.toHaveAttribute('aria-invalid');
    expect(fieldMessage()).toBeNull();

    type(' \n ');
    expect(shownCount()?.textContent).toBe(hint);
  });

  it('says the count to screen readers once typing has stopped for 1 s, never at each keystroke', async () => {
    renderEditor([]);
    // In the form from its first render, empty, so that a screen reader hears what is written into it.
    expect(countTwin()).toBeEmptyDOMElement();
    expect(countTwin()).toHaveClass('visually-hidden');
    expect(countTwin()).not.toHaveAttribute('aria-hidden');

    type('Makes own');
    await wait(999);
    expect(countTwin()).toBeEmptyDOMElement();
    type('Makes own breakfast');
    await wait(999);
    expect(countTwin()).toBeEmptyDOMElement();
    await wait(1);
    expect(countTwin().textContent).toBe('You have 181 characters remaining');
  });

  it('on Add with nothing typed, shows "Enter the goal" above the box and focuses it, sending nothing', async () => {
    const { add } = renderEditor([breakfast]);

    await press(addButton());

    expect(add).not.toHaveBeenCalled();
    const field = newGoal();
    expect(field).toHaveFocus();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveAccessibleDescription(`Error: ${enterTheGoal} ${hint} ${help}`);
    const message = within(addForm()).getByText(enterTheGoal);
    expect(message.textContent).toBe(`Error: ${enterTheGoal}`);
    expect(within(message).getByText('Error:')).toHaveClass('visually-hidden');
    expect(follows(message, field)).toBe(true);
    expect(addForm().querySelector('[data-invalid]')).toContainElement(field);
    // A one-field form: no error summary, and nothing in front of the page's title (form-validation.md rule 6).
    expect(screen.queryByRole('heading', { name: 'There is a problem' })).toBeNull();
    expect(document.title).toBe('Grow2Notes – Participant');
    expect(addAlert()).toBeEmptyDOMElement();
  });

  it.each([
    ['only spaces and line breaks', ' \n\t \n ', enterTheGoal],
    ['201 characters once its line break is a space', `${'a'.repeat(100)}\n${'b'.repeat(100)}`, tooLong],
  ])('on Add with %s, shows its message and sends nothing', async (_, text, message) => {
    const { add } = renderEditor([]);
    type(text);

    await press(addButton());

    expect(add).not.toHaveBeenCalled();
    expect(fieldMessage()).toBe(message);
    expect(newGoal()).toHaveFocus();
    expect(newGoal()).toHaveValue(text);
  });

  it('focuses the box again at each Add while its text is refused, even with the same message', async () => {
    renderEditor([]);
    await press(addButton());

    await press(addButton());

    expect(newGoal()).toHaveFocus();
    expect(fieldMessage()).toBe(enterTheGoal);
  });

  it('once the message shows, checks the text at each input, and takes it away silently once it passes', async () => {
    renderEditor([]);
    await press(addButton());

    type('a'.repeat(201));
    expect(fieldMessage()).toBe(tooLong);
    expect(newGoal()).toHaveAttribute('aria-invalid', 'true');
    type('Makes own breakfast');

    expect(fieldMessage()).toBeNull();
    expect(newGoal()).not.toHaveAttribute('aria-invalid');
    expect(newGoal()).toHaveAccessibleDescription(`${hint} ${help}`);
    expect(addForm().querySelector('[data-invalid]')).toBeNull();
    expect(addAlert()).toBeEmptyDOMElement();
    expect(pageStatus()).toBeEmptyDOMElement();
  });

  it('sends the text on one line with its ends trimmed, reads the list again, then empties the box', async () => {
    const rendered = renderEditor([breakfast]);
    serveList(rendered, [breakfast]);
    type(' Catches the bus\n  to day program ');

    await press(addButton());

    expect(rendered.add).toHaveBeenCalledExactlyOnceWith('Catches the bus to day program');
    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(activeTexts()).toEqual([breakfast.text, 'Catches the bus to day program']);
    expect(newGoal()).toHaveValue('');
    expect(newGoal()).toHaveFocus();
    expect(shownCount()?.textContent).toBe(hint);
    expect(addAlert()).toBeEmptyDOMElement();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(added);
  });

  it('says nothing of the count as Add empties the box, calling off what typing just before would say', async () => {
    const rendered = renderEditor([]);
    serveList(rendered, []);
    type('Makes own breakfast');
    await wait(1000);
    expect(countTwin().textContent).toBe('You have 181 characters remaining');

    type('Phones her sister');
    await press(addButton());
    await wait(60_000);

    expect(countTwin().textContent).toBe('You have 181 characters remaining');
  });

  it('adds five goals one after another, with focus in the box after each (design.md §14 M1)', async () => {
    const rendered = renderEditor([]);
    serveList(rendered, []);
    const texts = [
      'Makes own breakfast',
      'Catches the bus to day program and phones when she arrives',
      'Phones her sister',
      'Walks to the shop',
      'Waters the garden',
    ];

    for (const [index, text] of texts.entries()) {
      type(text);
      await press(addButton());

      expect(newGoal()).toHaveFocus();
      expect(newGoal()).toHaveValue('');
      expect(activeTexts()).toEqual(texts.slice(0, index + 1));
    }

    expect(vi.mocked(rendered.add).mock.calls).toEqual(texts.map((text) => [text]));
  });

  it('keeps a 250-character paste whole: the count says 50 too many, and Add shows the error', async () => {
    const { add } = renderEditor([]);
    const pasted = 'x'.repeat(250);

    fireEvent.paste(newGoal());
    type(pasted);
    expect(newGoal()).toHaveValue(pasted);
    expect(shownCount()?.textContent).toBe('You have 50 characters too many');
    await press(addButton());

    expect(add).not.toHaveBeenCalled();
    expect(newGoal()).toHaveValue(pasted);
    expect(fieldMessage()).toBe(tooLong);
    expect(newGoal()).toHaveFocus();
  });

  it('shows Add busy, "Adding…" after 400 ms, and a press while it is sends nothing more', async () => {
    const rendered = renderEditor([]);
    vi.mocked(rendered.add).mockReturnValue(held<undefined>().promise);
    type('Makes own breakfast');
    const button = addButton();

    await press(button);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    await wait(398);
    expect(button).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(button).toHaveAttribute('data-busy');
    expect(within(button).getByText('Adding…')).toBeInTheDocument();
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Adding…');
    await press(button);

    expect(rendered.add).toHaveBeenCalledOnce();
    expect(newGoal()).toHaveValue('Makes own breakfast');
  });

  it('stays busy until the list has been read again, and only then shows the goal and empties the box', async () => {
    const rendered = renderEditor([]);
    const read = held<ListRow[]>();
    vi.mocked(rendered.add).mockResolvedValue(undefined);
    vi.mocked(rendered.queryFn).mockReturnValue(read.promise);
    type('Makes own breakfast');
    const button = addButton();

    await press(button);
    expect(rendered.add).toHaveBeenCalledOnce();
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(newGoal()).toHaveValue('Makes own breakfast');
    expect(activeList()).toBeNull();
    read.resolve([goal('6', 'Makes own breakfast', 0)]);
    await wait(1);

    expect(button).not.toHaveAttribute('aria-disabled');
    expect(activeTexts()).toEqual(['Makes own breakfast']);
    expect(newGoal()).toHaveValue('');
  });

  it('keeps what was typed while the add was out', async () => {
    const rendered = renderEditor([]);
    const answer = held<undefined>();
    vi.mocked(rendered.add).mockReturnValue(answer.promise);
    vi.mocked(rendered.queryFn).mockResolvedValue([goal('6', 'Makes own breakfast', 0)]);
    type('Makes own breakfast');
    await press(addButton());

    type('Phones her sister');
    answer.resolve(undefined);
    await wait(1);

    expect(activeTexts()).toEqual(['Makes own breakfast']);
    expect(newGoal()).toHaveValue('Phones her sister');
  });

  it('leaves focus where the person has gone while the add was out', async () => {
    const rendered = renderEditor([]);
    const answer = held<undefined>();
    vi.mocked(rendered.add).mockReturnValue(answer.promise);
    vi.mocked(rendered.queryFn).mockResolvedValue([goal('6', 'Makes own breakfast', 0)]);
    type('Makes own breakfast');
    await press(addButton());

    const elsewhere = screen.getByRole('button', { name: 'Elsewhere' });
    elsewhere.focus();
    answer.resolve(undefined);
    await wait(1);

    expect(newGoal()).toHaveValue('');
    expect(elsewhere).toHaveFocus();
  });

  it.each([
    ['no connection, its answer lost', new ApiError(0), 'Not added: no connection. Try again.'],
    ['a server error', new ApiError(500), 'Not added: something went wrong. Try again.'],
    [
      'a refusal that names no field of the form',
      new ApiError(422, 'validation.failed', { participantId: ['Unknown'] }),
      'Not added: something went wrong. Try again.',
    ],
  ])('on %s, sends the add once, says so above Add, and keeps the text and focus on Add', async (_, failure, words) => {
    const rendered = renderEditor([breakfast]);
    vi.mocked(rendered.add).mockRejectedValue(failure);
    type('Makes own breakfast');
    const button = addButton();

    await press(button);
    await wait(60_000);

    expect(rendered.add).toHaveBeenCalledOnce();
    expect(rendered.queryFn).not.toHaveBeenCalled();
    expect(addAlert()).toHaveTextContent(words);
    expect(button).toHaveFocus();
    expect(newGoal()).toHaveValue('Makes own breakfast');
    expect(newGoal()).not.toHaveAttribute('aria-invalid');
    expect(pageStatus()).not.toHaveTextContent(added);
  });

  it('empties the alert at the next press, so that a failure that repeats is announced again', async () => {
    const rendered = renderEditor([]);
    vi.mocked(rendered.add).mockRejectedValueOnce(new ApiError(0)).mockReturnValueOnce(held<undefined>().promise);
    type('Makes own breakfast');
    await press(addButton());
    expect(addAlert()).toHaveTextContent('Not added: no connection. Try again.');

    await press(addButton());

    expect(addAlert()).toBeEmptyDOMElement();
  });

  it("shows the server's refusal of the text on the box, as Add's own check does, focusing it", async () => {
    const rendered = renderEditor([]);
    vi.mocked(rendered.add).mockRejectedValue(new ApiError(422, 'validation.failed', { text: [tooLong] }));
    type('Makes own breakfast');

    await press(addButton());

    expect(newGoal()).toHaveFocus();
    expect(newGoal()).toHaveAttribute('aria-invalid', 'true');
    expect(newGoal()).toHaveAccessibleDescription(`Error: ${tooLong} ${hint} ${help}`);
    expect(newGoal()).toHaveValue('Makes own breakfast');
    expect(addAlert()).toBeEmptyDOMElement();
  });

  it('shows nothing for a 401, which the session flow has', async () => {
    const rendered = renderEditor([]);
    vi.mocked(rendered.add).mockRejectedValue(new ApiError(401));
    type('Makes own breakfast');

    await press(addButton());

    expect(addAlert()).toBeEmptyDOMElement();
    expect(fieldMessage()).toBeNull();
    expect(newGoal()).toHaveValue('Makes own breakfast');
  });

  it("sends each write in the list's one scope, with no retry, and keys nothing by a goal's words", async () => {
    const rendered = renderEditor([]);
    serveList(rendered, []);
    type('Makes own breakfast');

    await press(addButton());

    const [write] = rendered.client.getMutationCache().getAll();
    expect(write.options.scope).toEqual({ id: `goals:${participantId}` });
    expect(write.options.retry).toBe(0);
    expect(write.options.mutationKey).toBeUndefined();
    expect(rendered.client.getQueryCache().getAll().map((query) => query.queryKey)).toEqual([queryKey]);
  });
});

const editLabel = 'Edit goal';
const saved = 'Goal saved';
const listChanged = 'Someone else changed this list just now. Check it, then try again.';
const changedWhileEditing =
  'Someone else changed this goal while you were editing. It now says: Makes own toast. Save to replace it with your '
  + 'wording, or Cancel to keep it.';

/** The active goal's list item whose text, or whose edit form's box, is `text`. */
function rowOf(text: string): HTMLLIElement {
  return activeItems().find(
    (item) => item.querySelector('p')?.textContent === text || item.querySelector('textarea')?.value === text,
  )!;
}

/** The row's Edit, by its name, which ends with the row's text. */
function editButton(text: string): HTMLElement {
  return screen.getByRole('button', { name: `Edit, ${text}` });
}

/** The row's edit form's box, or `null` while the row is not in edit mode. */
function editBox(item: HTMLElement): HTMLTextAreaElement | null {
  return within(item).queryByRole('textbox', { name: editLabel });
}

/** The row's Save, by its name while it is not busy. */
function saveButton(item: HTMLElement): HTMLElement {
  return within(item).getByRole('button', { name: 'Save' });
}

function cancelButton(item: HTMLElement): HTMLElement {
  return within(item).getByRole('button', { name: 'Cancel' });
}

/** The edit form's message on its box, without its hidden "Error: ", or `null` while it shows none. */
function editMessage(item: HTMLElement): string | null {
  const message = item.querySelector('form [id$="-error"]');
  return message === null ? null : (message.textContent ?? '').replace(/^Error: /, '');
}

/** The list conflict above the list, or `null` while it is not shown. */
function conflict(): HTMLElement | null {
  return screen.queryByText(listChanged);
}

/** Opens the row's edit form with Edit, and gives its box. */
async function openEdit(text: string): Promise<HTMLTextAreaElement> {
  const item = rowOf(text);
  await press(editButton(text));
  return editBox(item)!;
}

/** Types `text` in the box, replacing what was there. */
function retype(box: HTMLTextAreaElement, text: string) {
  fireEvent.change(box, { target: { value: text } });
}

describe('ListEditor, rewording', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    document.title = 'Grow2Notes – Participant';
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("gives each active row Edit after its alert, a label-width secondary button, its text hidden after it", () => {
    renderEditor([breakfast, bus, washing]);

    for (const row of [breakfast, bus]) {
      const button = editButton(row.text);
      const item = rowOf(row.text);
      expect(item).toContainElement(button);
      expect(button.tagName).toBe('BUTTON');
      expect(button).toHaveAttribute('type', 'button');
      expect(button).not.toHaveAttribute('aria-label');
      expect(button).not.toHaveAttribute('aria-expanded');
      expect(button.className).toMatch(/secondary/);
      expect(button.className).toMatch(/rowButton/);
      expect(button.textContent).toBe(`Edit, ${row.text}`);
      expect(within(button).getByText(`, ${row.text}`)).toHaveClass('visually-hidden');
      expect(follows(within(item).getByRole('alert'), button)).toBe(true);
    }
    expect(within(archivedSection()!).queryByRole('button', { name: /^Edit/ })).toBeNull();
  });

  it('turns the row into a form in place, still numbered: the box holds the text, focused, caret at its end', () => {
    renderEditor([breakfast, bus]);

    fireEvent.click(editButton(bus.text));

    const item = activeItems()[1];
    const box = editBox(item)!;
    expect(activeItems()).toHaveLength(2);
    expect(box.closest('form')).toHaveAttribute('novalidate');
    expect(box.closest('li')).toBe(item);
    expect(box.tagName).toBe('TEXTAREA');
    expect(box).toHaveAttribute('rows', '3');
    expect(box).not.toHaveAttribute('maxlength');
    expect(box).not.toHaveAttribute('readonly');
    expect(box).toHaveValue(bus.text);
    expect(box).toHaveFocus();
    expect([box.selectionStart, box.selectionEnd]).toEqual([bus.text.length, bus.text.length]);
    expect(within(item).queryByRole('button', { name: /^Edit/ })).toBeNull();
    expect(within(item).queryByText(bus.text, { selector: 'p' })).toBeNull();
    expect(editButton(breakfast.text)).toBeInTheDocument();
  });

  it('describes the box as New goal is, and shows the count, then the empty alert, then Save and Cancel', async () => {
    renderEditor([breakfast, bus]);

    const box = await openEdit(bus.text);

    const item = rowOf(bus.text);
    expect(box).toHaveAccessibleDescription(`${hint} ${help}`);
    const count = item.querySelector<HTMLElement>('p[aria-hidden="true"]')!;
    expect(count.textContent).toBe('You have 142 characters remaining');
    const alert = within(item).getByRole('alert');
    expect(alert).toBeEmptyDOMElement();
    expect(follows(box, count)).toBe(true);
    expect(follows(count, alert)).toBe(true);
    const save = saveButton(item);
    const cancel = cancelButton(item);
    expect(follows(alert, save)).toBe(true);
    expect(follows(save, cancel)).toBe(true);
    expect(save).toHaveAttribute('type', 'submit');
    expect(save.className).toMatch(/primary/);
    expect(save.className).toMatch(/rowButton/);
    expect(cancel).toHaveAttribute('type', 'button');
    expect(cancel.className).toMatch(/secondary/);
    expect(cancel.className).toMatch(/rowButton/);
  });

  it("on Cancel, drops what was typed, sends nothing, and puts focus on the row's Edit", async () => {
    const { update } = renderEditor([breakfast, bus]);
    retype(await openEdit(breakfast.text), 'Makes own lunch');

    await press(cancelButton(rowOf('Makes own lunch')));

    expect(update).not.toHaveBeenCalled();
    expect(activeTexts()).toEqual([breakfast.text, bus.text]);
    expect(editButton(breakfast.text)).toHaveFocus();
    expect(await openEdit(breakfast.text)).toHaveValue(breakfast.text);
  });

  it('on Save of the text unchanged once cleaned, closes as Cancel does and sends nothing', async () => {
    const { update } = renderEditor([breakfast, bus]);
    retype(await openEdit(breakfast.text), '  Makes own\n  breakfast ');

    await press(saveButton(rowOf('  Makes own\n  breakfast ')));
    await wait(16);

    expect(update).not.toHaveBeenCalled();
    expect(editBox(rowOf(breakfast.text))).toBeNull();
    expect(editButton(breakfast.text)).toHaveFocus();
    expect(pageStatus()).toBeEmptyDOMElement();
  });

  it("sends the text on one line with the row's version, reads the list again, shows it, focusing Edit", async () => {
    const rendered = renderEditor([breakfast, bus]);
    serveList(rendered, [breakfast, bus]);
    retype(await openEdit(breakfast.text), ' Makes own\n  lunch ');

    await press(saveButton(rowOf(' Makes own\n  lunch ')));

    expect(rendered.update).toHaveBeenCalledExactlyOnceWith(breakfast.id, 'Makes own lunch', breakfast.rowVersion);
    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(activeTexts()).toEqual(['Makes own lunch', bus.text]);
    expect(editBox(rowOf('Makes own lunch'))).toBeNull();
    expect(editButton('Makes own lunch')).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(saved);
  });

  it('shows Save busy, "Saving…" after 400 ms, the box read-only; a press while it is sends nothing more', async () => {
    const rendered = renderEditor([breakfast]);
    const answer = held<undefined>();
    vi.mocked(rendered.update).mockReturnValue(answer.promise);
    vi.mocked(rendered.queryFn).mockResolvedValue([{ ...breakfast, text: 'Makes own lunch' }]);
    const box = await openEdit(breakfast.text);
    retype(box, 'Makes own lunch');
    const save = saveButton(rowOf('Makes own lunch'));

    await press(save);
    expect(save).toHaveAttribute('aria-disabled', 'true');
    expect(box).toHaveAttribute('readonly');
    await wait(398);
    expect(save).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(save).toHaveAttribute('data-busy');
    expect(within(save).getByText('Saving…')).toBeInTheDocument();
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Saving…');
    await press(save);
    expect(rendered.update).toHaveBeenCalledOnce();

    answer.resolve(undefined);
    await wait(1);

    expect(editButton('Makes own lunch')).toHaveFocus();
  });

  it.each([
    ['only spaces and line breaks', ' \n\t \n ', enterTheGoal],
    ['over 200 characters', 'a'.repeat(201), tooLong],
    ['201 characters once its line break is a space', `${'a'.repeat(100)}\n${'b'.repeat(100)}`, tooLong],
  ])('on Save with %s, shows its message above the box and focuses it, sending nothing', async (_, text, message) => {
    const { update } = renderEditor([breakfast]);
    const box = await openEdit(breakfast.text);
    retype(box, text);
    const item = rowOf(text);

    await press(saveButton(item));

    expect(update).not.toHaveBeenCalled();
    expect(editMessage(item)).toBe(message);
    expect(box).toHaveFocus();
    expect(box).toHaveValue(text);
    expect(box).toHaveAttribute('aria-invalid', 'true');
    expect(box).toHaveAccessibleDescription(`Error: ${message} ${hint} ${help}`);
    expect(follows(within(item).getByText(message), box)).toBe(true);
    expect(item.querySelector('[data-invalid]')).toContainElement(box);
    expect(screen.queryByRole('heading', { name: 'There is a problem' })).toBeNull();
    expect(document.title).toBe('Grow2Notes – Participant');
    expect(within(item).getByRole('alert')).toBeEmptyDOMElement();
  });

  it('once the message shows, checks the text at each input, and takes it away silently once it passes', async () => {
    renderEditor([breakfast]);
    const box = await openEdit(breakfast.text);
    retype(box, '   ');
    const item = rowOf('   ');
    await press(saveButton(item));

    retype(box, 'a'.repeat(201));
    expect(editMessage(item)).toBe(tooLong);
    retype(box, 'Makes own lunch');

    expect(editMessage(item)).toBeNull();
    expect(box).not.toHaveAttribute('aria-invalid');
    expect(box).toHaveAccessibleDescription(`${hint} ${help}`);
    expect(item.querySelector('[data-invalid]')).toBeNull();
    expect(within(item).getByRole('alert')).toBeEmptyDOMElement();
    expect(pageStatus()).toBeEmptyDOMElement();
  });

  it("shows the server's refusal of the text on the box, as Save's own check does, focusing it", async () => {
    const rendered = renderEditor([breakfast]);
    vi.mocked(rendered.update).mockRejectedValue(new ApiError(422, 'validation.failed', { text: [tooLong] }));
    const box = await openEdit(breakfast.text);
    retype(box, 'Makes own lunch');
    const item = rowOf('Makes own lunch');

    await press(saveButton(item));

    expect(box).toHaveFocus();
    expect(box).toHaveAttribute('aria-invalid', 'true');
    expect(box).toHaveAccessibleDescription(`Error: ${tooLong} ${hint} ${help}`);
    expect(box).toHaveValue('Makes own lunch');
    expect(box).not.toHaveAttribute('readonly');
    expect(within(item).getByRole('alert')).toBeEmptyDOMElement();
    expect(rendered.queryFn).not.toHaveBeenCalled();
  });

  it.each([
    ['no connection, its answer lost', new ApiError(0), 'Not saved: no connection. Try again.'],
    ['a server error', new ApiError(500), 'Not saved: something went wrong. Try again.'],
  ])('on %s, sends the reword once, says so above Save, the text and focus kept', async (_, failure, words) => {
    const rendered = renderEditor([breakfast]);
    vi.mocked(rendered.update).mockRejectedValue(failure);
    const box = await openEdit(breakfast.text);
    retype(box, 'Makes own lunch');
    const item = rowOf('Makes own lunch');
    const save = saveButton(item);

    await press(save);
    await wait(60_000);

    expect(rendered.update).toHaveBeenCalledOnce();
    expect(rendered.queryFn).not.toHaveBeenCalled();
    const alert = within(item).getByRole('alert');
    expect(alert).toHaveTextContent(words);
    expect(follows(alert, save)).toBe(true);
    expect(save).toHaveFocus();
    expect(box).toHaveValue('Makes own lunch');
    expect(box).not.toHaveAttribute('readonly');
    expect(conflict()).toBeNull();
    expect(pageStatus()).not.toHaveTextContent(saved);
  });

  it('shows nothing for a 401, which the session flow has, keeping the text', async () => {
    const rendered = renderEditor([breakfast]);
    vi.mocked(rendered.update).mockRejectedValue(new ApiError(401));
    const box = await openEdit(breakfast.text);
    retype(box, 'Makes own lunch');
    const item = rowOf('Makes own lunch');

    await press(saveButton(item));

    expect(within(item).getByRole('alert')).toBeEmptyDOMElement();
    expect(editMessage(item)).toBeNull();
    expect(box).toHaveValue('Makes own lunch');
  });

  it('on a 412 finding the goal active, keeps the text, says what it says now, saves at the next Save', async () => {
    const rendered = renderEditor([breakfast, bus]);
    const server = serveList(rendered, [breakfast, bus]);
    const box = await openEdit(breakfast.text);
    retype(box, 'Makes own lunch');
    const item = rowOf('Makes own lunch');
    const save = saveButton(item);
    // Another manager rewords it meanwhile.
    server[0] = { ...breakfast, text: 'Makes own toast', rowVersion: 'AAAAAAAAC1E=' };

    await press(save);

    expect(rendered.update).toHaveBeenLastCalledWith(breakfast.id, 'Makes own lunch', breakfast.rowVersion);
    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(within(item).getByRole('alert')).toHaveTextContent(changedWhileEditing);
    expect(box).toHaveValue('Makes own lunch');
    expect(save).toHaveFocus();
    expect(conflict()).toBeNull();

    await press(save);

    expect(rendered.update).toHaveBeenLastCalledWith(breakfast.id, 'Makes own lunch', 'AAAAAAAAC1E=');
    expect(activeTexts()).toEqual(['Makes own lunch', bus.text]);
    expect(editButton('Makes own lunch')).toHaveFocus();
  });

  it('on a 412 finding the goal archived, shows the list conflict, focused, until a write succeeds', async () => {
    const rendered = renderEditor([breakfast, bus]);
    const server = serveList(rendered, [breakfast, bus]);
    retype(await openEdit(breakfast.text), 'Makes own lunch');
    // Another manager archives it meanwhile.
    server[0] = { ...breakfast, archivedAtUtc: '2026-10-05T01:00:00Z', rowVersion: 'AAAAAAAAC1E=' };

    await press(saveButton(rowOf('Makes own lunch')));

    const shown = conflict()!;
    expect(shown.tagName).toBe('P');
    expect(shown).toHaveAttribute('tabindex', '-1');
    expect(shown).toHaveFocus();
    expect(follows(screen.getByText(help), shown)).toBe(true);
    expect(follows(shown, activeList()!)).toBe(true);
    expect(activeTexts()).toEqual([bus.text]);
    expect(archivedTexts()).toEqual([breakfast.text]);
    expect(screen.queryByRole('textbox', { name: editLabel })).toBeNull();

    // A write that fails leaves it; the next that succeeds takes it away.
    vi.mocked(rendered.add).mockRejectedValueOnce(new ApiError(0));
    type('Phones her sister');
    await press(addButton());
    expect(conflict()).toBe(shown);
    await press(addButton());
    expect(conflict()).toBeNull();
  });

  it('keeps two rows in edit mode at once, each as typed, while the other rows move', async () => {
    const { client } = renderEditor([breakfast, bus, sister]);
    retype(await openEdit(breakfast.text), 'Makes own lunch');
    retype(await openEdit(sister.text), 'Phones her brother');

    // As a read of the list after someone else reordered it.
    client.setQueryData(queryKey, [sister, bus, breakfast]);
    await wait(1);

    const [first, second, third] = activeItems();
    expect(editBox(first)).toHaveValue('Phones her brother');
    expect(second.querySelector('p')?.textContent).toBe(bus.text);
    expect(editBox(third)).toHaveValue('Makes own lunch');
  });

  it("leaves focus in another row's box that the person went to while Save was out, else focuses Edit", async () => {
    const rendered = renderEditor([breakfast, sister]);
    serveList(rendered, [breakfast, sister]);
    const answer = held<undefined>();
    const serve = vi.mocked(rendered.update).getMockImplementation()!;
    vi.mocked(rendered.update).mockImplementationOnce(async (...sent) => {
      await answer.promise;
      return serve(...sent);
    });
    retype(await openEdit(breakfast.text), 'Makes own lunch');
    const other = await openEdit(sister.text);
    retype(other, 'Phones her brother');
    await press(saveButton(rowOf('Makes own lunch')));

    other.focus();
    answer.resolve(undefined);
    await wait(1);

    expect(editBox(rowOf('Makes own lunch'))).toBeNull();
    expect(other).toHaveFocus();
    await wait(16);
    expect(pageStatus()).toHaveTextContent(saved);

    await press(saveButton(rowOf('Phones her brother')));

    expect(editButton('Phones her brother')).toHaveFocus();
  });

  it('sends the version the list holds as the write runs, read again by the write before it', async () => {
    const rendered = renderEditor([breakfast]);
    const answer = held<undefined>();
    vi.mocked(rendered.add).mockReturnValue(answer.promise);
    vi.mocked(rendered.update).mockResolvedValue(undefined);
    vi.mocked(rendered.queryFn).mockResolvedValue([
      { ...breakfast, rowVersion: 'AAAAAAAAC1E=' },
      goal('6', 'Phones her sister', 1),
    ]);
    type('Phones her sister');
    await press(addButton());
    retype(await openEdit(breakfast.text), 'Makes own lunch');

    await press(saveButton(rowOf('Makes own lunch')));
    expect(rendered.update).not.toHaveBeenCalled();
    answer.resolve(undefined);
    await wait(1);

    expect(rendered.update).toHaveBeenCalledExactlyOnceWith(breakfast.id, 'Makes own lunch', 'AAAAAAAAC1E=');
  });

  it('sends nothing for a Save whose goal the write before it found archived, showing the list conflict', async () => {
    const rendered = renderEditor([breakfast]);
    const answer = held<undefined>();
    vi.mocked(rendered.add).mockReturnValue(answer.promise);
    vi.mocked(rendered.queryFn).mockResolvedValue([
      { ...breakfast, archivedAtUtc: '2026-10-05T01:00:00Z', rowVersion: 'AAAAAAAAC1E=' },
      goal('6', 'Phones her sister', 1),
    ]);
    type('Phones her sister');
    await press(addButton());
    retype(await openEdit(breakfast.text), 'Makes own lunch');
    await press(saveButton(rowOf('Makes own lunch')));

    answer.resolve(undefined);
    await wait(1);

    expect(rendered.update).not.toHaveBeenCalled();
    expect(conflict()).toHaveFocus();
    expect(activeTexts()).toEqual(['Phones her sister']);
  });

  it("sends each reword in the list's one scope, with no retry", async () => {
    const rendered = renderEditor([breakfast]);
    serveList(rendered, [breakfast]);
    retype(await openEdit(breakfast.text), 'Makes own lunch');

    await press(saveButton(rowOf('Makes own lunch')));

    const [write] = rendered.client.getMutationCache().getAll();
    expect(write.options.scope).toEqual({ id: `goals:${participantId}` });
    expect(write.options.retry).toBe(0);
    expect(rendered.client.getQueryCache().getAll().map((query) => query.queryKey)).toEqual([queryKey]);
  });
});

/** The row's Move up or Move down, by its name, which ends with the row's text. */
function moveButton(direction: 'up' | 'down', text: string): HTMLElement {
  return screen.getByRole('button', { name: `Move ${direction}, ${text}` });
}

/** The alert slot of the active goal's row whose text is `text`. */
function rowAlert(text: string): HTMLElement {
  return within(rowOf(text)).getByRole('alert');
}

/** The row's Archive, by its name while it is not busy, which ends with the row's text. */
function archiveButton(text: string): HTMLElement {
  return screen.getByRole('button', { name: `Archive, ${text}` });
}

/** Records each text written into the page's status region from now on, leaving out its emptying. */
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

describe('ListEditor, moving', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('gives each row Move up and Move down, then Edit and Archive, after its alert, its text hidden after each', () => {
    renderEditor([breakfast, bus, sister]);

    for (const row of [breakfast, bus, sister]) {
      const item = rowOf(row.text);
      const up = moveButton('up', row.text);
      const down = moveButton('down', row.text);
      const edit = editButton(row.text);
      const archive = archiveButton(row.text);
      expect(within(item).getAllByRole('button')).toEqual([up, down, edit, archive]);
      expect(up.parentElement).toBe(down.parentElement);
      expect(edit.parentElement).toBe(archive.parentElement);
      expect(edit.parentElement).not.toBe(up.parentElement);
      expect(edit.parentElement?.parentElement).toBe(up.parentElement?.parentElement);
      expect(follows(within(item).getByRole('alert'), up)).toBe(true);
      for (const button of [up, down]) {
        expect(button.tagName).toBe('BUTTON');
        expect(button).toHaveAttribute('type', 'button');
        expect(button.className).toMatch(/secondary/);
        expect(button.className).toMatch(/rowButton/);
        expect(button.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
        expect(within(button).getByText(`, ${row.text}`)).toHaveClass('visually-hidden');
      }
    }
  });

  it('keeps Move up on the first row and Move down on the last in place, focusable but unavailable and dashed', () => {
    renderEditor([breakfast, bus, sister]);

    const ends = [moveButton('up', breakfast.text), moveButton('down', sister.text)];
    for (const button of ends) {
      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(button).toHaveAttribute('data-unavailable');
      expect(button).toBeEnabled();
      expect(getComputedStyle(button).display).not.toBe('none');
    }
    const others = [moveButton('down', breakfast.text), moveButton('up', bus.text), moveButton('down', bus.text)];
    for (const button of [...others, moveButton('up', sister.text)]) {
      expect(button).not.toHaveAttribute('aria-disabled');
    }
  });

  it('makes both unavailable on the one row of a list of one', () => {
    renderEditor([breakfast, washing]);

    expect(moveButton('up', breakfast.text)).toHaveAttribute('aria-disabled', 'true');
    expect(moveButton('down', breakfast.text)).toHaveAttribute('aria-disabled', 'true');
  });

  it('on a press of an unavailable one, sends nothing and says why, once, focus staying on it', async () => {
    const rendered = renderEditor([breakfast, bus]);
    const announced = recordAnnouncements();

    await press(moveButton('up', breakfast.text));
    await wait(16);
    expect(announced()).toEqual(['Already at the top']);
    expect(moveButton('up', breakfast.text)).toHaveFocus();
    await press(moveButton('down', bus.text));
    await wait(16);

    expect(announced()).toEqual(['Already at the top', 'Already at the bottom']);
    expect(rendered.reorder).not.toHaveBeenCalled();
    expect(rendered.client.getMutationCache().getAll()).toEqual([]);
    expect(activeTexts()).toEqual([breakfast.text, bus.text]);
  });

  // Move down on row 1 is the case in which React moves the focused button's row (list-editor.md).
  it('on Move down on row 1, sends the order with it second, shows it read again, focus on its Move down', async () => {
    const rendered = renderEditor([breakfast, bus, sister]);
    serveList(rendered, [breakfast, bus, sister]);

    await press(moveButton('down', breakfast.text));

    expect(rendered.reorder).toHaveBeenCalledExactlyOnceWith([bus.id, breakfast.id, sister.id]);
    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(activeTexts()).toEqual([bus.text, breakfast.text, sister.text]);
    expect(document.activeElement).toBe(moveButton('down', breakfast.text));
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Moved down to number 2 of 3');
  });

  // As Safari leaves it after a click, which does not focus the button clicked.
  it("puts focus on the moved goal's button when the press left it on the page", async () => {
    const rendered = renderEditor([breakfast, bus, sister]);
    serveList(rendered, [breakfast, bus, sister]);

    fireEvent.click(moveButton('down', breakfast.text));
    await wait(1);

    expect(activeTexts()).toEqual([bus.text, breakfast.text, sister.text]);
    expect(document.activeElement).toBe(moveButton('down', breakfast.text));
  });

  it('leaves focus where the person has gone while the move was out', async () => {
    const rendered = renderEditor([breakfast, bus, sister]);
    serveList(rendered, [breakfast, bus, sister]);
    const answer = held<undefined>();
    const serve = vi.mocked(rendered.reorder).getMockImplementation()!;
    vi.mocked(rendered.reorder).mockImplementationOnce(async (ids) => {
      await answer.promise;
      return serve(ids);
    });
    await press(moveButton('down', breakfast.text));

    const elsewhere = screen.getByRole('button', { name: 'Elsewhere' });
    elsewhere.focus();
    answer.resolve(undefined);
    await wait(1);

    expect(activeTexts()).toEqual([bus.text, breakfast.text, sister.text]);
    expect(elsewhere).toHaveFocus();
  });

  it('on Move up, sends the order with it a place higher, focus on its Move up, saying where it went', async () => {
    const rendered = renderEditor([breakfast, bus, sister]);
    serveList(rendered, [breakfast, bus, sister]);

    await press(moveButton('up', sister.text));

    expect(rendered.reorder).toHaveBeenCalledExactlyOnceWith([breakfast.id, sister.id, bus.id]);
    expect(activeTexts()).toEqual([breakfast.text, sister.text, bus.text]);
    expect(document.activeElement).toBe(moveButton('up', sister.text));
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Moved up to number 2 of 3');
  });

  it('keeps the order on screen while the reorder is out, busy at once and "Moving…" after 400 ms', async () => {
    const rendered = renderEditor([breakfast, bus, sister]);
    const answer = held<undefined>();
    vi.mocked(rendered.reorder).mockReturnValue(answer.promise);
    vi.mocked(rendered.queryFn).mockResolvedValue([
      { ...bus, sortOrder: 0 },
      { ...breakfast, sortOrder: 1 },
      sister,
    ]);
    const down = moveButton('down', breakfast.text);

    await press(down);
    expect(activeTexts()).toEqual([breakfast.text, bus.text, sister.text]);
    expect(down).toHaveAttribute('aria-disabled', 'true');
    await wait(398);
    expect(down).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(down).toHaveAttribute('data-busy');
    expect(within(down).getByText('Moving…')).toBeInTheDocument();
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Moving…');
    await press(down);
    expect(rendered.reorder).toHaveBeenCalledOnce();
    expect(activeTexts()).toEqual([breakfast.text, bus.text, sister.text]);

    answer.resolve(undefined);
    await wait(1);

    expect(activeTexts()).toEqual([bus.text, breakfast.text, sister.text]);
    expect(down).not.toHaveAttribute('aria-disabled');
    expect(down).toHaveFocus();
  });

  it("runs two quick presses on different rows one after the other, the second from the first's re-read", async () => {
    const rendered = renderEditor([breakfast, bus, sister]);
    serveList(rendered, [breakfast, bus, sister]);
    const answer = held<undefined>();
    const serve = vi.mocked(rendered.reorder).getMockImplementation()!;
    vi.mocked(rendered.reorder).mockImplementationOnce(async (ids) => {
      await answer.promise;
      return serve(ids);
    });

    await press(moveButton('down', breakfast.text));
    await press(moveButton('up', sister.text));
    expect(rendered.reorder).toHaveBeenCalledOnce();
    expect(moveButton('up', sister.text)).toHaveAttribute('aria-disabled', 'true');
    answer.resolve(undefined);
    await wait(1);

    expect(vi.mocked(rendered.reorder).mock.calls).toEqual([
      [[bus.id, breakfast.id, sister.id]],
      [[bus.id, sister.id, breakfast.id]],
    ]);
    expect(rendered.queryFn).toHaveBeenCalledTimes(2);
    expect(activeTexts()).toEqual([bus.text, sister.text, breakfast.text]);
    expect(moveButton('up', sister.text)).toHaveFocus();
  });

  it('sends nothing for a move whose goal the move before it took to the end, saying so after it', async () => {
    const rendered = renderEditor([breakfast, bus]);
    serveList(rendered, [breakfast, bus]);
    const answer = held<undefined>();
    const serve = vi.mocked(rendered.reorder).getMockImplementation()!;
    vi.mocked(rendered.reorder).mockImplementationOnce(async (ids) => {
      await answer.promise;
      return serve(ids);
    });
    await press(moveButton('down', breakfast.text));
    await press(moveButton('up', bus.text));
    const announced = recordAnnouncements();

    answer.resolve(undefined);
    await wait(1);
    await wait(16);

    expect(rendered.reorder).toHaveBeenCalledOnce();
    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(activeTexts()).toEqual([bus.text, breakfast.text]);
    expect(announced()).toEqual(['Moved down to number 2 of 2', 'Already at the top']);
    const up = moveButton('up', bus.text);
    expect(up).toHaveFocus();
    expect(up).toHaveAttribute('data-unavailable');
  });

  it('sends nothing for a move whose goal the write before it found archived, showing the list conflict', async () => {
    const rendered = renderEditor([breakfast, bus]);
    const answer = held<undefined>();
    vi.mocked(rendered.add).mockReturnValue(answer.promise);
    vi.mocked(rendered.queryFn).mockResolvedValue([
      { ...breakfast, archivedAtUtc: '2026-10-05T01:00:00Z', rowVersion: 'AAAAAAAAC1E=' },
      bus,
      goal('6', 'Phones her sister', 2),
    ]);
    type('Phones her sister');
    await press(addButton());
    await press(moveButton('down', breakfast.text));

    answer.resolve(undefined);
    await wait(1);

    expect(rendered.reorder).not.toHaveBeenCalled();
    expect(conflict()).toHaveFocus();
    expect(activeTexts()).toEqual([bus.text, 'Phones her sister']);
  });

  it('on a 422, shows the list read again and the list conflict, focused, until a write succeeds', async () => {
    const rendered = renderEditor([breakfast, bus, sister]);
    const server = serveList(rendered, [breakfast, bus, sister]);
    // Another manager archives a goal meanwhile.
    server[2] = { ...sister, archivedAtUtc: '2026-10-05T01:00:00Z', rowVersion: 'AAAAAAAAC3E=' };

    await press(moveButton('down', breakfast.text));

    expect(rendered.reorder).toHaveBeenCalledExactlyOnceWith([bus.id, breakfast.id, sister.id]);
    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(conflict()).toHaveFocus();
    expect(activeTexts()).toEqual([breakfast.text, bus.text]);
    expect(archivedTexts()).toEqual([sister.text]);
    expect(rowAlert(breakfast.text)).toBeEmptyDOMElement();

    await press(moveButton('down', breakfast.text));

    expect(conflict()).toBeNull();
    expect(activeTexts()).toEqual([bus.text, breakfast.text]);
  });

  it("on no connection or a server failure, says so in the row's alert, keeping the order and focus", async () => {
    const rendered = renderEditor([breakfast, bus]);
    let fail: (failure: ApiError) => void = () => {};
    const failing = new Promise((_, reject) => {
      fail = reject;
    });
    vi.mocked(rendered.reorder).mockRejectedValueOnce(new ApiError(0)).mockReturnValueOnce(failing);
    const down = moveButton('down', breakfast.text);

    await press(down);
    expect(rowAlert(breakfast.text)).toHaveTextContent('Not moved: no connection. Try again.');
    expect(down).toHaveFocus();
    expect(down).not.toHaveAttribute('aria-disabled');
    expect(activeTexts()).toEqual([breakfast.text, bus.text]);
    expect(rendered.queryFn).not.toHaveBeenCalled();
    expect(conflict()).toBeNull();

    // Emptied at the next press, so that a failure that repeats is announced again.
    await press(down);
    expect(rowAlert(breakfast.text)).toBeEmptyDOMElement();
    fail(new ApiError(500));
    await wait(1);

    expect(rowAlert(breakfast.text)).toHaveTextContent('Not moved: something went wrong. Try again.');
    expect(down).toHaveFocus();
  });

  it("empties the row's alert as Edit opens, so that Cancel or Save brings back no stale failure", async () => {
    const rendered = renderEditor([breakfast, bus]);
    serveList(rendered, [breakfast, bus]);
    vi.mocked(rendered.reorder).mockRejectedValueOnce(new ApiError(0)).mockRejectedValueOnce(new ApiError(0));
    const notMoved = 'Not moved: no connection. Try again.';

    await press(moveButton('down', breakfast.text));
    expect(rowAlert(breakfast.text)).toHaveTextContent(notMoved);
    await openEdit(breakfast.text);
    await press(cancelButton(rowOf(breakfast.text)));

    expect(rowAlert(breakfast.text)).toBeEmptyDOMElement();

    await press(moveButton('down', breakfast.text));
    expect(rowAlert(breakfast.text)).toHaveTextContent(notMoved);
    retype(await openEdit(breakfast.text), 'Makes own lunch');
    await press(saveButton(rowOf('Makes own lunch')));
    await wait(16);

    expect(pageStatus()).toHaveTextContent(saved);
    expect(rowAlert('Makes own lunch')).toBeEmptyDOMElement();
  });

  it('shows nothing for a 401, which the session flow has', async () => {
    const rendered = renderEditor([breakfast, bus]);
    vi.mocked(rendered.reorder).mockRejectedValue(new ApiError(401));

    await press(moveButton('down', breakfast.text));

    expect(rowAlert(breakfast.text)).toBeEmptyDOMElement();
    expect(conflict()).toBeNull();
  });

  it('has no drag, hides no end button and gives no row button an aria-label', () => {
    renderEditor([breakfast, bus, washing, sister]);

    expect(document.querySelectorAll('[draggable]')).toHaveLength(0);
    for (const button of [moveButton('up', breakfast.text), moveButton('down', sister.text)]) {
      expect(getComputedStyle(button).display).not.toBe('none');
    }
    const rowButtons = [
      ...within(activeList()!).getAllByRole('button'),
      ...within(archivedSection()!).getAllByRole('button'),
    ];
    expect(rowButtons).toHaveLength(13);
    for (const button of rowButtons) {
      expect(button).not.toHaveAttribute('aria-label');
    }
  });

  it("sends each move in the list's one scope, with no retry", async () => {
    const rendered = renderEditor([breakfast, bus]);
    serveList(rendered, [breakfast, bus]);

    await press(moveButton('down', breakfast.text));

    const [write] = rendered.client.getMutationCache().getAll();
    expect(write.options.scope).toEqual({ id: `goals:${participantId}` });
    expect(write.options.retry).toBe(0);
    expect(rendered.client.getQueryCache().getAll().map((query) => query.queryKey)).toEqual([queryKey]);
  });
});

const archived = 'Goal archived';
const restored = 'Goal restored to the end of the list';

/** The archived goal's Restore, by its name while it is not busy, which ends with the goal's text. */
function restoreButton(text: string): HTMLElement {
  return screen.getByRole('button', { name: `Restore, ${text}` });
}

/** The archived goal's list item whose text is `text`. */
function archivedRowOf(text: string): HTMLLIElement {
  return archivedItems().find((item) => item.querySelector('p')?.textContent === text)!;
}

describe('ListEditor, archiving and restoring', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("gives each active row Archive after Edit, a label-width secondary button, its text hidden after it", () => {
    renderEditor([breakfast, bus]);

    for (const row of [breakfast, bus]) {
      const button = archiveButton(row.text);
      expect(button.tagName).toBe('BUTTON');
      expect(button).toHaveAttribute('type', 'button');
      expect(button.className).toMatch(/secondary/);
      expect(button.className).toMatch(/rowButton/);
      expect(button.parentElement?.parentElement?.className).toMatch(/actions/);
      expect(button).toHaveAccessibleName(`Archive, ${row.text}`);
      expect(within(button).getByText(`, ${row.text}`)).toHaveClass('visually-hidden');
    }
  });

  it('gives each archived row its text, its empty alert, then Restore, label width, its text hidden after it', () => {
    renderEditor([breakfast, washing, shop]);

    for (const row of [shop, washing]) {
      const item = archivedRowOf(row.text);
      const button = restoreButton(row.text);
      expect(within(item).getAllByRole('button')).toEqual([button]);
      expect(button).toHaveAttribute('type', 'button');
      expect(button).not.toHaveAttribute('aria-label');
      expect(button.className).toMatch(/secondary/);
      expect(button.className).toMatch(/rowButton/);
      expect(button.parentElement?.className).toMatch(/actions/);
      expect(button).toHaveAccessibleName(`Restore, ${row.text}`);
      expect(within(button).getByText(`, ${row.text}`)).toHaveClass('visually-hidden');
      const alert = within(item).getByRole('alert');
      expect(alert).toBeEmptyDOMElement();
      expect(follows(within(item).getByText(row.text), alert)).toBe(true);
      expect(follows(alert, button)).toBe(true);
    }
  });

  it('on Archive, sends its version, reads the list again, opens the section on it, focusing Restore', async () => {
    const rendered = renderEditor([breakfast, bus, sister]);
    serveList(rendered, [breakfast, bus, sister]);
    expect(archivedSection()).toBeNull();

    await press(archiveButton(bus.text));

    expect(rendered.archive).toHaveBeenCalledExactlyOnceWith(bus.id, bus.rowVersion);
    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(activeTexts()).toEqual([breakfast.text, sister.text]);
    expect(activeItems()).toHaveLength(2);
    expect(archivedSection()!.open).toBe(true);
    expect(archivedTexts()).toEqual([bus.text]);
    expect(document.activeElement).toBe(restoreButton(bus.text));
    await wait(16);
    expect(pageStatus()).toHaveTextContent(archived);
  });

  it('opens the closed archived section with the goal just archived first, the newest archived', async () => {
    const rendered = renderEditor([breakfast, washing, sister, shop]);
    serveList(rendered, [breakfast, washing, sister, shop]);
    expect(archivedSection()!.open).toBe(false);

    await press(archiveButton(sister.text));

    expect(archivedSection()!.open).toBe(true);
    expect(archivedTexts()).toEqual([sister.text, shop.text, washing.text]);
    expect(activeTexts()).toEqual([breakfast.text]);
    expect(restoreButton(sister.text)).toHaveFocus();
  });

  it('keeps the list as it is while the archive is out, busy at once and "Archiving…" after 400 ms', async () => {
    const rendered = renderEditor([breakfast, bus]);
    serveList(rendered, [breakfast, bus]);
    const answer = held<undefined>();
    const serve = vi.mocked(rendered.archive).getMockImplementation()!;
    vi.mocked(rendered.archive).mockImplementationOnce(async (...sent) => {
      await answer.promise;
      return serve(...sent);
    });
    const button = archiveButton(breakfast.text);

    await press(button);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(activeTexts()).toEqual([breakfast.text, bus.text]);
    await wait(398);
    expect(button).not.toHaveAttribute('data-busy');
    await wait(1);
    expect(button).toHaveAttribute('data-busy');
    expect(within(button).getByText('Archiving…')).toBeInTheDocument();
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Archiving…');
    await press(button);
    expect(rendered.archive).toHaveBeenCalledOnce();

    answer.resolve(undefined);
    await wait(1);

    expect(activeTexts()).toEqual([bus.text]);
    expect(archivedTexts()).toEqual([breakfast.text]);
    expect(restoreButton(breakfast.text)).toHaveFocus();
  });

  it('leaves focus where the person has gone while the archive was out, opening the section all the same', async () => {
    const rendered = renderEditor([breakfast, bus]);
    serveList(rendered, [breakfast, bus]);
    const answer = held<undefined>();
    const serve = vi.mocked(rendered.archive).getMockImplementation()!;
    vi.mocked(rendered.archive).mockImplementationOnce(async (...sent) => {
      await answer.promise;
      return serve(...sent);
    });
    await press(archiveButton(breakfast.text));

    const elsewhere = screen.getByRole('button', { name: 'Elsewhere' });
    elsewhere.focus();
    answer.resolve(undefined);
    await wait(1);

    expect(archivedTexts()).toEqual([breakfast.text]);
    expect(archivedSection()!.open).toBe(true);
    expect(elsewhere).toHaveFocus();
  });

  it("on Restore, sends the goal's version, reads the list again, shows it last, focusing its Archive", async () => {
    const rendered = renderEditor([breakfast, washing, sister, shop]);
    serveList(rendered, [breakfast, washing, sister, shop]);
    archivedSection()!.open = true;

    await press(restoreButton(washing.text));

    expect(rendered.restore).toHaveBeenCalledExactlyOnceWith(washing.id, washing.rowVersion);
    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(activeTexts()).toEqual([breakfast.text, sister.text, washing.text]);
    expect(archivedTexts()).toEqual([shop.text]);
    expect(archivedSection()!.open).toBe(true);
    expect(document.activeElement).toBe(archiveButton(washing.text));
    await wait(16);
    expect(pageStatus()).toHaveTextContent(restored);
  });

  it('takes the archived section away once Restore has emptied it', async () => {
    const rendered = renderEditor([breakfast, washing]);
    serveList(rendered, [breakfast, washing]);

    await press(restoreButton(washing.text));

    expect(archivedSection()).toBeNull();
    expect(activeTexts()).toEqual([breakfast.text, washing.text]);
    expect(archiveButton(washing.text)).toHaveFocus();
  });

  it('shows Restore busy at once, "Restoring…" after 400 ms, and a press while it is sends nothing more', async () => {
    const rendered = renderEditor([breakfast, washing]);
    vi.mocked(rendered.restore).mockReturnValue(new Promise(() => {}));
    const button = restoreButton(washing.text);

    await press(button);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    await wait(399);
    expect(button).toHaveAttribute('data-busy');
    expect(within(button).getByText('Restoring…')).toBeInTheDocument();
    await wait(16);
    expect(pageStatus()).toHaveTextContent('Restoring…');
    await press(button);

    expect(rendered.restore).toHaveBeenCalledOnce();
    expect(archivedTexts()).toEqual([washing.text]);
  });

  it('archives and restores a goal again and again, focus following it each way', async () => {
    const rendered = renderEditor([breakfast, bus]);
    serveList(rendered, [breakfast, bus]);

    await press(archiveButton(breakfast.text));
    await press(restoreButton(breakfast.text));
    await press(archiveButton(breakfast.text));

    expect(vi.mocked(rendered.archive).mock.calls.map(([id]) => id)).toEqual([breakfast.id, breakfast.id]);
    expect(rendered.restore).toHaveBeenCalledOnce();
    expect(activeTexts()).toEqual([bus.text]);
    expect(archivedSection()!.open).toBe(true);
    expect(restoreButton(breakfast.text)).toHaveFocus();
  });

  it.each([
    ['Archive', () => archiveButton(breakfast.text)],
    ['Restore', () => restoreButton(washing.text)],
  ])('on a 412 from %s, shows the list read again and the list conflict, focused, until a write succeeds', async (
    _,
    button,
  ) => {
    const rendered = renderEditor([breakfast, washing]);
    const server = serveList(rendered, [breakfast, washing]);
    // Another manager rewords both meanwhile.
    server[0] = { ...breakfast, text: 'Makes own toast', rowVersion: 'AAAAAAAAC1E=' };
    server[1] = { ...washing, text: 'Uses the dryer', rowVersion: 'AAAAAAAAC4E=' };

    await press(button());

    expect(rendered.queryFn).toHaveBeenCalledOnce();
    expect(conflict()).toHaveFocus();
    expect(follows(screen.getByText(help), conflict()!)).toBe(true);
    expect(activeTexts()).toEqual(['Makes own toast']);
    expect(archivedTexts()).toEqual(['Uses the dryer']);
    expect(rowAlert('Makes own toast')).toBeEmptyDOMElement();
    expect(within(archivedRowOf('Uses the dryer')).getByRole('alert')).toBeEmptyDOMElement();

    await press(archiveButton('Makes own toast'));

    expect(conflict()).toBeNull();
    expect(archivedTexts()).toEqual(['Makes own toast', 'Uses the dryer']);
  });

  it.each([
    ['no connection', new ApiError(0)],
    ['something went wrong', new ApiError(500)],
  ])('says "Not archived: %s" in the row\'s alert, keeping the goal and focus, emptied at the next press', async (
    cause,
    failure,
  ) => {
    const rendered = renderEditor([breakfast, bus]);
    vi.mocked(rendered.archive).mockRejectedValueOnce(failure).mockReturnValueOnce(new Promise(() => {}));
    const button = archiveButton(breakfast.text);

    await press(button);

    expect(rowAlert(breakfast.text)).toHaveTextContent(`Not archived: ${cause}. Try again.`);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(activeTexts()).toEqual([breakfast.text, bus.text]);
    expect(archivedSection()).toBeNull();
    expect(rendered.queryFn).not.toHaveBeenCalled();
    expect(conflict()).toBeNull();

    await press(button);
    expect(rowAlert(breakfast.text)).toBeEmptyDOMElement();
  });

  it.each([
    ['no connection', new ApiError(0)],
    ['something went wrong', new ApiError(500)],
  ])('says "Not restored: %s" in the archived row\'s alert, keeping it and focus, emptied at the next press', async (
    cause,
    failure,
  ) => {
    const rendered = renderEditor([breakfast, washing]);
    vi.mocked(rendered.restore).mockRejectedValueOnce(failure).mockReturnValueOnce(new Promise(() => {}));
    const button = restoreButton(washing.text);
    const alert = within(archivedRowOf(washing.text)).getByRole('alert');

    await press(button);

    expect(alert).toHaveTextContent(`Not restored: ${cause}. Try again.`);
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(activeTexts()).toEqual([breakfast.text]);
    expect(archivedTexts()).toEqual([washing.text]);
    expect(rendered.queryFn).not.toHaveBeenCalled();

    await press(button);
    expect(alert).toBeEmptyDOMElement();
  });

  it('shows nothing for a 401 on Archive or Restore, which the session flow has', async () => {
    const rendered = renderEditor([breakfast, washing]);
    vi.mocked(rendered.archive).mockRejectedValue(new ApiError(401));
    vi.mocked(rendered.restore).mockRejectedValue(new ApiError(401));

    await press(archiveButton(breakfast.text));
    await press(restoreButton(washing.text));

    expect(rowAlert(breakfast.text)).toBeEmptyDOMElement();
    expect(within(archivedRowOf(washing.text)).getByRole('alert')).toBeEmptyDOMElement();
    expect(conflict()).toBeNull();
  });

  it('sends the version the list holds as the write runs, read again by the write before it', async () => {
    const rendered = renderEditor([breakfast, bus, washing]);
    serveList(rendered, [breakfast, bus, washing]);
    const answer = held<undefined>();
    const serve = vi.mocked(rendered.reorder).getMockImplementation()!;
    vi.mocked(rendered.reorder).mockImplementationOnce(async (ids) => {
      await answer.promise;
      return serve(ids);
    });
    await press(moveButton('down', breakfast.text));
    await press(archiveButton(breakfast.text));
    await press(restoreButton(washing.text));
    expect(rendered.archive).not.toHaveBeenCalled();

    answer.resolve(undefined);
    await wait(1);

    // The reorder gave both moved goals a new version; the restore's goal kept its own.
    expect(rendered.archive).toHaveBeenCalledExactlyOnceWith(breakfast.id, 'AAAAAAAAD2=');
    expect(rendered.restore).toHaveBeenCalledExactlyOnceWith(washing.id, washing.rowVersion);
    expect(activeTexts()).toEqual([bus.text, washing.text]);
    expect(archivedTexts()).toEqual([breakfast.text]);
    expect(conflict()).toBeNull();
  });

  it.each([
    ['an Archive whose goal the write before it found archived', () => archiveButton(breakfast.text), 'archive'],
    ['a Restore whose goal the write before it found restored', () => restoreButton(washing.text), 'restore'],
  ] as const)('sends nothing for %s, showing the list conflict', async (_, button, write) => {
    const rendered = renderEditor([breakfast, washing]);
    const answer = held<undefined>();
    vi.mocked(rendered.add).mockReturnValue(answer.promise);
    // Another manager archives the one and restores the other meanwhile.
    vi.mocked(rendered.queryFn).mockResolvedValue([
      { ...washing, sortOrder: 0, archivedAtUtc: null, rowVersion: 'AAAAAAAAC4E=' },
      { ...breakfast, archivedAtUtc: '2026-10-06T00:00:00Z', rowVersion: 'AAAAAAAAC1E=' },
      goal('6', 'Phones her sister', 1),
    ]);
    type('Phones her sister');
    await press(addButton());
    await press(button());

    answer.resolve(undefined);
    await wait(1);

    expect(rendered[write]).not.toHaveBeenCalled();
    expect(conflict()).toHaveFocus();
    expect(activeTexts()).toEqual([washing.text, 'Phones her sister']);
    expect(archivedTexts()).toEqual([breakfast.text]);
  });

  it("sends each archive and restore in the list's one scope, with no retry", async () => {
    const rendered = renderEditor([breakfast, washing]);
    serveList(rendered, [breakfast, washing]);

    await press(archiveButton(breakfast.text));
    await press(restoreButton(washing.text));

    const writes = rendered.client.getMutationCache().getAll();
    expect(writes).toHaveLength(2);
    for (const write of writes) {
      expect(write.options.scope).toEqual({ id: `goals:${participantId}` });
      expect(write.options.retry).toBe(0);
    }
    expect(rendered.client.getQueryCache().getAll().map((query) => query.queryKey)).toEqual([queryKey]);
  });
});
