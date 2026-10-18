import { queryOptions } from '@tanstack/react-query';
import { api } from '../../api/api.ts';
import type { ListApi, ListRow } from '../../components/ListEditor.tsx';

/**
 * A common item group as `GET /api/admin/common-item-groups` lists it (design.md §6.6), with its items, active and
 * archived, in the list's order.
 */
export type AdminCommonItemGroup = {
  id: string;
  name: string;
  /** True for the organisation's one built-in Every note group, which the list gives first (D45). */
  isEveryNote: boolean;
  sortOrder: number;
  /** In UTC, with `Z`, or `null` while the group is active. */
  archivedAtUtc: string | null;
  /** The row's `RowVersion` as base64, which a change to the group sends as `If-Match` (D78). */
  rowVersion: string;
  items: ListRow[];
};

// The path of the groups, which the list reads (design.md §6.6).
const groupsPath = '/api/admin/common-item-groups';

/** The path of a group's items, which an add posts to, and under which their order is put (design.md §6.6). */
function groupItemsPath(groupId: string): string {
  return `${groupsPath}/${encodeURIComponent(groupId)}/items`;
}

/** The path of an item, which a reword puts to, and under which it is archived and restored (design.md §6.6). */
function itemPath(itemId: string): string {
  return `/api/admin/common-items/${encodeURIComponent(itemId)}`;
}

/**
 * The `['admin', 'common-item-groups']` query, Common items' one (common-items.md component 3): every group, archived
 * ones too, each with its active and archived items, in the API's order, from which each group's list picks its own,
 * so that one read again after a write refreshes every section. Keyed by nothing an item or a group says. Fetched
 * again on mount, as TanStack's `staleTime: 0` has it, but not on window focus, which app-shell.md does not list it
 * for.
 */
export const commonItemGroupsQuery = queryOptions({
  queryKey: ['admin', 'common-item-groups'],
  queryFn: ({ signal }) => api<AdminCommonItemGroup[]>(`${groupsPath}?includeArchived=true`, { signal }),
});

/**
 * `ListEditor`'s way to a group's items (list-editor.md *Implementation notes*): the page's one query, from which it
 * picks the group's items; the one scope of every write on the page, `common-items` (common-items.md component 5);
 * the add to the end of the group, whose `201` has no body; an item's reword, with its `rowVersion` as `If-Match`,
 * whose `200` has none either; the reorder of the group's active items, with no `If-Match`, as the set of their IDs is
 * its check (design.md §6.7), whose `204` has no body; and an item's archive and restore, with no body and its
 * `rowVersion` as `If-Match`, whose `204`s have none.
 */
export function commonItemsApi(groupId: string): ListApi<AdminCommonItemGroup[]> {
  return {
    query: commonItemGroupsQuery,
    rowsOf: (groups) => groups.find((group) => group.id === groupId)?.items ?? [],
    scopeId: 'common-items',
    add: (text) => api<undefined>(groupItemsPath(groupId), { method: 'POST', body: { text } }),
    update: (id, text, rowVersion) =>
      api<undefined>(itemPath(id), { method: 'PUT', body: { text }, ifMatch: rowVersion }),
    reorder: (commonItemIds) =>
      api<undefined>(`${groupItemsPath(groupId)}/order`, { method: 'PUT', body: { commonItemIds } }),
    archive: (id, rowVersion) => api<undefined>(`${itemPath(id)}/archive`, { method: 'POST', ifMatch: rowVersion }),
    restore: (id, rowVersion) => api<undefined>(`${itemPath(id)}/restore`, { method: 'POST', ifMatch: rowVersion }),
  };
}
