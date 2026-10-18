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
// with none; and an archived group, which the page lists in Archived groups alone, with an item of its own.
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

/** The address of a group's Archive (design.md §6.6), for a route ahead of `serveCommonItems`'s, as for an item's. */
export const groupWrites = { archive: '**/api/admin/common-item-groups/*/archive' } as const;

// The groups, which the list reads and a group add posts to; their order; a group, which a rename puts to, with its
// archive and restore after it; a group's items, which an add posts to; their order; and an item, which a reword or a
// move puts to, with its archive and restore after it.
const groupsPath = /^\/api\/admin\/common-item-groups$/;
const groupOrderPath = /^\/api\/admin\/common-item-groups\/order$/;
const groupPath = /^\/api\/admin\/common-item-groups\/([^/]+)(?:\/(archive|restore))?$/;
const itemsPath = /^\/api\/admin\/common-item-groups\/([^/]+)\/items$/;
const orderPath = /^\/api\/admin\/common-item-groups\/([^/]+)\/items\/order$/;
const itemPath = /^\/api\/admin\/common-items\/([^/]+)(?:\/(archive|restore))?$/;

/** What a test does to the groups and items that `serveCommonItems` holds, as another manager would. */
export type CommonItemStore = {
  /**
   * Rewords the item or renames the group of ID `id`, other than Every note, or archives it, with a new version, so
   * that the page's next write to it is stale, as is a move built from the list as the page last read it.
   */
  changeElsewhere: (id: string, change: { text: string } | { archived: true }) => void;
};

/**
 * Answers Common items' read and each group and item write in the browser from a list in memory, a copy of `groups`,
 * as the API keeps them (design.md §6.6, §6.7; `HeldRows` for the groups other than Every note and for each group's
 * items), so that no request reaches the app, where no made-up manager has a session: the list, in its order, Every
 * note first; a group add at the end, `201` with its ID; a group's rename, archive and restore, each with its version
 * as `If-Match`, `428` without one and `412 precondition.failed` with an old one, `422 config.every_note_fixed` for
 * Every note, the rename then answering `200` with no body, and the others `204`, a restored group at the end; and a
 * reorder of the active groups other than Every note, each once, `204`, or else `422 config.order_mismatch`. Then an
 * item add at the end of an active group, `201` with no body, or `409 config.group_archived` for an archived one; an
 * item's reword or move, archive and restore, each with its version as `If-Match`, `428` and `412` as for a group, the
 * reword or move then answering `200` with no `ETag`, or `409 config.group_archived` while its group, or the group it
 * is moved to, is archived, a moved item at the end of its new group, with its archived state, and the others `204`, a
 * restored item at the end of its group; and a reorder of a group's active items, each once, `204`, or else
 * `422 config.order_mismatch`. An unknown group or item is `404`. Call it before `page.goto`; a later call overrides
 * it, as Playwright tries the latest route first.
 */
