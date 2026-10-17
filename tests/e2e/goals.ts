import { randomUUID } from 'node:crypto';
import type { Page, Route } from '@playwright/test';

/** A goal as `GET /api/admin/participants/{participantId}/goals` lists it (design.md §6.6). */
export type AdminGoal = {
  id: string;
  text: string;
  sortOrder: number;
  /** In UTC, with `Z`, or `null` while the goal is active. */
  archivedAtUtc: string | null;
  /** The row's `RowVersion` as base64, which a change to the goal sends as `If-Match` (D78). */
  rowVersion: string;
};

let versionsGiven = 0;

/** A row version not given before, as base64 of 8 bytes, as the API gives a `RowVersion`. */
function newVersion(): string {
  versionsGiven += 1;
  const bytes = Buffer.alloc(8);
  bytes.writeBigUInt64BE(BigInt(0x7d0 + versionsGiven));
  return bytes.toString('base64');
}

let archivesGiven = 0;

/**
 * The time of an archive done in the browser: a minute on from the one before, from 4:12 pm in Melbourne on
 * 1 October 2026, so that each is newer than the made-up goals' and than the one before it.
 */
function archivedNow(): string {
  archivesGiven += 1;
  return new Date(Date.UTC(2026, 9, 1, 6, 12 + archivesGiven)).toISOString();
}

/** A made-up goal (A35), active unless given the time at which it was archived. */
export function madeUpGoal(
  id: string,
  text: string,
  sortOrder: number,
  archivedAtUtc: string | null = null,
): AdminGoal {
  return { id, text, sortOrder, archivedAtUtc, rowVersion: newVersion() };
}

// participants.md's sketch of a participant's goals (*Layout - phone*), and list-editor.md's two archived ones.
export const breakfast = madeUpGoal('6a1d9e4b-3c27-4f85-b0e6-2d8f5a7c1b93', 'Makes own breakfast', 0);
export const bus = madeUpGoal(
  '7b2e0f5c-4d38-4a96-8c17-3e9a6b8d2c04',
  'Catches the bus to day program and phones when she arrives',
  1,
);
export const sister = madeUpGoal('8c3f1a6d-5e49-4ba7-9d28-4f0b7c9e3d15', 'Phones her sister', 2);
export const washing = madeUpGoal(
  '9d4a2b7e-6f5a-4cb8-ae39-5a1c8d0f4e26',
  'Uses the washing machine',
  1,
  '2026-09-21T05:12:00Z',
);
export const shop = madeUpGoal('0e5b3c8f-7a6b-4dc9-bf4a-6b2d9e1a5f37', 'Walks to the shop', 3, '2026-09-14T05:12:00Z');

/** The five, in the API's order, by `SortOrder` (design.md §6.6): three active, with two archived among them. */
export const goals: readonly AdminGoal[] = [breakfast, bus, washing, sister, shop];

/**
 * The addresses of a move's and an Archive's writes (design.md §6.6), for a route ahead of `serveGoals`'s that holds
 * the page's next one, as `holdNext` does, or fails it. A held write reaches `serveGoals` through `route.fallback()`.
 */
export const goalWrites = {
  reorder: '**/api/admin/participants/*/goals/order',
  archive: '**/api/admin/goals/*/archive',
} as const;

// A participant's goals, which the list reads and an add posts to; their order; and a goal, which a reword puts to,
// with its archive and restore after it.
const goalsPath = /^\/api\/admin\/participants\/[^/]+\/goals$/;
const orderPath = /^\/api\/admin\/participants\/[^/]+\/goals\/order$/;
const goalPath = /^\/api\/admin\/goals\/([^/]+)(?:\/(archive|restore))?$/;

/** An RFC 9457 problem with the API's stable `code` (design.md §6.9). */
const problem = (status: number, code: string) => ({ status, json: { status, code } });

/** Whether `sent` holds each of `ids` once and nothing else, as a reorder must (design.md §6.7). */
const eachOnce = (sent: readonly string[], ids: readonly string[]) =>
  sent.length === ids.length && new Set(sent).size === ids.length && sent.every((id) => ids.includes(id));

