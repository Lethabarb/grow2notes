import type { Page } from '@playwright/test';

/** A common item as `GET /api/admin/common-item-groups` lists it in its group (design.md §6.6). */
export type AdminCommonItem = {
  id: string;
  text: string;
  sortOrder: number;
  /** In UTC, with `Z`, or `null` while the item is active. */
  archivedAtUtc: string | null;
  /** The row's `RowVersion` as base64, which a change to the item sends as `If-Match` (D78). */
  rowVersion: string;
};

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

/** A made-up organisation's built-in Every note group (A35), as `admin bootstrap` leaves it, with no items yet. */
export const everyNote: AdminCommonItemGroup = {
  id: '0e2a4c6f-1b3d-4e5f-8a7b-9c0d1e2f3a4b',
  name: 'Every note',
  isEveryNote: true,
  sortOrder: 0,
  archivedAtUtc: null,
  rowVersion: 'AAAAAAAAC1E=',
  items: [],
};

/**
 * Answers each `GET /api/admin/common-item-groups?includeArchived=true`, Common items' one read, with `groups`, in the
 * browser, as `serveParticipants` answers the participants' list, so that the request never reaches the app, where no
 * made-up manager has a session. A later call overrides it, as Playwright tries the latest route first.
 */
export async function serveCommonItems(page: Page, groups: readonly AdminCommonItemGroup[]): Promise<void> {
  await page.route('**/api/admin/common-item-groups?includeArchived=true', (route) => route.fulfill({ json: groups }));
}
