import { useQuery, type UseQueryOptions } from '@tanstack/react-query';
import { strings } from '../copy/strings.ts';
import styles from './ListEditor.module.css';
import { RequestAlert } from './RequestAlert.tsx';

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
 * archived, in the API's order.
 */
export type ListApi = {
  query: UseQueryOptions<ListRow[], Error, ListRow[], string[]>;
};

type ListEditorProps = {
  kind: ListKind;
  api: ListApi;
  /** For an archived participant's goals (participants.md B6): the rows' text alone, with no help text or controls. */
  readOnly?: boolean;
};

/**
 * An ordered list that managers edit (list-editor.md *Anatomy*, *States*): the help text; the active rows as a
 * numbered `<ol>`, in the API's order, which the editor never sorts again, as the API breaks ties in `SortOrder` as
 * the reorder reads them, each row its text, wrapping anywhere, then its alert slot, always rendered; or, with no
 * active row, the kind's empty sentence; then the archived rows, newest archived first, as a `<ul>` in a `<details>`,
 * closed as it loads and left to the person from then on, which is left out while none is archived (decision 7).
 * Read-only, the help text and the alert slots are left out, as nothing can be changed.
 *
 * The page loads the list before rendering the editor, in its load region, or puts it in the query itself, as Add
 * participant does with a new participant's empty one, so the editor reads it from the query and never fetches it as
 * it mounts.
 */
export function ListEditor({ kind, api, readOnly = false }: ListEditorProps) {
  const copy = strings.listEditor[kind];
  const { data: rows = [] } = useQuery({ ...api.query, refetchOnMount: false });
  const active = rows.filter((row) => row.archivedAtUtc === null);
  const archived = rows
    .flatMap((row) => (row.archivedAtUtc === null ? [] : [{ row, archivedAt: Date.parse(row.archivedAtUtc) }]))
    .sort((a, b) => b.archivedAt - a.archivedAt)
    .map(({ row }) => row);

  return (
    <>
      {!readOnly && <p className={styles.help}>{copy.help}</p>}
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
