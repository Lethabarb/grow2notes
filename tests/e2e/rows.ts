import type { Request } from '@playwright/test';

/** A row of an ordered list as the API lists it (design.md §6.6): one of a participant's goals, or a common item. */
export type ListedRow = {
  id: string;
  text: string;
  sortOrder: number;
  /** In UTC, with `Z`, or `null` while the row is active. */
  archivedAtUtc: string | null;
  /** The row's `RowVersion` as base64, which a change to the row sends as `If-Match` (D78). */
  rowVersion: string;
};

let versionsGiven = 0;

/** A row version not given before, as base64 of 8 bytes, as the API gives a `RowVersion`. */
export function newVersion(): string {
  versionsGiven += 1;
  const bytes = Buffer.alloc(8);
  bytes.writeBigUInt64BE(BigInt(0x7d0 + versionsGiven));
  return bytes.toString('base64');
}

let archivesGiven = 0;

/**
 * The time of an archive done in the browser: a minute on from the one before, from 4:12 pm in Melbourne on
 * 1 October 2026, so that each is newer than the made-up rows' and than the one before it.
 */
function archivedNow(): string {
  archivesGiven += 1;
  return new Date(Date.UTC(2026, 9, 1, 6, 12 + archivesGiven)).toISOString();
}

/** A made-up row (A35), active unless given the time at which it was archived, with a version of its own. */
export function madeUpRow(id: string, text: string, sortOrder: number, archivedAtUtc: string | null = null): ListedRow {
  return { id, text, sortOrder, archivedAtUtc, rowVersion: newVersion() };
}

/** An RFC 9457 problem with the API's stable `code` (design.md §6.9). */
export const problem = (status: number, code: string) => ({ status, json: { status, code } });

/**
 * The answer to a change to `row`, a list's row or a common item group, whose `If-Match` is not the row's version
 * (design.md §6.7): `428` without one and `412 precondition.failed` with another; or `undefined` when it is the row's,
 * so that the change goes ahead.
 */
export function refusedPrecondition(request: Request, row: Pick<ListedRow, 'rowVersion'>) {
  const ifMatch = request.headers()['if-match'];
  if (ifMatch === undefined) {
    return problem(428, 'precondition.required');
  }

  return ifMatch === `"${row.rowVersion}"` ? undefined : problem(412, 'precondition.failed');
}

/**
 * One ordered list's rows held in the browser as the API keeps them (design.md §5.4, §6.7): copies of rows given,
 * listed by `SortOrder`; a row added, restored or moved in from another list at one more than the highest active
 * `SortOrder` (A2, A42); an archive that keeps the row's place; and a reorder of the active rows, each once. A row
 * whose words, place or archive change takes a new version, as its `RowVersion` would, so that the page meets what
 * the API would say to its next write; and a change that leaves a row as it was saves nothing (D84).
 */
export class HeldRows {
  readonly #rows: ListedRow[];

  constructor(rows: readonly ListedRow[]) {
    this.#rows = rows.map((row) => ({ ...row }));
  }

  /** The rows by `SortOrder`, sorted stably, so that rows of one `SortOrder` keep the order in which they came. */
  listed(): ListedRow[] {
    return this.#rows.toSorted((a, b) => a.sortOrder - b.sortOrder);
  }

  /** The row of ID `id`, or `undefined` for one that the list does not hold. */
  find(id: string): ListedRow | undefined {
    return this.#rows.find((row) => row.id === id);
  }

  /** Adds an active row of `text` at the end. */
  add(id: string, text: string): void {
    this.#rows.push({ id, text, sortOrder: this.#end(), archivedAtUtc: null, rowVersion: newVersion() });
  }

  /** Rewords the row. */
  reword(row: ListedRow, text: string): void {
    if (text !== row.text) {
      this.#change(row, { text });
    }
  }

  /** Takes the row out of the list, as a move to another list does. */
  remove(row: ListedRow): void {
    this.#rows.splice(this.#rows.indexOf(row), 1);
  }

  /** Puts a row that a move took out of another list at the end of this one, keeping its words and archived state. */
  moveIn(row: ListedRow): void {
    this.#rows.push({ ...row, sortOrder: this.#end(), rowVersion: newVersion() });
  }

  /** Archives the row, which keeps its `SortOrder`, as of now. */
  archive(row: ListedRow): void {
    if (row.archivedAtUtc === null) {
      this.#change(row, { archivedAtUtc: archivedNow() });
    }
  }

  /** Restores the row to the end of the active rows. */
  restore(row: ListedRow): void {
    if (row.archivedAtUtc !== null) {
      this.#change(row, { archivedAtUtc: null, sortOrder: this.#end() });
    }
  }

  /**
   * Puts the active rows in the order of `ids`, giving whether it could: only when `ids` holds each active row's ID
   * once and nothing else, as the API's reorder checks the set (design.md §6.7).
   */
  reorder(ids: readonly string[]): boolean {
    const active = this.listed().filter((row) => row.archivedAtUtc === null);
    const eachOnce =
      ids.length === active.length && new Set(ids).size === ids.length && active.every((row) => ids.includes(row.id));
    if (!eachOnce) {
      return false;
    }

    for (const [sortOrder, id] of ids.entries()) {
      const row = this.find(id);
      if (row !== undefined && row.sortOrder !== sortOrder) {
        this.#change(row, { sortOrder });
      }
    }

    return true;
  }

  #end(): number {
    return Math.max(-1, ...this.#rows.filter((row) => row.archivedAtUtc === null).map((row) => row.sortOrder)) + 1;
  }

  #change(row: ListedRow, values: Partial<ListedRow>): void {
    Object.assign(row, values, { rowVersion: newVersion() });
  }
}