/** What a test does to the goals that `serveGoals` holds, as another manager would. */
export type GoalStore = {
  /** Rewords the goal, or archives it, with a new version, so that the page's next write to it is stale. */
  changeElsewhere: (id: string, change: { text: string } | { archived: true }) => void;
};

/**
 * Answers each participant's goals in the browser from a list in memory, a copy of `goals`, as the API keeps them
 * (design.md §6.6, §6.7), so that no goal request reaches the app, where no made-up manager has a session: the list,
 * in its order; an add, at the end, `201`; a reword, a goal's archive and its restore, each with its version as
 * `If-Match`, `428` without one and `412 precondition.failed` with an old one, the reword then answering `200` with the
 * goal's `ETag`, the same one for its own text, and the others `204`, a restored goal at the end; and a reorder of the
 * active goals, each once, `204`, or else `422 config.order_mismatch`. A goal whose place or words change takes a new
 * version, as its `RowVersion` would. Every participant shares the one list. A later call overrides it, as Playwright
 * tries the latest route first.
 */
export async function serveGoals(page: Page, goals: readonly AdminGoal[]): Promise<GoalStore> {
  const held = goals.map((goal) => ({ ...goal }));
  // Sorted stably, so that goals of one SortOrder keep the order in which they were added, as the API's tie-breaks do.
  const listed = () => held.toSorted((a, b) => a.sortOrder - b.sortOrder);
  const active = () => listed().filter((goal) => goal.archivedAtUtc === null);
  const end = () => Math.max(-1, ...active().map((goal) => goal.sortOrder)) + 1;
  const change = (goal: AdminGoal, values: Partial<AdminGoal>) => {
    Object.assign(goal, values, { rowVersion: newVersion() });
  };

  const answer = (route: Route) => {
    const request = route.request();
    const { pathname } = new URL(request.url());
    if (goalsPath.test(pathname)) {
      if (request.method() === 'GET') {
        return route.fulfill({ json: listed() });
      }

      const { text } = request.postDataJSON() as { text: string };
      held.push({ id: randomUUID(), text, sortOrder: end(), archivedAtUtc: null, rowVersion: newVersion() });
      return route.fulfill({ status: 201 });
    }

    if (orderPath.test(pathname)) {
      const { goalIds } = request.postDataJSON() as { goalIds: string[] };
      if (!eachOnce(goalIds, active().map((goal) => goal.id))) {
        return route.fulfill(problem(422, 'config.order_mismatch'));
      }

      for (const [sortOrder, id] of goalIds.entries()) {
        const goal = held.find((each) => each.id === id);
        if (goal !== undefined && goal.sortOrder !== sortOrder) {
          change(goal, { sortOrder });
        }
      }

      return route.fulfill({ status: 204 });
    }

    const [, id, action] = goalPath.exec(pathname) ?? [];
    const goal = held.find((each) => each.id === id);
    if (goal === undefined) {
      return route.fulfill({ status: 404 });
    }

    const ifMatch = request.headers()['if-match'];
    if (ifMatch === undefined) {
      return route.fulfill(problem(428, 'precondition.required'));
    }

    if (ifMatch !== `"${goal.rowVersion}"`) {
      return route.fulfill(problem(412, 'precondition.failed'));
    }

    if (action === undefined) {
      const { text } = request.postDataJSON() as { text: string };
      if (text !== goal.text) {
        change(goal, { text });
      }

      return route.fulfill({ status: 200, headers: { ETag: `"${goal.rowVersion}"` } });
    }

    if (action === 'archive' && goal.archivedAtUtc === null) {
      change(goal, { archivedAtUtc: archivedNow() });
    } else if (action === 'restore' && goal.archivedAtUtc !== null) {
      change(goal, { archivedAtUtc: null, sortOrder: end() });
    }

    return route.fulfill({ status: 204 });
  };

  await page.route(
    ({ pathname }) => goalsPath.test(pathname) || orderPath.test(pathname) || goalPath.test(pathname),
    answer,
  );

  return {
    changeElsewhere: (id, values) => {
      const goal = held.find((each) => each.id === id);
      if (goal === undefined) {
        throw new Error(`No goal ${id} is held.`);
      }

      change(goal, 'text' in values ? { text: values.text } : { archivedAtUtc: archivedNow() });
    },
  };
}
