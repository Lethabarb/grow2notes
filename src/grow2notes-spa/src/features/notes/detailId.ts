/** A Details box's id, which its label, its message and the error summary's link name, from its item's ID. */
export function detailId(itemId: string): string {
  return `detail-${itemId}`;
}