export async function serveCommonItems(
  page: Page,
  groups: readonly AdminCommonItemGroup[],
): Promise<CommonItemStore> {
  const fixed = groups.find((group) => group.isEveryNote);
  if (fixed === undefined) {
    throw new Error('The groups hold no Every note group, which every organisation has (D45).');
  }

  // The groups other than Every note, each held as a row whose text is the group's name, so that they are added,
  // renamed, archived, restored and reordered as the API does each, Every note never among them (A43).
  const others = new HeldRows(
    groups
      .filter((group) => !group.isEveryNote)
      .map(({ id, name, sortOrder, archivedAtUtc, rowVersion }) => ({
        id,
        text: name,
        sortOrder,
        archivedAtUtc,
        rowVersion,
      })),
  );
  // Each group's items, by the group's ID.
  const items = new Map(groups.map((group) => [group.id, new HeldRows(group.items)]));
  const itemsOf = (groupId: string) => items.get(groupId)?.listed() ?? [];
  const listed = (): AdminCommonItemGroup[] => [
    { ...fixed, items: itemsOf(fixed.id) },
    ...others.listed().map(({ id, text, sortOrder, archivedAtUtc, rowVersion }) => ({
      id,
      name: text,
      isEveryNote: false,
      sortOrder,
      archivedAtUtc,
      rowVersion,
      items: itemsOf(id),
    })),
  ];
  // Every note is never archived (A43).
  const isArchived = (groupId: string) => (others.find(groupId)?.archivedAtUtc ?? null) !== null;
  const itemOf = (id: string) =>
    [...items].flatMap(([groupId, list]) => {
      const item = list.find(id);
      return item === undefined ? [] : [{ groupId, list, item }];
    })[0];

  const answerGroup = (route: Route, groupId: string, action: string | undefined) => {
    const request = route.request();
    const group = others.find(groupId);
    if (group === undefined && groupId !== fixed.id) {
      return route.fulfill({ status: 404 });
    }

    const refused = refusedPrecondition(request, group ?? fixed);
    if (refused !== undefined) {
      return route.fulfill(refused);
    }

    if (group === undefined) {
      return route.fulfill(problem(422, 'config.every_note_fixed'));
    }

    if (action === undefined) {
      const { name } = request.postDataJSON() as { name: string };
      others.reword(group, name);
      return route.fulfill({ status: 200 });
    }

    if (action === 'archive') {
      others.archive(group);
    } else {
      others.restore(group);
    }

    return route.fulfill({ status: 204 });
  };

  const answerItem = (route: Route, id: string, action: string | undefined) => {
    const request = route.request();
    const found = itemOf(id);
    if (found === undefined) {
      return route.fulfill({ status: 404 });
    }

    const refused = refusedPrecondition(request, found.item);
    if (refused !== undefined) {
      return route.fulfill(refused);
    }

    if (action === undefined) {
      // A missing or null groupId keeps the item in its own group (T07's Notes).
      const sent = request.postDataJSON() as { text: string; groupId?: string | null };
      if (isArchived(found.groupId)) {
        return route.fulfill(problem(409, 'config.group_archived'));
      }

      const to = sent.groupId ?? found.groupId;
      const target = items.get(to);
      if (target === undefined) {
        return route.fulfill({ status: 404 });
      }

      if (isArchived(to)) {
        return route.fulfill(problem(409, 'config.group_archived'));
      }

      if (target === found.list) {
        found.list.reword(found.item, sent.text);
      } else {
        found.list.remove(found.item);
        target.moveIn({ ...found.item, text: sent.text });
      }

      return route.fulfill({ status: 200 });
    }

    if (action === 'archive') {
      found.list.archive(found.item);
    } else {
      found.list.restore(found.item);
    }

    return route.fulfill({ status: 204 });
  };

  const answer = (route: Route) => {
    const request = route.request();
    const { pathname } = new URL(request.url());
    if (groupsPath.test(pathname)) {
      if (request.method() === 'GET') {
        return route.fulfill({ json: listed() });
      }

      const { name } = request.postDataJSON() as { name: string };
      const id = randomUUID();
      others.add(id, name);
      items.set(id, new HeldRows([]));
      return route.fulfill({ status: 201, json: { id } });
    }

    if (groupOrderPath.test(pathname)) {
      const { groupIds } = request.postDataJSON() as { groupIds: string[] };
      return route.fulfill(others.reorder(groupIds) ? { status: 204 } : problem(422, 'config.order_mismatch'));
    }

    const [, addTo] = itemsPath.exec(pathname) ?? [];
    const [, orderOf] = orderPath.exec(pathname) ?? [];
    const listId = addTo ?? orderOf;
    if (listId !== undefined) {
      const list = items.get(listId);
      if (list === undefined) {
        return route.fulfill({ status: 404 });
      }

      if (orderOf !== undefined) {
        const { commonItemIds } = request.postDataJSON() as { commonItemIds: string[] };
        return route.fulfill(list.reorder(commonItemIds) ? { status: 204 } : problem(422, 'config.order_mismatch'));
      }

      if (isArchived(listId)) {
        return route.fulfill(problem(409, 'config.group_archived'));
      }

      const { text } = request.postDataJSON() as { text: string };
      list.add(randomUUID(), text);
      return route.fulfill({ status: 201 });
    }

    const [, groupId, groupAction] = groupPath.exec(pathname) ?? [];
    if (groupId !== undefined) {
      return answerGroup(route, groupId, groupAction);
    }

    const [, id, action] = itemPath.exec(pathname) ?? [];
    return answerItem(route, id, action);
  };

  await page.route(
    ({ pathname }) =>
      [groupsPath, groupOrderPath, groupPath, itemsPath, orderPath, itemPath].some((path) => path.test(pathname)),
    answer,
  );

  return {
    changeElsewhere: (id, change) => {
      const found = itemOf(id);
      const [rows, row] = found === undefined ? [others, others.find(id)] : [found.list, found.item];
      if (row === undefined) {
        throw new Error(`No common item, and no group other than Every note, of ID ${id} is held.`);
      }

      if ('text' in change) {
        rows.reword(row, change.text);
      } else {
        rows.archive(row);
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
