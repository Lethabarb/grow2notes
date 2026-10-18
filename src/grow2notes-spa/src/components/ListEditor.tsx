import { useMutation, useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { useEffect, useId, useRef, useState, type FormEvent, type MouseEvent, type RefObject } from 'react';
import { flushSync } from 'react-dom';
import { ApiError, classify } from '../api/api.ts';
import { msg } from '../copy/messages.ts';
import { strings, type Cause } from '../copy/strings.ts';
import { limits } from '../limits.ts';
import { Button } from './Button.tsx';
import { CharacterCount } from './CharacterCount.tsx';
import { FieldGroup } from './FieldGroup.tsx';
import styles from './ListEditor.module.css';
import { useListFrame } from './listFrameContext.ts';
import { useAnnounce } from './pageStatusContext.ts';
import { RequestAlert } from './RequestAlert.tsx';
import textBox from './TextBox.module.css';

/** The kinds of list that the editor edits, each with its words in `strings.listEditor`. */
type ListKind = keyof typeof strings.listEditor;

/** A row of an ordered list as the API lists it (design.md §6.6), such as one of a participant's goals. */
export type ListRow = {
  id: string;
  text: string;
  sortOrder: number;
  /** In UTC, with `Z`, or `null` while the row is active. */
  archivedAtUtc: string | null;
  /** The row's `RowVersion` as base64, which a change to the row sends as `If-Match` (D78). */
  rowVersion: string;
};

/** How the editor writes to one list's API (list-editor.md *Implementation notes*): its writes, and their scope. */
type ListWrites = {
  /**
   * The TanStack mutation scope that every write on the list shares, such as `goals:<participantId>`, or
   * `common-items` for every group's list on Common items, so that the writes on the page run one at a time.
   */
  scopeId: string;
  /** Adds a row of `text` at the end of the list, as the API's `POST` does; the editor reads nothing of the answer. */
  add: (text: string) => Promise<unknown>;
  /**
   * Rewords a row, with its `rowVersion` as `If-Match`, as the API's `PUT` does; the editor reads nothing of the
   * answer, as it reads the row's next version from the list.
   */
  update: (id: string, text: string, rowVersion: string) => Promise<unknown>;
  /**
   * Puts the list's active rows in the order of `ids`, each active row's ID once, as the API's `PUT …/order` does; the
   * editor reads nothing of the answer, as it reads the new order, and each row's next version, from the list.
   */
  reorder: (ids: string[]) => Promise<unknown>;
  /**
   * Archives a row, with its `rowVersion` as `If-Match`, as the API's `POST …/archive` does, leaving its place in the
   * order; the editor reads nothing of the answer.
   */
  archive: (id: string, rowVersion: string) => Promise<unknown>;
  /**
   * Restores a row to the end of the list, with its `rowVersion` as `If-Match`, as the API's `POST …/restore` does; the
   * editor reads nothing of the answer.
   */
  restore: (id: string, rowVersion: string) => Promise<unknown>;
};

/**
 * How the editor reaches one list's API (list-editor.md *Implementation notes*): the query that holds its rows, which
 * may hold other lists' too, as Common items' one query holds every group's; the list's rows, active and archived, in
 * the API's order, picked from that query's data; and its writes.
 */
export type ListApi<TData> = ListWrites & {
  query: UseQueryOptions<TData, Error, TData, string[]>;
  rowsOf: (data: TData) => ListRow[];
};

type ListEditorProps<TData> = {
  kind: ListKind;
  api: ListApi<TData>;
  /**
   * The list's name among the lists on the page, a group's name on Common items, after which its add field's label
   * and, for screen readers alone, its archived section's summary say " in [name]", so that each is told from the
   * others' (common-items.md component 5).
   */
  name?: string;
  /** For an archived participant's goals (participants.md B6): the rows' text alone, with no controls. */
  readOnly?: boolean;
};

/**
 * What the editor's rows and forms share with it: the list's kind, name and writes; the id of the page's help text,
 * which describes each text field; the rows as last read, and the read that follows each write; focus that follows a
 * change, and the list's archived section that an Archive opens; and the list conflict, which a write shows and the
 * next write that succeeds takes away.
 */
type ListContext = {
  kind: ListKind;
  name: string | undefined;
  api: ListWrites;
  helpId: string;
  /** The rows as the list was last read, which a write reads as it runs. */
  latest: () => ListRow[];
  /** Reads the list again, which a write waits for, so that the next write starts from what the server holds. */
  reread: () => Promise<void>;
  /** Registers an element under `key`, such as a row's Edit under `<id>:edit`, for `focusAfterRender`. */
  focusTarget: (key: string) => (element: HTMLElement | null) => void;
  /** Focuses the element registered under `key` once the render that a change brings is in the DOM. */
  focusAfterRender: (key: string) => void;
  /**
   * Opens the archived section once the render that an Archive brings is in the DOM, as it may have only just
   * appeared, then focuses the element registered under `key`, if one is given.
   */
  openArchivedAfterRender: (key: string | undefined) => void;
  /** Shows the list conflict above the list, and focuses it, as a write found the list changed by someone else. */
  changedMeanwhile: () => void;
  /** Takes the list conflict away, as a write on the list has succeeded. */
  written: () => void;
};

/** Which way a row's move button moves it. */
type Direction = 'up' | 'down';

/** Each move button's words: its label, and what a press says where the row is already at the button's end. */
const moveWords = {
  up: { label: strings.listRows.moveUp, atEnd: strings.listRows.atTop },
  down: { label: strings.listRows.moveDown, atEnd: strings.listRows.atBottom },
} as const;

// Each move button's arrow, filled with the button's colour, which forced colours give it too.
const arrowPaths = {
  up: 'M10 2.5 3.5 9h4.25v8.5h4.5V9h4.25z',
  down: 'M10 17.5 16.5 11h-4.25V2.5h-4.5V11H3.5z',
} as const;

/**
 * A write that found its row no longer active, as someone else archived it, once the list was read again; the list
 * conflict then shows, as the row has left the list with its form (list-editor.md *Request errors*).
 */
class ListChanged extends Error {
  override name = 'ListChanged';
}

/**
 * A move that waited its turn behind a write that took its row to the end it moves towards, so that it sends nothing
 * (list-editor.md *Queueing*).
 */
class AlreadyAtEnd extends Error {
  override name = 'AlreadyAtEnd';
}

/**
 * A Save refused as stale (`412`) whose row is still active once the list is read again, with the row's wording now
 * (list-editor.md *Request errors*).
 */
class RowChanged extends Error {
  override name = 'RowChanged';
  readonly text: string;

  constructor(text: string) {
    super('Someone else changed the row while it was being edited.');
    this.text = text;
  }
}

/**
 * Sends a write that the server refuses with `status` when the list has changed since it was read (list-editor.md
 * *Request errors*): a reorder's `422 config.order_mismatch` (design.md §6.7), or an archive's or a restore's `412`.
 * That refusal reads the list again and fails as `ListChanged`, so that the list conflict shows over what the server
 * now holds; any other failure fails as it is.
 */
async function sendUnlessListChanged(list: ListContext, status: number, send: () => Promise<unknown>): Promise<void> {
  try {
    await send();
  } catch (failed) {
    if (!(failed instanceof ApiError && failed.status === status)) {
      throw failed;
    }

    await list.reread();
    throw new ListChanged();
  }
}

/**
 * What a write that failed says in the alert of the row or the form that sent it (list-editor.md *Request errors*):
 * `message` with the cause; or nothing for the list conflict, which shows above the list, or for a `401`, which the
 * session flow has, as `api()` started it.
 */
function failureAlert(failed: unknown, message: (cause: Cause) => string): string | undefined {
  if (failed instanceof ListChanged) {
    return undefined;
  }

  const cause = classify(failed);
  return cause === 'signedOut' ? undefined : message(cause);
}

/**
 * A row's text as Add and Save send it (list-editor.md *Implementation notes*): each line break, with the spaces
 * around it, as one space, and the ends trimmed, as the server cleans it too.
 */
function cleanText(text: string): string {
  return text.replace(/\s*[\r\n]+\s*/g, ' ').trim();
}

/**
 * The message for a row's cleaned text, or nothing (form-validation.md): none left, or more than `limits.itemText`
 * characters, counted in UTF-16 units, as the server counts them.
 */
function textError(cleaned: string, kind: ListKind): string | undefined {
  const copy = strings.listEditor[kind];
  if (cleaned === '') {
    return copy.enter;
  }

  return cleaned.length > limits.itemText ? msg.tooLong(copy.text, limits.itemText) : undefined;
}

/** The active row of ID `id` among `rows`, or `undefined` once it is archived. */
function activeRow(rows: ListRow[], id: string): ListRow | undefined {
  return rows.find((row) => row.id === id && row.archivedAtUtc === null);
}

/** The archived row of ID `id` among `rows`, or `undefined` once it is active. */
function archivedRow(rows: ListRow[], id: string): ListRow | undefined {
  return rows.find((row) => row.id === id && row.archivedAtUtc !== null);
}

/**
 * Whether focus is still in `origin`, the form or the button that started a write, or nowhere: only then does the
 * write's end move it, as focus is never pulled away from where the person has gone while the write was out
 * (common-items.md *Queueing*).
 */
function focusStayedIn(origin: HTMLElement | null): boolean {
  const focused = document.activeElement;
  return focused === null || focused === document.body || origin?.contains(focused) === true;
}

/**
 * An ordered list that managers edit (list-editor.md *Anatomy*, *States*), in a `ListFrame`, which holds the help text
 * and the list conflict that it shares with any other lists on the page: the active rows as a numbered `<ol>`, in the
 * API's order, which the editor never sorts again, as the API breaks ties in `SortOrder` as the reorder reads them;
 * or, with no active row, the kind's empty sentence; then the add form; then the archived rows, newest archived first,
 * each with Restore, as a `<ul>` in a `<details>`, closed as it loads and left to the person from then on but for an
 * Archive, which opens it, and left out while none is archived (decision 7). Read-only, each row is its text alone,
 * and the add form is left out, as nothing can be changed.
 *
 * The page loads the list before rendering the editor, in its load region, or puts it in the query itself, as Add
 * participant does with a new participant's empty one, so the editor reads it from the query and never fetches it as
 * it mounts.
 */
export function ListEditor<TData>({ kind, api, name, readOnly = false }: ListEditorProps<TData>) {
  const copy = strings.listEditor[kind];
  const frame = useListFrame();
  // The archived section's key in the frame's focus map, which an Archive opens before its row's Restore takes focus.
  const archivedKey = useId();
  const queryClient = useQueryClient();
  const { data: rows = [] } = useQuery<TData, Error, ListRow[], string[]>({
    ...api.query,
    select: api.rowsOf,
    refetchOnMount: false,
  });
  const active = rows.filter((row) => row.archivedAtUtc === null);
  const archived = rows
    .flatMap((row) => (row.archivedAtUtc === null ? [] : [{ row, archivedAt: Date.parse(row.archivedAtUtc) }]))
    .sort((a, b) => b.archivedAt - a.archivedAt)
    .map(({ row }) => row);

  const list: ListContext = {
    kind,
    name,
    api,
    helpId: frame.helpId,
    latest: () => {
      const data = queryClient.getQueryData<TData>(api.query.queryKey);
      return data === undefined ? [] : api.rowsOf(data);
    },
    reread: () => queryClient.refetchQueries({ queryKey: api.query.queryKey, exact: true }),
    focusTarget: frame.focusTarget,
    focusAfterRender: frame.focusAfterRender,
    openArchivedAfterRender: (key) => {
      frame.openArchivedAfterRender(archivedKey, key);
    },
    changedMeanwhile: frame.changedMeanwhile,
    written: frame.written,
  };

  return (
    <>
      {active.length === 0 ? (
        <p>{copy.empty}</p>
      ) : (
        <ol className={styles.list}>
          {active.map((row, index) =>
            readOnly ? (
              <li key={row.id} className={styles.row}>
                <p className={styles.text}>{row.text}</p>
              </li>
            ) : (
              <ActiveRow
                key={row.id}
                row={row}
                list={list}
                first={index === 0}
                last={index === active.length - 1}
              />
            ),
          )}
        </ol>
      )}
      {!readOnly && <AddForm list={list} />}
      {archived.length > 0 && (
        <details ref={frame.focusTarget(archivedKey)} className={styles.archived}>
          <summary className={styles.summary}>
            {copy.archivedSummary}
            {name !== undefined && <span className="visually-hidden">{msg.inList(name)}</span>}
          </summary>
          <ul className={styles.archivedList}>
            {archived.map((row) =>
              readOnly ? (
                <li key={row.id} className={styles.row}>
                  <p className={styles.text}>{row.text}</p>
                </li>
              ) : (
                <ArchivedRow key={row.id} row={row} list={list} />
              ),
            )}
          </ul>
        </details>
      )}
    </>
  );
}

/**
 * A write to the list (list-editor.md *Implementation notes*): in the list's one scope, so that it waits for the write
 * before it; sent once, never again by itself, as an add sent again after a lost answer would add the row twice; then
 * the list read again before the next write starts, as the screen shows a change only once the server has it, and
 * what `send` gave passed on. Once it succeeds, the list conflict goes; once it finds the list changed, the conflict
 * shows, though the form or the row that sent it may have gone.
 */
function useListWrite<T, R>(list: ListContext, send: (variables: T) => Promise<R>) {
  return useMutation({
    mutationFn: async (variables: T) => {
      const result = await send(variables);
      await list.reread();
      return result;
    },
    onSuccess: () => {
      list.written();
    },
    onError: (failed) => {
      if (failed instanceof ListChanged) {
        list.changedMeanwhile();
      }
    },
    scope: { id: list.api.scopeId },
    retry: 0,
  });
}

/**
 * A row's text as its form holds it (form-validation.md's one-field pattern): as typed, in this page's memory only
 * (D22); its inputs, after which the count's twin speaks; and its message, shown by a press that finds the text
 * refused, or by the server's refusal of it, each of which moves focus to the field, even with the same message, and,
 * once shown, checked again at each input, and taken away, silently, once the text passes.
 */
function useItemText(kind: ListKind, initial: string, field: RefObject<HTMLTextAreaElement | null>) {
  const [text, setText] = useState(initial);
  const [inputs, setInputs] = useState(0);
  const [error, setError] = useState<string>();
  const [refusals, setRefusals] = useState(0);

  // After the render that names the message in the field's description, so that a screen reader reads it with focus.
  useEffect(() => {
    if (refusals > 0) {
      field.current?.focus();
    }
  }, [refusals, field]);

  function refuse(message: string) {
    setError(message);
    setRefusals((presses) => presses + 1);
  }

  return {
    text,
    setText,
    inputs,
    error,
    edit: (next: string) => {
      setText(next);
      setInputs((count) => count + 1);
      setError((shown) => (shown === undefined ? undefined : textError(cleanText(next), kind)));
    },
    /** The text as its form sends it, or `undefined` once it is refused, with its message shown. */
    checked: (): string | undefined => {
      const cleaned = cleanText(text);
      const refused = textError(cleaned, kind);
      if (refused === undefined) {
        return cleaned;
      }

      refuse(refused);
      return undefined;
    },
    /** Whether `failed` is the server's refusal of the text, a `422` under `text`, which it shows on the field. */
    refusedByServer: (failed: unknown): boolean => {
      const refusal = failed instanceof ApiError && failed.status === 422 ? failed.errors?.text?.[0] : undefined;
      if (refusal !== undefined) {
        refuse(refusal);
      }

      return refusal !== undefined;
    },
  };
}

type ActiveRowProps = {
  row: ListRow;
  list: ListContext;
  /** Whether the row is the first active one, where Move up is unavailable. */
  first: boolean;
  /** Whether the row is the last active one, where Move down is unavailable. */
  last: boolean;
};

/**
 * An active row (list-editor.md *Anatomy*, *Behaviour*): its text, wrapping anywhere, then its alert slot, always
 * rendered, which a failed move or Archive writes to, then its buttons in two pairs, Move up and Move down, then Edit
 * and Archive, each label width, with the row's text after its label for screen readers alone; or, once Edit is
 * pressed, the edit form in their place, in the same numbered item, until Save or Cancel closes it and focus goes back
 * to Edit, unless a Save finds it gone elsewhere as it ends. The form is rendered inside the press, so that focus goes
 * to its box, with the caret after the text, while the tap still runs, which is what lets a phone open its keyboard.
 * Its typed text is the row's own, so it stays as typed while other rows move.
 *
 * Archive is busy at once, "Archiving…" after 400 ms, and waits its turn in the list's scope; when it runs, it sends
 * the row's version as the list was last read. Once the list is read again, the row has left the active list, whose
 * numbers close up, for the top of the archived section, which opens, and focus goes to the row's Restore there,
 * unless the person has gone elsewhere meanwhile, and the page's status region says so. An Archive whose row has
 * already left the list, or that the server refuses as stale, shows the list conflict over the list read again; any
 * other failure says so in the row's alert, focus staying on Archive.
 */
function ActiveRow({ row, list, first, last }: ActiveRowProps) {
  const announce = useAnnounce();
  const [editing, setEditing] = useState(false);
  // The alert's words while it holds a move's or an Archive's failure, which the row's next press, or Edit, clears.
  const [failure, setFailure] = useState<string>();
  const field = useRef<HTMLTextAreaElement>(null);
  const editKey = `${row.id}:edit`;
  const archive = useListWrite(list, async () => {
    // The version as the write runs, which the list read after the write before it may have changed.
    const current = activeRow(list.latest(), row.id);
    if (current === undefined) {
      throw new ListChanged();
    }

    await sendUnlessListChanged(list, 412, () => list.api.archive(row.id, current.rowVersion));
  });

  function edit() {
    flushSync(() => {
      // The form takes the alert's place, which comes back as it closes, so it must come back empty, not stale.
      setFailure(undefined);
      setEditing(true);
    });
    const box = field.current;
    if (box !== null) {
      box.focus();
      box.setSelectionRange(box.value.length, box.value.length);
    }
  }

  function close(focusEdit: boolean) {
    setEditing(false);
    if (focusEdit) {
      list.focusAfterRender(editKey);
    }
  }

  function pressArchive(event: MouseEvent<HTMLButtonElement>) {
    const button = event.currentTarget;
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    archive.mutate(undefined, {
      onSuccess: () => {
        list.openArchivedAfterRender(focusStayedIn(button) ? `${row.id}:restore` : undefined);
        announce?.(strings.listEditor[list.kind].archived);
      },
      onError: (failed) => {
        setFailure(failureAlert(failed, msg.notArchived));
      },
    });
  }

  return (
    <li className={styles.row}>
      {editing ? (
        <EditForm row={row} list={list} field={field} onClose={close} />
      ) : (
        <div className={styles.rowInner}>
          <p className={styles.text}>{row.text}</p>
          <div className={styles.side}>
            <RequestAlert>{failure}</RequestAlert>
            <div className={styles.actions}>
              <div className={styles.pair}>
                <MoveButton row={row} list={list} direction="up" atEnd={first} onFailure={setFailure} />
                <MoveButton row={row} list={list} direction="down" atEnd={last} onFailure={setFailure} />
              </div>
              <div className={styles.pair}>
                <Button ref={list.focusTarget(editKey)} className={styles.rowButton} onClick={edit}>
                  {strings.listRows.edit}
                  <span className="visually-hidden">{msg.rowContext(row.text)}</span>
                </Button>
                <Button
                  ref={list.focusTarget(`${row.id}:archive`)}
                  className={styles.rowButton}
                  busy={archive.isPending}
                  busyLabel={strings.archiving}
                  onClick={pressArchive}
                >
                  {strings.listRows.archive}
                  <span className="visually-hidden">{msg.rowContext(row.text)}</span>
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
    </li>
  );
}

type MoveButtonProps = {
  row: ListRow;
  list: ListContext;
  direction: Direction;
  /** Whether the row is already at the end that the button moves it towards, where the button is unavailable. */
  atEnd: boolean;
  /** Shows a move's failure in the row's alert, or, given `undefined`, empties it. */
  onFailure: (message: string | undefined) => void;
};

/**
 * A row's Move up or Move down (list-editor.md *Behaviour*), in words after a decorative arrow, with the row's text
 * after them for screen readers alone. At the row's end it stays in place, unavailable (`aria-disabled`, dashed), and a
 * press sends nothing and says why in the page's status region. Otherwise a press is busy at once, "Moving…" after
 * 400 ms, and waits its turn in the list's scope; when it runs, it swaps the row with its neighbour in the order of the
 * list as last read, and sends that whole order, the list showing the new order only once it has been read again.
 * Then focus goes back to the button, unless the person has gone elsewhere meanwhile, and the page's status region
 * says where the row went. A move whose row an earlier one has taken to the end sends nothing, and says so as an
 * unavailable press does; one whose row has left the list, or that the server refuses `422` as the list has changed,
 * shows the list conflict over the list read again; and any other failure says so in the row's alert, focus staying on
 * the button.
 */
function MoveButton({ row, list, direction, atEnd, onFailure }: MoveButtonProps) {
  const announce = useAnnounce();
  const words = moveWords[direction];
  const key = `${row.id}:${direction}`;
  const move = useListWrite(list, async () => {
    // The order as the write runs, which the list read after the write before it may have changed.
    const ids = list.latest().flatMap((listed) => (listed.archivedAtUtc === null ? [listed.id] : []));
    const from = ids.indexOf(row.id);
    if (from === -1) {
      throw new ListChanged();
    }

    const to = direction === 'up' ? from - 1 : from + 1;
    if (to < 0 || to === ids.length) {
      throw new AlreadyAtEnd();
    }

    [ids[from], ids[to]] = [ids[to], ids[from]];
    await sendUnlessListChanged(list, 422, () => list.api.reorder(ids));
    return { position: to + 1, total: ids.length };
  });

  function press(event: MouseEvent<HTMLButtonElement>) {
    const button = event.currentTarget;
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    onFailure(undefined);
    move.mutate(undefined, {
      onSuccess: ({ position, total }) => {
        if (focusStayedIn(button)) {
          list.focusAfterRender(key);
        }

        announce?.(msg.moved(direction, position, total));
      },
      onError: (failed) => {
        if (failed instanceof AlreadyAtEnd) {
          if (focusStayedIn(button)) {
            list.focusAfterRender(key);
          }

          announce?.(words.atEnd);
          return;
        }

        onFailure(failureAlert(failed, msg.notMoved));
      },
    });
  }

  return (
    <Button
      ref={list.focusTarget(key)}
      className={styles.rowButton}
      unavailable={atEnd}
      onUnavailablePress={() => announce?.(words.atEnd)}
      busy={move.isPending}
      busyLabel={strings.listRows.moving}
      onClick={press}
    >
      <svg className={styles.arrow} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
        <path d={arrowPaths[direction]} fill="currentColor" />
      </svg>
      {words.label}
      <span className="visually-hidden">{msg.rowContext(row.text)}</span>
    </Button>
  );
}

/**
 * An archived row (list-editor.md *Anatomy*, *Behaviour*): its text, wrapping anywhere, then its alert slot, always
 * rendered, then Restore, label width, with the row's text after its label for screen readers alone. Restore is busy at
 * once, "Restoring…" after 400 ms, and waits its turn in the list's scope; when it runs, it sends the row's version as
 * the list was last read. Once the list is read again, the row is at the end of the active list, where focus goes to
 * its Archive, unless the person has gone elsewhere meanwhile, and the page's status region says so; the archived
 * section goes once it is empty. A Restore whose row is no longer archived, or that the server refuses as stale, shows
 * the list conflict over the list read again; any other failure says so in the row's alert, focus staying on Restore.
 */
function ArchivedRow({ row, list }: { row: ListRow; list: ListContext }) {
  const announce = useAnnounce();
  // The alert's words while it holds a Restore's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const restore = useListWrite(list, async () => {
    // The version as the write runs, which the list read after the write before it may have changed.
    const current = archivedRow(list.latest(), row.id);
    if (current === undefined) {
      throw new ListChanged();
    }

    await sendUnlessListChanged(list, 412, () => list.api.restore(row.id, current.rowVersion));
  });

  function press(event: MouseEvent<HTMLButtonElement>) {
    const button = event.currentTarget;
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    restore.mutate(undefined, {
      onSuccess: () => {
        if (focusStayedIn(button)) {
          list.focusAfterRender(`${row.id}:archive`);
        }

        announce?.(strings.listEditor[list.kind].restored);
      },
      onError: (failed) => {
        setFailure(failureAlert(failed, msg.notRestored));
      },
    });
  }

  return (
    <li className={styles.row}>
      <div className={styles.rowInner}>
        <p className={styles.text}>{row.text}</p>
        <div className={styles.side}>
          <RequestAlert>{failure}</RequestAlert>
          <div className={styles.actions}>
            <Button
              ref={list.focusTarget(`${row.id}:restore`)}
              className={styles.rowButton}
              busy={restore.isPending}
              busyLabel={strings.restoring}
              onClick={press}
            >
              {strings.restore}
              <span className="visually-hidden">{msg.rowContext(row.text)}</span>
            </Button>
          </div>
        </div>
      </div>
    </li>
  );
}

type EditFormProps = {
  row: ListRow;
  list: ListContext;
  /** The box, which the row focuses as it opens the form. */
  field: RefObject<HTMLTextAreaElement | null>;
  /** Closes the form, focusing the row's Edit when `focusEdit`. */
  onClose: (focusEdit: boolean) => void;
};

/**
 * A row's edit form (list-editor.md *Behaviour*, participants.md B6): the row's text field, filled with its text, then
 * the alert for a request that failed, then Save, primary, and Cancel, which drops what was typed, both label width.
 * Save checks the text as Add does, sending nothing while it fails; then, when the text as Save would send it is the
 * row's own, it closes the form as Cancel does and sends nothing, as there is nothing to save. Otherwise it sends the
 * text with the row's version as the write runs, the box read-only while it is out, and once the list is read again,
 * closes the form, focus going to Edit unless the person has gone elsewhere meanwhile, and the page's status region
 * says so. A failure keeps the text, focus staying on Save: the server's refusal of the text shows on the field as
 * Save's own does; a Save refused as stale whose row is still active says what the row now says, and the next Save
 * sends its new version; a row archived meanwhile leaves the list, and its form with it, and the list conflict shows;
 * and anything else says so in the alert. Cancel stays free while a Save is out or queued, closing the form, as a
 * write once started runs to its end and the list then shows what the server holds.
 */
function EditForm({ row, list, field, onClose }: EditFormProps) {
  const copy = strings.listEditor[list.kind];
  const announce = useAnnounce();
  const fieldId = useId();
  const form = useRef<HTMLFormElement>(null);
  const box = useItemText(list.kind, row.text, field);
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const save = useListWrite(list, async (text: string) => {
    // The version as the write runs, which the list read after the write before it may have changed.
    const current = activeRow(list.latest(), row.id);
    if (current === undefined) {
      throw new ListChanged();
    }

    try {
      await list.api.update(row.id, text, current.rowVersion);
    } catch (failed) {
      if (!(failed instanceof ApiError && failed.status === 412)) {
        throw failed;
      }

      await list.reread();
      const now = activeRow(list.latest(), row.id);
      throw now === undefined ? new ListChanged() : new RowChanged(now.text);
    }
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    const sent = box.checked();
    if (sent === undefined) {
      return;
    }

    if (sent === row.text) {
      onClose(true);
      return;
    }

    save.mutate(sent, {
      onSuccess: () => {
        onClose(focusStayedIn(form.current));
        announce?.(copy.saved);
      },
      onError: (failed) => {
        if (failed instanceof RowChanged) {
          setFailure(msg.rowChangedWhileEditing(copy.row, failed.text));
          return;
        }

        if (!box.refusedByServer(failed)) {
          setFailure(failureAlert(failed, msg.notSaved));
        }
      },
    });
  }

  return (
    <form ref={form} noValidate className={textBox.column} onSubmit={submit}>
      <ItemTextField
        fieldId={fieldId}
        label={copy.editLabel}
        helpId={list.helpId}
        text={box.text}
        error={box.error}
        inputs={box.inputs}
        readOnly={save.isPending}
        fieldRef={field}
        onChange={box.edit}
      />
      <RequestAlert>{failure}</RequestAlert>
      <div className={styles.actions}>
        <Button
          type="submit"
          variant="primary"
          className={styles.rowButton}
          busy={save.isPending}
          busyLabel={strings.saving}
        >
          {strings.save}
        </Button>
        <Button
          className={styles.rowButton}
          onClick={() => {
            onClose(true);
          }}
        >
          {strings.cancel}
        </Button>
      </div>
    </form>
  );
}

/**
 * The add form (list-editor.md *Behaviour*, participants.md B6): the row's text field, then the alert for a request
 * that failed, then Add, primary. Add checks the text alone, in form-validation.md's one-field pattern, and sends
 * nothing while it fails: the message above the field, and focus on it, with no error summary and no "Error: " in the
 * title. Sent, the row goes to the end of the list, which is read again before Add is free, then the field is emptied
 * if it still holds the text sent, with focus kept in it for the next one, and the page's status region says so; text
 * typed while the add was out stays. A failure keeps the text and says so in the alert, focus staying on Add, but for
 * the server's refusal of the text, which shows on the field as Add's own does.
 */
function AddForm({ list }: { list: ListContext }) {
  const copy = strings.listEditor[list.kind];
  const announce = useAnnounce();
  const fieldId = useId();
  const form = useRef<HTMLFormElement>(null);
  const field = useRef<HTMLTextAreaElement>(null);
  const box = useItemText(list.kind, '', field);
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const add = useListWrite(list, list.api.add);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    const sent = box.checked();
    if (sent === undefined) {
      return;
    }

    add.mutate(sent, {
      onSuccess: () => {
        // Emptied only while it still holds what was sent, so that nothing typed while the add was out is lost.
        box.setText((current) => (cleanText(current) === sent ? '' : current));
        if (focusStayedIn(form.current)) {
          field.current?.focus();
        }

        announce?.(copy.added);
      },
      onError: (failed) => {
        if (!box.refusedByServer(failed)) {
          setFailure(failureAlert(failed, msg.notAdded));
        }
      },
    });
  }

  return (
    <form ref={form} noValidate className={`${textBox.column} ${styles.addForm}`} onSubmit={submit}>
      <ItemTextField
        fieldId={fieldId}
        label={list.name === undefined ? copy.newLabel : `${copy.newLabel}${msg.inList(list.name)}`}
        helpId={list.helpId}
        text={box.text}
        error={box.error}
        inputs={box.inputs}
        fieldRef={field}
        onChange={box.edit}
      />
      <RequestAlert>{failure}</RequestAlert>
      <Button type="submit" variant="primary" busy={add.isPending} busyLabel={strings.adding}>
        {copy.add}
      </Button>
    </form>
  );
}

type ItemTextFieldProps = {
  fieldId: string;
  label: string;
  /** The id of the list's help text, which describes the field after its hint. */
  helpId: string;
  text: string;
  /** The message after a press that found the text refused, until the text passes. */
  error: string | undefined;
  /** The field's inputs, after which the count's twin speaks. */
  inputs: number;
  /** While a Save of the text is out, so that what was sent is what shows (list-editor.md *Behaviour*). */
  readOnly?: boolean;
  fieldRef: RefObject<HTMLTextAreaElement | null>;
  onChange: (text: string) => void;
};

/**
 * A row's text field (list-editor.md *Anatomy*): its label; its message, while it has one; a visually hidden hint of
 * the limit (conditional-reveal.md's `-hint`); the shared text box, three lines high and growing with its text, in the
 * reading column, with no `maxLength`, so that a paste over the limit is kept whole; and the count under it, which
 * counts the text as Add or Save sends it. The field is described by its message, then the hint, then the help text
 * (common-items.md *Conflicts resolved* 12), never by the count, which changes at each keystroke and has a twin of its
 * own. Past the limit, the box's border and the count take the error colour, which is not yet an error.
 */
function ItemTextField({
  fieldId,
  label,
  helpId,
  text,
  error,
  inputs,
  readOnly = false,
  fieldRef,
  onChange,
}: ItemTextFieldProps) {
  const hintId = `${fieldId}-hint`;
  const length = cleanText(text).length;
  const description = [error === undefined ? undefined : `${fieldId}-error`, hintId, helpId].filter(
    (id) => id !== undefined,
  );

  return (
    <div className={styles.field}>
      <FieldGroup fieldId={fieldId} label={label} error={error}>
        <p id={hintId} className="visually-hidden">
          {msg.characterLimitHint(limits.itemText)}
        </p>
        {/* The copy behind the box, from data-value, sizes it without field-sizing (TextBox.module.css). */}
        <div className={textBox.grow} data-value={text}>
          <textarea
            ref={fieldRef}
            id={fieldId}
            rows={3}
            className={`${textBox.box} ${styles.box}`}
            value={text}
            readOnly={readOnly}
            autoComplete="off"
            autoCapitalize="sentences"
            aria-describedby={description.join(' ')}
            aria-invalid={error === undefined ? undefined : true}
            data-over-limit={length > limits.itemText || undefined}
            onChange={(event) => onChange(event.currentTarget.value)}
          />
        </div>
        <CharacterCount length={length} limit={limits.itemText} inputs={inputs} />
      </FieldGroup>
    </div>
  );
}
