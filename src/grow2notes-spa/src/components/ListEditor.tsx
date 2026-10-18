import { useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { useId, useRef, useState, type FormEvent, type MouseEvent, type Ref, type RefObject } from 'react';
import { flushSync } from 'react-dom';
import { ApiError } from '../api/api.ts';
import { msg } from '../copy/messages.ts';
import { strings } from '../copy/strings.ts';
import { limits } from '../limits.ts';
import { Button } from './Button.tsx';
import { CharacterCount } from './CharacterCount.tsx';
import { ChoiceRow } from './ChoiceRow.tsx';
import { FieldGroup } from './FieldGroup.tsx';
import styles from './ListEditor.module.css';
import { useListFrame } from './listFrameContext.ts';
import {
  cleanText,
  failureAlert,
  focusStayedIn,
  ListChanged,
  sendUnlessListChanged,
  useFieldText,
  useListWrite,
  type ListWriter,
} from './listWrite.ts';
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
   * Rewords a row, with its `rowVersion` as `If-Match`, as the API's `PUT` does, and, on Common items, puts it in the
   * group of `groupId`, its own unless the Group radios chose another (`RowGroups`); the editor reads nothing of the
   * answer, as it reads the row's next version, and its group, from the list.
   */
  update: (id: string, text: string, rowVersion: string, groupId?: string) => Promise<unknown>;
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

/** A group that a row of Common items can be moved to, as the Group radios name it (common-items.md component 6). */
type GroupChoice = { id: string; name: string };

/**
 * How the rows of a group's list on Common items move to another group (common-items.md component 6), which no other
 * list does: the list's own group, which each of its rows is in; the active groups, in the list's order, Every note
 * first, as the page's one query holds them, which a row's edit form offers as its Group radios while there are two or
 * more; and the `name` of a row's radios, given the row's ID, `item-{id}-group`.
 */
type RowGroups = {
  own: string;
  active: GroupChoice[];
  fieldName: (rowId: string) => string;
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
  /**
   * The add field's id where its page names it, such as a group's `item-new-{groupId}` on Common items
   * (common-items.md component 5); otherwise one of React's own.
   */
  addFieldId?: string;
  /**
   * A row's edit field's id, given the row's ID, where its page names it, such as `item-{id}-text` on Common items
   * (common-items.md component 6); otherwise one of React's own.
   */
  editFieldId?: (rowId: string) => string;
  /** On Common items alone, the groups that a row can be moved to from its edit form. */
  groups?: RowGroups;
  /** For an archived participant's goals (participants.md B6): the rows' text alone, with no controls. */
  readOnly?: boolean;
};

/**
 * What a move needs of the list that it moves a row or a group in (list-editor.md *Behaviour*, common-items.md *Group
 * actions*): what every write needs of the list (`ListWriter`), its scope, the read that follows each write and the
 * list conflict; focus that follows a change; the order as last read, which a move reads as it runs; and the write of a
 * new order.
 */
export type MoveList = ListWriter & {
  /** Registers an element under `key`, such as a row's Edit under `<id>:edit`, for `focusAfterRender`. */
  focusTarget: (key: string) => (element: HTMLElement | null) => void;
  /** Focuses the element registered under `key` once the render that a change brings is in the DOM. */
  focusAfterRender: (key: string) => void;
  /** The IDs of the list's active rows, or of its groups but Every note, in their order as last read. */
  order: () => string[];
  /** Puts the rows or the groups in the order of `ids`, each of `order`'s IDs once, as the API's `PUT …/order` does. */
  reorder: (ids: string[]) => Promise<unknown>;
};

/**
 * What the editor's rows and forms share with it: what a move needs of the list (`MoveList`), most of which every other
 * write needs too; the list's kind, name and writes; the ids of its rows' edit fields where its page names them, the
 * groups that a row can be moved to, on Common items, and the id of the page's help text, which describes each text
 * field; the rows as last read; and the list's archived section that an Archive opens.
 */
type ListContext = MoveList & {
  kind: ListKind;
  name: string | undefined;
  api: ListWrites;
  editFieldId: ((rowId: string) => string) | undefined;
  groups: RowGroups | undefined;
  helpId: string;
  /** The rows as the list was last read, which a write reads as it runs. */
  latest: () => ListRow[];
  /**
   * Opens the archived section once the render that an Archive brings is in the DOM, as it may have only just
   * appeared, then focuses the element registered under `key`, if one is given.
   */
  openArchivedAfterRender: (key: string | undefined) => void;
};

/** Which way a move button moves its row or its group. */
type Direction = 'up' | 'down';

/** Each move button's words: its label, and what a press says where its row or group is already at that end. */
const moveWords = {
  up: { label: strings.listRows.moveUp, atEnd: strings.listRows.atTop },
  down: { label: strings.listRows.moveDown, atEnd: strings.listRows.atBottom },
} as const;

/**
 * A row's wording now, as the edit conflict gives it for each kind of list, each as its screen's spec words it: a
 * goal's as it is (participants.md *States*), and a common item's in typographic quotes (common-items.md *States*).
 */
const wordingNow: Record<ListKind, (text: string) => string> = {
  goal: (text) => text,
  item: msg.quoted,
};

// Each move button's arrow, filled with the button's colour, which forced colours give it too.
const arrowPaths = {
  up: 'M10 2.5 3.5 9h4.25v8.5h4.5V9h4.25z',
  down: 'M10 17.5 16.5 11h-4.25V2.5h-4.5V11H3.5z',
} as const;

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
 * The rule for a row's text, of the list's kind (form-validation.md): the message for the text as cleaned, none left
 * or more than `limits.itemText` characters, counted in UTF-16 units, as the server counts them; or nothing once it
 * passes.
 */
function textRule(kind: ListKind): (cleaned: string) => string | undefined {
  const copy = strings.listEditor[kind];
  return (cleaned) => {
    if (cleaned === '') {
      return copy.enter;
    }

    return cleaned.length > limits.itemText ? msg.tooLong(copy.text, limits.itemText) : undefined;
  };
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
export function ListEditor<TData>({
  kind,
  api,
  name,
  addFieldId,
  editFieldId,
  groups,
  readOnly = false,
}: ListEditorProps<TData>) {
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

  const latest = () => {
    const data = queryClient.getQueryData<TData>(api.query.queryKey);
    return data === undefined ? [] : api.rowsOf(data);
  };

  const list: ListContext = {
    scopeId: api.scopeId,
    kind,
    name,
    api,
    editFieldId,
    groups,
    helpId: frame.helpId,
    latest,
    order: () => latest().flatMap((listed) => (listed.archivedAtUtc === null ? [listed.id] : [])),
    reorder: api.reorder,
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
      {!readOnly && <AddForm list={list} fieldId={addFieldId} />}
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
              <MovePair
                list={list}
                id={row.id}
                first={first}
                last={last}
                context={msg.rowContext(row.text)}
                moved={msg.moved}
                onFailure={setFailure}
              />
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

type MovePairProps = Omit<MoveButtonProps, 'direction' | 'atEnd'> & {
  /** Whether the row or the group is the first in its order, where Move up is unavailable. */
  first: boolean;
  /** Whether the row or the group is the last in its order, where Move down is unavailable. */
  last: boolean;
};

/**
 * A row's or a group's first pair of buttons (list-editor.md *Anatomy*, common-items.md component 5), Move up, then
 * Move down, which share a line.
 */
export function MovePair({ first, last, ...move }: MovePairProps) {
  return (
    <div className={styles.pair}>
      <MoveButton {...move} direction="up" atEnd={first} />
      <MoveButton {...move} direction="down" atEnd={last} />
    </div>
  );
}

type MoveButtonProps = {
  list: MoveList;
  /** The ID of the row or the group that the button moves. */
  id: string;
  direction: Direction;
  /** Whether the row or the group is already at the end that the button moves it towards, where it is unavailable. */
  atEnd: boolean;
  /** After the label, for screen readers alone: the row's text, or the group's name, with `msg`'s comma before it. */
  context: string;
  /**
   * What the page's status region says once the list shows the move, given the way it went and the place it went to,
   * counted from 1 among the `total` in the order.
   */
  moved: (direction: Direction, position: number, total: number) => string;
  /** Shows a move's failure in the row's or the group's alert, or, given `undefined`, empties it. */
  onFailure: (message: string | undefined) => void;
};

/**
 * A row's or a group's Move up or Move down (list-editor.md *Behaviour*, common-items.md *Group actions*), in words
 * after a decorative arrow, with the row's text or the group's name after them for screen readers alone. At the end of
 * the order it stays in place, unavailable (`aria-disabled`, dashed), and a press sends nothing and says why in the
 * page's status region. Otherwise a press is busy at once, "Moving…" after 400 ms, and waits its turn in the list's
 * scope; when it runs, it swaps the row or the group with its neighbour in the order as last read, and sends that whole
 * order, the page showing the new order only once it has been read again. Then focus goes back to the button, which
 * may have moved with its row or its whole section, unless the person has gone elsewhere meanwhile, and the page's
 * status region says where it went. A move that an earlier one has taken to the end sends nothing, and says so as an
 * unavailable press does; one whose row or group has left the order, or that the server refuses `422` as the order has
 * changed, shows the list conflict over the list read again; and any other failure says so in the alert, focus staying
 * on the button.
 */
function MoveButton({ list, id, direction, atEnd, context, moved, onFailure }: MoveButtonProps) {
  const announce = useAnnounce();
  const words = moveWords[direction];
  const key = `${id}:${direction}`;
  const move = useListWrite(list, async () => {
    // The order as the write runs, which the list read after the write before it may have changed.
    const ids = list.order();
    const from = ids.indexOf(id);
    if (from === -1) {
      throw new ListChanged();
    }

    const to = direction === 'up' ? from - 1 : from + 1;
    if (to < 0 || to === ids.length) {
      throw new AlreadyAtEnd();
    }

    [ids[from], ids[to]] = [ids[to], ids[from]];
    await sendUnlessListChanged(list, 422, () => list.reorder(ids));
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

        announce?.(moved(direction, position, total));
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
      <span className="visually-hidden">{context}</span>
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
 * A row's edit form (list-editor.md *Behaviour*, participants.md B6, common-items.md component 6): the row's text
 * field, filled with its text, then, on Common items while two or more groups are active, the Group radios, the row's
 * own group chosen, then the alert for a request that failed, then Save, primary, and Cancel, which drops what was
 * typed and chosen, both label width. Save checks the text as Add does, sending nothing while it fails; then, when the
 * text as Save would send it is the row's own, and its group too, it closes the form as Cancel does and sends nothing,
 * as there is nothing to save. Otherwise it sends the text, and the group chosen, with the row's version as the write
 * runs, the box read-only while it is out, and once the list is read again, closes the form, focus going to Edit,
 * which a row moved to another group has at the end of that group's list, unless the person has gone elsewhere
 * meanwhile, and the page's status region says so, naming the group that a moved row went to. A failure keeps the text
 * and the group chosen, focus staying on Save: the server's refusal of the text shows on the field as Save's own does;
 * a Save refused as stale whose row is still in the list says what the row now says, and the next Save sends its new
 * version; a row archived or moved meanwhile leaves the list, and its form with it, and the list conflict shows, as it
 * does for a Save refused as the row's group, or the group chosen, was archived meanwhile (common-items.md
 * *Failures*), a group chosen that is no longer active leaving the radios, which then have the row's own chosen; and
 * anything else says so in the alert. Cancel stays free while a Save is out or queued, closing the form, as a write
 * once started runs to its end and the list then shows what the server holds.
 */
function EditForm({ row, list, field, onClose }: EditFormProps) {
  const copy = strings.listEditor[list.kind];
  const announce = useAnnounce();
  const ownId = useId();
  const fieldId = list.editFieldId?.(row.id) ?? ownId;
  const form = useRef<HTMLFormElement>(null);
  const box = useFieldText(row.text, textRule(list.kind), 'text', field);
  // The group chosen in the Group radios, the row's own until another is chosen, on Common items alone.
  const [groupId, setGroupId] = useState(list.groups?.own);
  // The group chosen while it is still active, or else the row's own, as a group archived meanwhile leaves the radios.
  const chosen = list.groups?.active.some(({ id }) => id === groupId) ? groupId : list.groups?.own;
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const save = useListWrite(list, async ({ text, to }: { text: string; to: string | undefined }) => {
    // The version as the write runs, which the list read after the write before it may have changed.
    const current = activeRow(list.latest(), row.id);
    if (current === undefined) {
      throw new ListChanged();
    }

    try {
      await sendUnlessListChanged(list, 409, () => list.api.update(row.id, text, current.rowVersion, to));
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

    if (sent === row.text && chosen === list.groups?.own) {
      onClose(true);
      return;
    }

    // The group as its radio names it as Save is pressed, or nothing while the row stays in its own.
    const movedTo = chosen === list.groups?.own ? undefined : list.groups?.active.find(({ id }) => id === chosen);
    save.mutate(
      { text: sent, to: chosen },
      {
        onSuccess: () => {
          onClose(focusStayedIn(form.current));
          announce?.(movedTo === undefined ? copy.saved : msg.itemSavedAndMoved(movedTo.name));
        },
        onError: (failed) => {
          if (failed instanceof RowChanged) {
            setFailure(msg.rowChangedWhileEditing(copy.row, wordingNow[list.kind](failed.text)));
            return;
          }

          if (!box.refusedByServer(failed)) {
            setFailure(failureAlert(failed, msg.notSaved));
          }
        },
      },
    );
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
      {list.groups !== undefined && list.groups.active.length >= 2 && (
        <GroupRadios
          name={list.groups.fieldName(row.id)}
          groups={list.groups.active}
          chosen={chosen}
          onChoose={setGroupId}
        />
      )}
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

type GroupRadiosProps = {
  /** The `name` of every radio in the set, `item-{id}-group`, which each radio's id starts with. */
  name: string;
  groups: GroupChoice[];
  /** The ID of the group chosen, whose radio is checked. */
  chosen: string | undefined;
  onChoose: (groupId: string) => void;
};

/**
 * An item's Group radios (common-items.md component 6): a `<fieldset>` whose legend is "Group", then a `ChoiceRow` for
 * each active group, in the list's order, Every note first, named by the group's name alone, the group chosen checked.
 * One Tab stop, in which the arrow keys move and choose, as the browser's own radios do; choosing sends nothing until
 * Save (SC 3.2.2). Radios, not a `<select>`, which some find very hard to use (GOV.UK).
 */
function GroupRadios({ name, groups, chosen, onChoose }: GroupRadiosProps) {
  return (
    <div className={styles.field}>
      <FieldGroup fieldId={name} label={strings.commonItems.group} group>
        {groups.map((group) => (
          <ChoiceRow
            key={group.id}
            id={`${name}-${group.id}`}
            name={name}
            label={group.name}
            checked={group.id === chosen}
            onChange={() => onChoose(group.id)}
          />
        ))}
      </FieldGroup>
    </div>
  );
}

/**
 * The add form (list-editor.md *Behaviour*, participants.md B6): the row's text field, then the alert for a request
 * that failed, then Add, primary. Add checks the text alone, in form-validation.md's one-field pattern, and sends
 * nothing while it fails: the message above the field, and focus on it, with no error summary and no "Error: " in the
 * title. Sent, the row goes to the end of the list, which is read again before Add is free, then the field is emptied
 * if it still holds the text sent, with focus kept in it for the next one, and the page's status region says so; text
 * typed while the add was out stays. A failure keeps the text and says so in the alert, focus staying on Add, but for
 * the server's refusal of the text, which shows on the field as Add's own does, and for an add refused as the list's
 * group was archived meanwhile, which shows the list conflict over the list read again. The field takes focus that
 * follows a change by its id, as a new group's does after Add group (common-items.md *Group actions*).
 */
function AddForm({ list, fieldId }: { list: ListContext; fieldId: string | undefined }) {
  const copy = strings.listEditor[list.kind];
  const announce = useAnnounce();
  const ownId = useId();
  const id = fieldId ?? ownId;
  const focusTarget = list.focusTarget(id);
  const form = useRef<HTMLFormElement>(null);
  const field = useRef<HTMLTextAreaElement>(null);
  const box = useFieldText('', textRule(list.kind), 'text', field);
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const add = useListWrite(list, (text: string) => sendUnlessListChanged(list, 409, () => list.api.add(text)));

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
        fieldId={id}
        label={list.name === undefined ? copy.newLabel : `${copy.newLabel}${msg.inList(list.name)}`}
        helpId={list.helpId}
        text={box.text}
        error={box.error}
        inputs={box.inputs}
        fieldRef={(element) => {
          field.current = element;
          focusTarget(element);
        }}
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
  fieldRef: Ref<HTMLTextAreaElement>;
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
