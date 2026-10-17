import { useMutation, useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { useEffect, useId, useLayoutEffect, useRef, useState, type FormEvent, type RefObject } from 'react';
import { flushSync } from 'react-dom';
import { ApiError, classify } from '../api/api.ts';
import { msg } from '../copy/messages.ts';
import { strings } from '../copy/strings.ts';
import { limits } from '../limits.ts';
import { Button } from './Button.tsx';
import { CharacterCount } from './CharacterCount.tsx';
import { FieldGroup } from './FieldGroup.tsx';
import styles from './ListEditor.module.css';
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

/**
 * How the editor reaches one list's API (list-editor.md *Implementation notes*): the query of its rows, active and
 * archived, in the API's order; the scope of its writes; and the writes themselves.
 */
export type ListApi = {
  query: UseQueryOptions<ListRow[], Error, ListRow[], string[]>;
  /** The TanStack mutation scope that every write on the list shares, such as `goals:<participantId>`. */
  scopeId: string;
  /** Adds a row of `text` at the end of the list, as the API's `POST` does; the editor reads nothing of the answer. */
  add: (text: string) => Promise<unknown>;
  /**
   * Rewords a row, with its `rowVersion` as `If-Match`, as the API's `PUT` does; the editor reads nothing of the
   * answer, as it reads the row's next version from the list.
   */
  update: (id: string, text: string, rowVersion: string) => Promise<unknown>;
};

type ListEditorProps = {
  kind: ListKind;
  api: ListApi;
  /** For an archived participant's goals (participants.md B6): the rows' text alone, with no help text or controls. */
  readOnly?: boolean;
};

/**
 * What the editor's rows and forms share with it: the list's kind and API; the id of its help text, which describes
 * each text field; the rows as last read, and the read that follows each write; focus that follows a change; and the
 * list conflict, which a write shows and the next write that succeeds takes away.
 */
type ListContext = {
  kind: ListKind;
  api: ListApi;
  helpId: string;
  /** The rows as the list was last read, which a write reads as it runs. */
  latest: () => ListRow[];
  /** Reads the list again, which a write waits for, so that the next write starts from what the server holds. */
  reread: () => Promise<void>;
  /** Registers an element under `key`, such as a row's Edit under `<id>:edit`, for `focusAfterRender`. */
  focusTarget: (key: string) => (element: HTMLElement | null) => void;
  /** Focuses the element registered under `key` once the render that a change brings is in the DOM. */
  focusAfterRender: (key: string) => void;
  /** Shows the list conflict above the list, and focuses it, as a write found the list changed by someone else. */
  changedMeanwhile: () => void;
  /** Takes the list conflict away, as a write on the list has succeeded. */
  written: () => void;
};

// The list conflict's key among the elements that take focus after a change.
const conflictKey = 'conflict';

/**
 * A write that found its row no longer active, as someone else archived it, once the list was read again; the list
 * conflict then shows, as the row has left the list with its form (list-editor.md *Request errors*).
 */
class ListChanged extends Error {
  override name = 'ListChanged';
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

/**
 * Whether focus is still in `form`, which started a write, or nowhere: only then does the write's end move it, as
 * focus is never pulled away from where the person has gone while the write was out (common-items.md *Queueing*).
 */
function focusStayedIn(form: HTMLFormElement | null): boolean {
  const focused = document.activeElement;
  return focused === null || focused === document.body || form?.contains(focused) === true;
}

/**
 * An ordered list that managers edit (list-editor.md *Anatomy*, *States*): the help text; then, after a write that
 * found the list changed by someone else, the list conflict, focused, until the next write on the list succeeds; the
 * active rows as a numbered `<ol>`, in the API's order, which the editor never sorts again, as the API breaks ties in
 * `SortOrder` as the reorder reads them; or, with no active row, the kind's empty sentence; then the add form; then
 * the archived rows, newest archived first, as a `<ul>` in a `<details>`, closed as it loads and left to the person
 * from then on, which is left out while none is archived (decision 7). Read-only, each active row is its text alone,
 * and the help text and the add form are left out, as nothing can be changed.
 *
 * The page loads the list before rendering the editor, in its load region, or puts it in the query itself, as Add
 * participant does with a new participant's empty one, so the editor reads it from the query and never fetches it as
 * it mounts.
 */
export function ListEditor({ kind, api, readOnly = false }: ListEditorProps) {
  const copy = strings.listEditor[kind];
  const helpId = useId();
  const queryClient = useQueryClient();
  const { data: rows = [] } = useQuery({ ...api.query, refetchOnMount: false });
  const [changed, setChanged] = useState(false);
  const focus = useFocusAfterRender();
  const active = rows.filter((row) => row.archivedAtUtc === null);
  const archived = rows
    .flatMap((row) => (row.archivedAtUtc === null ? [] : [{ row, archivedAt: Date.parse(row.archivedAtUtc) }]))
    .sort((a, b) => b.archivedAt - a.archivedAt)
    .map(({ row }) => row);

  const list: ListContext = {
    kind,
    api,
    helpId,
    latest: () => queryClient.getQueryData<ListRow[]>(api.query.queryKey) ?? [],
    reread: () => queryClient.refetchQueries({ queryKey: api.query.queryKey, exact: true }),
    focusTarget: focus.target,
    focusAfterRender: focus.request,
    changedMeanwhile: () => {
      setChanged(true);
      focus.request(conflictKey);
    },
    written: () => {
      setChanged(false);
    },
  };

  return (
    <>
      {!readOnly && (
        <p id={helpId} className={styles.help}>
          {copy.help}
        </p>
      )}
      {changed && (
        <p ref={focus.target(conflictKey)} tabIndex={-1} className={styles.conflict}>
          {strings.listRows.listChanged}
        </p>
      )}
      {active.length === 0 ? (
        <p>{copy.empty}</p>
      ) : (
        <ol className={styles.list}>
          {active.map((row) =>
            readOnly ? (
              <li key={row.id} className={styles.row}>
                <p className={styles.text}>{row.text}</p>
              </li>
            ) : (
              <ActiveRow key={row.id} row={row} list={list} />
            ),
          )}
        </ol>
      )}
      {!readOnly && <AddForm list={list} />}
      {archived.length > 0 && (
        <details className={styles.archived}>
          <summary className={styles.summary}>{copy.archivedSummary}</summary>
          <ul className={styles.archivedList}>
            {archived.map((row) => (
              <li key={row.id} className={styles.row}>
                <p className={styles.text}>{row.text}</p>
              </li>
            ))}
          </ul>
        </details>
      )}
    </>
  );
}

/**
 * Focus that follows the item (list-editor.md *Implementation notes*): each element that may take it registers under
 * a key, and a request focuses the one under its key in a layout effect, once the render that the change brings is in
 * the DOM and before it is painted, as React may have only just added the element, or moved it.
 */
function useFocusAfterRender() {
  const elements = useRef(new Map<string, HTMLElement>());
  // A new object at each request, so that a second request for the same key focuses it again.
  const [request, setRequest] = useState<{ key: string }>();

  useLayoutEffect(() => {
    if (request !== undefined) {
      elements.current.get(request.key)?.focus();
    }
  }, [request]);

  return {
    target: (key: string) => (element: HTMLElement | null) => {
      if (element === null) {
        elements.current.delete(key);
      } else {
        elements.current.set(key, element);
      }
    },
    request: (key: string) => {
      setRequest({ key });
    },
  };
}

/**
 * A write to the list (list-editor.md *Implementation notes*): in the list's one scope, so that it waits for the write
 * before it; sent once, never again by itself, as an add sent again after a lost answer would add the row twice; then
 * the list read again before the next write starts, as the screen shows a change only once the server has it. Once it
 * succeeds, the list conflict goes; once it finds the list changed, the conflict shows, though the form that sent it
 * may have gone with its row.
 */
function useListWrite<T>(list: ListContext, send: (variables: T) => Promise<unknown>) {
  return useMutation({
    mutationFn: async (variables: T) => {
      await send(variables);
      await list.reread();
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

type ActiveRowProps = { row: ListRow; list: ListContext };

/**
 * An active row (list-editor.md *Anatomy*, *Behaviour*): its text, wrapping anywhere, then its alert slot, always
 * rendered, and Edit, label width, with the row's text after its label for screen readers alone; or, once Edit is
 * pressed, the edit form in their place, in the same numbered item, until Save or Cancel closes it and focus goes back
 * to Edit, unless a Save finds it gone elsewhere as it ends. The form is rendered inside the press, so that focus goes
 * to its box, with the caret after the text, while the tap still runs, which is what lets a phone open its keyboard.
 * Its typed text is the row's own, so it stays as typed while other rows move.
 */
function ActiveRow({ row, list }: ActiveRowProps) {
  const [editing, setEditing] = useState(false);
  const field = useRef<HTMLTextAreaElement>(null);
  const editKey = `${row.id}:edit`;

  function edit() {
    flushSync(() => {
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

  return (
    <li className={styles.row}>
      {editing ? (
        <EditForm row={row} list={list} field={field} onClose={close} />
      ) : (
        <div className={styles.rowInner}>
          <p className={styles.text}>{row.text}</p>
          <div className={styles.side}>
            <RequestAlert />
            <div className={styles.actions}>
              <Button ref={list.focusTarget(editKey)} className={styles.rowButton} onClick={edit}>
                {strings.listRows.edit}
                <span className="visually-hidden">{msg.rowContext(row.text)}</span>
              </Button>
            </div>
          </div>
        </div>
      )}
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

        // A list conflict shows above the list, and a 401 has the session flow, which `api()` started.
        if (failed instanceof ListChanged || box.refusedByServer(failed)) {
          return;
        }

        const cause = classify(failed);
        if (cause !== 'signedOut') {
          setFailure(msg.notSaved(cause));
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
 * title. Sent, the row goes to the end of the list, which is read again before Add is free, then the field is emptied,
 * with focus kept in it for the next one, and the page's status region says so. A failure keeps the text and says so
 * in the alert, focus staying on Add, but for the server's refusal of the text, which shows on the field as Add's own
 * does.
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
        // A 401 has the session flow, which `api()` started, and shows nothing here.
        if (box.refusedByServer(failed)) {
          return;
        }

        const cause = classify(failed);
        if (cause !== 'signedOut') {
          setFailure(msg.notAdded(cause));
        }
      },
    });
  }

  return (
    <form ref={form} noValidate className={`${textBox.column} ${styles.addForm}`} onSubmit={submit}>
      <ItemTextField
        fieldId={fieldId}
        label={copy.newLabel}
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
