import { randomUUID } from 'node:crypto';
import type { Page, Route } from '@playwright/test';
import { HeldRows, madeUpRow, newVersion, problem, refusedPrecondition, type ListedRow } from './rows.ts';

/** A common item as `GET /api/admin/common-item-groups` lists it in its group (design.md §6.6). */
export type AdminCommonItem = ListedRow;

/** A common item group as `GET /api/admin/common-item-groups` lists it, with its items (design.md §6.6). */
export type AdminCommonItemGroup = {
  id: string;
  name: string;
  isEveryNote: boolean;
  sortOrder: number;
  /** In UTC, with `Z`, or `null` while the group is active. */
  archivedAtUtc: string | null;
  /** The row's `RowVersion` as base64, which a change to the group sends as `If-Match` (D78). */
  rowVersion: string;
  items: AdminCommonItem[];
};

/** A made-up common item (A35), active unless given the time at which it was archived. */
export const madeUpItem = madeUpRow;

/** A made-up group other than Every note (A35), active unless given the time at which it was archived. */
export function madeUpGroup(
  id: string,
  name: string,
  sortOrder: number,
  items: AdminCommonItem[],
  archivedAtUtc: string | null = null,
): AdminCommonItemGroup {
  return { id, name, isEveryNote: false, sortOrder, archivedAtUtc, rowVersion: newVersion(), items };
}

/** A made-up organisation's built-in Every note group (A35), as `admin bootstrap` leaves it, with no items yet. */
export const everyNote: AdminCommonItemGroup = {
  ...madeUpGroup('0e2a4c6f-1b3d-4e5f-8a7b-9c0d1e2f3a4b', 'Every note', 0, []),
  isEveryNote: true,
};

// common-items.md's sketch (*Layout - phone*), as CommonItemsPage.test.tsx has it: Every note with two items and two
// archived ones, archived in this order and so shown the other way round; Community outing with three; Personal care
// with none; and an archived group, which the page leaves out, with an item of its own.
export const medication = madeUpItem('1a4c7e90-2b5d-4f81-9c36-8e0a2d4f6b17', 'Medication prompted', 0);
export const meal = madeUpItem('2b5d8fa1-3c6e-4a92-8d47-9f1b3e5a7c28', 'Meal prepared', 1);
export const laundry = madeUpItem('3c6e9ab2-4d7f-4ba3-9e58-0a2c4f6b8d39', 'Laundry done', 2, '2026-09-21T05:12:00Z');
export const bed = madeUpItem('4d7fabc3-5e80-4cb4-8f69-1b3d5a7c9e4a', 'Bed made', 3, '2026-09-28T05:12:00Z');
export const busOrTrain = madeUpItem('5e80bcd4-6f91-4dc5-9a7a-2c4e6b8dafb5', 'Travelled by bus or train', 0);
export const paid = madeUpItem('6f91cde5-7aa2-4ed6-8b8b-3d5f7c9eb0c6', 'Paid for a ticket at the counter', 1);
export const lunch = madeUpItem('7aa2def6-8bb3-4fe7-9c9c-4e6a8daf1d07', 'Bought lunch', 2);
const packed = madeUpItem('8bb3ef07-9cc4-4af8-8dad-5f7b9eb02e18', 'Packed an overnight bag', 0);

const outing = madeUpGroup('1f3b5d7a-2c4e-4f60-9b8c-0d1e2f3a4b5c', 'Community outing', 0, [
  busOrTrain,
  paid,
  lunch,
]);
const care = madeUpGroup('2a4c6e8b-3d5f-4a71-8c9d-1e2f3a4b5c6d', 'Personal care', 1, []);
const respite = madeUpGroup(
  '3b5d7f9c-4e6a-4b82-9dae-2f3a4b5c6d7e',
  'Overnight respite',
  2,
  [packed],
  '2026-09-14T05:12:00Z',
);

/**
 * The page ready with three groups, in the API's order: Every note first, with its items, then the others in order,
 * the archived one among them by its `SortOrder`.
 */
export const threeGroups: readonly AdminCommonItemGroup[] = [
  { ...everyNote, items: [medication, meal, laundry, bed] },
  outing,
  care,
  respite,
];

/**
 * Common items' one read, of every group, archived ones too, each with its items: the address of a route that holds
 * it, as `holdNext` does, or fails it.
 */
export const listRead = '**/api/admin/common-item-groups?includeArchived=true';

/**
 * The addresses of an item move's and an item Archive's writes (design.md §6.6), for a route ahead of
 * `serveCommonItems`'s that holds the page's next one, as `holdNext` does, or fails it. A held write reaches
 * `serveCommonItems` through `route.fallback()`.
 */
export const itemWrites = {
  reorder: '**/api/admin/common-item-groups/*/items/order',
  archive: '**/api/admin/common-items/*/archive',
} as const;

// The groups, which the list reads; a group's items, which an add posts to; their order; and an item, which a reword
// puts to, with its archive and restore after it.
const groupsPath = /^\/api\/admin\/common-item-groups$/;
const itemsPath = /^\/api\/admin\/common-item-groups\/([^/]+)\/items$/;
const orderPath = /^\/api\/admin\/common-item-groups\/([^/]+)\/items\/order$/;
const itemPath = /^\/api\/admin\/common-items\/([^/]+)(?:\/(archive|restore))?$/;

