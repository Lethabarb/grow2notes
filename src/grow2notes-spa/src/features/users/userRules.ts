import type { AdminUser } from './usersQuery.ts';

// Letters alone, so that neither letter case nor an accent sets two names apart (users.md component A item 6).
const collator = new Intl.Collator('en-AU', { sensitivity: 'base' });

/**
 * Compares two users by name, as the Users list sorts them, since the API sorts nothing (design.md §6.6) and SQL
 * Server's collation would not sort as `Intl.Collator` does.
 */
export function byName(a: AdminUser, b: AdminUser): number {
  return collator.compare(a.displayName, b.displayName);
}
