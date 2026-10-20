import type { DraftGroup, DraftItem } from './noteQueries.ts';

/**
 * Whether the picker offers the group (group-picker.md *States*): Every note never is, as it is not a choice, and a
 * group with no items is not, as there would be nothing to tick under it.
 */
export function isOffered(group: DraftGroup): boolean {
  return !group.isEveryNote && group.items.length > 0;
}

/** The items that the form shows, and so may be ticked: those of Every note and of the picked groups. */
export function shownItems(groups: readonly DraftGroup[], picked: ReadonlySet<string>): DraftItem[] {
  return groups.filter((group) => group.isEveryNote || picked.has(group.id)).flatMap((group) => group.items);
}
