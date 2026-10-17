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
};

/**
 * Renders the editor for goals in a page's status region, its list in the query already, as its page puts it there,
 * under a query client of its own, beside a button elsewhere on the page; gives the client, the query's function and
 * the adds' function.
 */
function renderEditor(rows: ListRow[], readOnly = false): Rendered {
  const client = createQueryClient();
  client.setQueryData(queryKey, rows);
  const queryFn = vi.fn<() => Promise<ListRow[]>>();
  const add = vi.fn<(text: string) => Promise<unknown>>();
  const api: ListApi = { query: queryOptions({ queryKey, queryFn }), scopeId: `goals:${participantId}`, add };
  render(
    <QueryClientProvider client={client}>
      <PageStatus>
        <ListEditor kind="goal" api={api} readOnly={readOnly} />
        <button type="button">Elsewhere</button>
      </PageStatus>
    </QueryClientProvider>,
  );
  return { client, queryFn, add };
}

/** The numbered list of active goals, or `null` while there is none. */
function activeList(): HTMLOListElement | null {
  return document.querySelector('ol');
}

/** Each active goal's list item. */
function activeItems(): HTMLLIElement[] {
  return Array.from(activeList()?.children ?? [], (item) => item as HTMLLIElement);
}

/** The archived section, or `null` while it is left out. */
function archivedSection(): HTMLDetailsElement | null {
  return document.querySelector('details');
}

/** The archived goals' texts, in the order shown. */
function archivedTexts(): string[] {
  return Array.from(archivedSection()?.querySelectorAll('ul > li') ?? [], (item) => item.textContent);
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

    expect(activeItems().map((item) => item.textContent)).toEqual([sister.text, breakfast.text, bus.text]);
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
    expect(activeItems().map((item) => item.textContent)).toEqual([breakfast.text, sister.text]);
    expect(activeList()!.compareDocumentPosition(details) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(details.open).toBe(false);
    expect(details).not.toHaveAttribute('open');
    const summary = details.querySelector('summary')!;
    expect(summary).toBe(details.firstElementChild);
    expect(summary.textContent).toBe('Archived goals');
    expect(summary.children).toHaveLength(0);
    expect(archivedTexts()).toEqual([shop.text, washing.text]);
    expect(within(details).queryByRole('alert')).toBeNull();
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
    expect(activeItems().map((item) => item.textContent)).toEqual([breakfast.text, sister.text]);
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
 * Has the adds and the list's reads answered by a server that holds the list: an add puts a goal at its end, and a
 * read gives what it holds.
 */
function serveList({ add, queryFn }: Rendered, rows: ListRow[]) {
  const list = [...rows];
  vi.mocked(add).mockImplementation((text) => {
    list.push(goal(`added-${list.length}`, text, list.length));
    return Promise.resolve(undefined);
  });
  vi.mocked(queryFn).mockImplementation(() => Promise.resolve([...list]));
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
    expect(activeItems().map((item) => item.textContent)).toEqual([breakfast.text, 'Catches the bus to day program']);
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
      expect(activeItems().map((item) => item.textContent)).toEqual(texts.slice(0, index + 1));
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
    expect(activeItems().map((item) => item.textContent)).toEqual(['Makes own breakfast']);
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

    expect(activeItems().map((item) => item.textContent)).toEqual(['Makes own breakfast']);
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
