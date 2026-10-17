import { useMutation, useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { useEffect, useId, useRef, useState, type FormEvent, type Ref } from 'react';
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
};

type ListEditorProps = {
  kind: ListKind;
  api: ListApi;
  /** For an archived participant's goals (participants.md B6): the rows' text alone, with no help text or controls. */
  readOnly?: boolean;
};

/**
 * A row's text as Add sends it (list-editor.md *Implementation notes*): each line break, with the spaces around it, as
 * one space, and the ends trimmed, as the server cleans it too.
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

/**
 * An ordered list that managers edit (list-editor.md *Anatomy*, *States*): the help text; the active rows as a
 * numbered `<ol>`, in the API's order, which the editor never sorts again, as the API breaks ties in `SortOrder` as
 * the reorder reads them, each row its text, wrapping anywhere, then its alert slot, always rendered; or, with no
 * active row, the kind's empty sentence; then the add form; then the archived rows, newest archived first, as a `<ul>`
 * in a `<details>`, closed as it loads and left to the person from then on, which is left out while none is archived
 * (decision 7). Read-only, the help text, the alert slots and the add form are left out, as nothing can be changed.
 *
 * The page loads the list before rendering the editor, in its load region, or puts it in the query itself, as Add
 * participant does with a new participant's empty one, so the editor reads it from the query and never fetches it as
 * it mounts.
 */
export function ListEditor({ kind, api, readOnly = false }: ListEditorProps) {
  const copy = strings.listEditor[kind];
  const helpId = useId();
  const { data: rows = [] } = useQuery({ ...api.query, refetchOnMount: false });
  const active = rows.filter((row) => row.archivedAtUtc === null);
  const archived = rows
    .flatMap((row) => (row.archivedAtUtc === null ? [] : [{ row, archivedAt: Date.parse(row.archivedAtUtc) }]))
    .sort((a, b) => b.archivedAt - a.archivedAt)
    .map(({ row }) => row);

  return (
    <>
      {!readOnly && (
        <p id={helpId} className={styles.help}>
          {copy.help}
        </p>
      )}
      {active.length === 0 ? (
        <p>{copy.empty}</p>
      ) : (
        <ol className={styles.list}>
          {active.map((row) => (
            <li key={row.id} className={styles.row}>
              <div className={styles.rowInner}>
                <p className={styles.text}>{row.text}</p>
                {!readOnly && (
                  <div className={styles.side}>
                    <RequestAlert />
                  </div>
                )}
              </div>
            </li>
          ))}
        </ol>
      )}
      {!readOnly && <AddForm kind={kind} api={api} helpId={helpId} />}
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
 * A write to the list (list-editor.md *Implementation notes*): in the list's one scope, so that it waits for the write
 * before it; sent once, never again by itself, as an add sent again after a lost answer would add the row twice; then
 * the list read again before the next write starts, as the screen shows a change only once the server has it.
 */
function useListWrite<T>(api: ListApi, send: (variables: T) => Promise<unknown>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (variables: T) => {
      await send(variables);
      await queryClient.refetchQueries({ queryKey: api.query.queryKey, exact: true });
    },
    scope: { id: api.scopeId },
    retry: 0,
  });
}

type AddFormProps = { kind: ListKind; api: ListApi; helpId: string };

/**
 * The add form (list-editor.md *Behaviour*, participants.md B6): the row's text field, then the alert for a request
 * that failed, then Add, primary. Add checks the text alone, in form-validation.md's one-field pattern, and sends
 * nothing while it fails: the message above the field, and focus on it, with no error summary and no "Error: " in the
 * title; once the message shows, each input checks the text again, and it goes, silently, once the text passes. Sent,
 * the row goes to the end of the list, which is read again before Add is free, then the field is emptied, with focus
 * kept in it for the next one, and the page's status region says so. A failure keeps the text and says so in the
 * alert, focus staying on Add, but for the server's refusal of the text, which shows on the field as Add's own does.
 */
function AddForm({ kind, api, helpId }: AddFormProps) {
  const copy = strings.listEditor[kind];
  const announce = useAnnounce();
  const fieldId = useId();
  const form = useRef<HTMLFormElement>(null);
  const field = useRef<HTMLTextAreaElement>(null);
  // As typed, in this page's memory only (D22).
  const [text, setText] = useState('');
  const [inputs, setInputs] = useState(0);
  const [error, setError] = useState<string>();
  // The presses that found the text refused, so that each moves focus to the field, even with the same message.
  const [refusals, setRefusals] = useState(0);
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const add = useListWrite(api, api.add);

  // After the render that names the message in the field's description, so that a screen reader reads it with focus.
  useEffect(() => {
    if (refusals > 0) {
      field.current?.focus();
    }
  }, [refusals]);

  function edit(next: string) {
    setText(next);
    setInputs((count) => count + 1);
    setError((shown) => (shown === undefined ? undefined : textError(cleanText(next), kind)));
  }

  function refuse(message: string) {
    setError(message);
    setRefusals((presses) => presses + 1);
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    const sent = cleanText(text);
    const refused = textError(sent, kind);
    if (refused !== undefined) {
      refuse(refused);
      return;
    }

    add.mutate(sent, {
      onSuccess: () => {
        // Emptied only while it still holds what was sent, so that nothing typed while the add was out is lost.
        setText((current) => (cleanText(current) === sent ? '' : current));
        // Unless the person has moved on while the add was out, as focus is never pulled back (common-items.md
        // *Queueing*).
        const focused = document.activeElement;
        if (focused === null || focused === document.body || form.current?.contains(focused)) {
          field.current?.focus();
        }

        announce?.(copy.added);
      },
      onError: (failed) => {
        const refusal = failed instanceof ApiError && failed.status === 422 ? failed.errors?.text?.[0] : undefined;
        if (refusal !== undefined) {
          refuse(refusal);
          return;
        }

        // A 401 has the session flow, which `api()` started, and shows nothing here.
        const cause = classify(failed);
        if (cause !== 'signedOut') {
          setFailure(msg.notAdded(cause));
        }
      },
    });
  }

  return (
    <form ref={form} noValidate className={`${textBox.column} ${styles.textForm}`} onSubmit={submit}>
      <ItemTextField
        fieldId={fieldId}
        label={copy.newLabel}
        helpId={helpId}
        text={text}
        error={error}
        inputs={inputs}
        fieldRef={field}
        onChange={edit}
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
  fieldRef: Ref<HTMLTextAreaElement>;
  onChange: (text: string) => void;
};

/**
 * A row's text field (list-editor.md *Anatomy*): its label; its message, while it has one; a visually hidden hint of
 * the limit (conditional-reveal.md's `-hint`); the shared text box, three lines high and growing with its text, in the
 * reading column, with no `maxLength`, so that a paste over the limit is kept whole; and the count under it, which
 * counts the text as Add sends it. The field is described by its message, then the hint, then the help text
 * (common-items.md *Conflicts resolved* 12), never by the count, which changes at each keystroke and has a twin of its
 * own. Past the limit, the box's border and the count take the error colour, which is not yet an error.
 */
function ItemTextField({ fieldId, label, helpId, text, error, inputs, fieldRef, onChange }: ItemTextFieldProps) {
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