/** What a test does to the items that `serveCommonItems` holds, as another manager would. */
export type CommonItemStore = {
  /** Rewords the item, or archives it, with a new version, so that the page's next write to it is stale. */
  changeElsewhere: (id: string, change: { text: string } | { archived: true }) => void;
};

/**
 * Answers Common items' read and each item write in the browser from a list in memory, a copy of `groups`, as the API
 * keeps them (design.md §6.6, §6.7; `HeldRows` for each group's items), so that no request reaches the app, where no
 * made-up manager has a session: the list, in its order, Every note first; an add at the end of an active group, `201`,
 * or `409 config.group_archived` for an archived one; an item's reword, archive and restore, each with its version as
 * `If-Match`, `428` without one and `412 precondition.failed` with an old one, the reword then answering `200` with no
 * `ETag`, or `409 config.group_archived` while its group is archived, and the others `204`, a restored item at the end
 * of its group; and a reorder of a group's active items, each once, `204`, or else `422 config.order_mismatch`. An
 * unknown group or item is `404`. Call it before `page.goto`; a later call overrides it, as Playwright tries the latest
 * route first.
 */
export async function serveCommonItems(
  page: Page,
  groups: readonly AdminCommonItemGroup[],
): Promise<CommonItemStore> {
  const held = groups.map(({ items, ...group }) => ({ group, items: new HeldRows(items) }));
  // Every note first, then by SortOrder, sorted stably, so that groups of one SortOrder keep the order they came in.
  type Ordered = Pick<AdminCommonItemGroup, 'isEveryNote' | 'sortOrder'>;
  const inOrder = (a: Ordered, b: Ordered) =>
    Number(b.isEveryNote) - Number(a.isEveryNote) || a.sortOrder - b.sortOrder;
  const listed = () =>
    held.toSorted((a, b) => inOrder(a.group, b.group)).map(({ group, items }) => ({ ...group, items: items.listed() }));
  const groupOf = (id: string) => held.find(({ group }) => group.id === id);
  const itemOf = (id: string) =>
    held.flatMap(({ group, items }) => {
      const item = items.find(id);
      return item === undefined ? [] : [{ group, items, item }];
    })[0];

  const answer = (route: Route) => {
    const request = route.request();
    const { pathname } = new URL(request.url());
    if (groupsPath.test(pathname)) {
      return route.fulfill({ json: listed() });
    }

    const [, addTo] = itemsPath.exec(pathname) ?? [];
    const [, orderOf] = orderPath.exec(pathname) ?? [];
    const groupId = addTo ?? orderOf;
    if (groupId !== undefined) {
      const found = groupOf(groupId);
      if (found === undefined) {
        return route.fulfill({ status: 404 });
      }

      if (orderOf !== undefined) {
        const { commonItemIds } = request.postDataJSON() as { commonItemIds: string[] };
        const reordered = found.items.reorder(commonItemIds);
        return route.fulfill(reordered ? { status: 204 } : problem(422, 'config.order_mismatch'));
      }

      if (found.group.archivedAtUtc !== null) {
        return route.fulfill(problem(409, 'config.group_archived'));
      }

      const { text } = request.postDataJSON() as { text: string };
      found.items.add(randomUUID(), text);
      return route.fulfill({ status: 201 });
    }

    const [, id, action] = itemPath.exec(pathname) ?? [];
    const found = itemOf(id);
    if (found === undefined) {
      return route.fulfill({ status: 404 });
    }

    const refused = refusedPrecondition(request, found.item);
    if (refused !== undefined) {
      return route.fulfill(refused);
    }

    if (action === undefined) {
      if (found.group.archivedAtUtc !== null) {
        return route.fulfill(problem(409, 'config.group_archived'));
      }

      const { text } = request.postDataJSON() as { text: string };
      found.items.reword(found.item, text);
      return route.fulfill({ status: 200 });
    }

    if (action === 'archive') {
      found.items.archive(found.item);
    } else {
      found.items.restore(found.item);
    }

    return route.fulfill({ status: 204 });
  };

  await page.route(
    ({ pathname }) => [groupsPath, itemsPath, orderPath, itemPath].some((path) => path.test(pathname)),
    answer,
  );

  return {
    changeElsewhere: (id, change) => {
      const found = itemOf(id);
      if (found === undefined) {
        throw new Error(`No common item ${id} is held.`);
      }

      if ('text' in change) {
        found.items.reword(found.item, change.text);
      } else {
        found.items.archive(found.item);
      }
    },
  };
}

/**
 * Aborts each `GET /api/admin/common-item-groups?includeArchived=true` that the page sends from now on, in the browser,
 * as a phone with no signal would, as `failParticipants` does. A later call of `serveCommonItems` overrides it.
 */
export async function failCommonItems(page: Page): Promise<void> {
  await page.route(listRead, (route) => route.abort('internetdisconnected'));
}
