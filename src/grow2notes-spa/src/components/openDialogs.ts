// The confirmation dialogs open now, each from its showModal() until it unmounts.
const openDialogs = new Set<HTMLDialogElement>();

/**
 * Counts `dialog`, a confirmation that has just opened, among those that `closeOpenDialogs()` closes, until the
 * function that this gives is called, as the dialog unmounts.
 */
export function trackOpenDialog(dialog: HTMLDialogElement): () => void {
  openDialogs.add(dialog);
  return () => {
    openDialogs.delete(dialog);
  };
}

/**
 * Closes every open confirmation with a plain `close()`, which each takes as its safe choice, Go back, so that nothing
 * it would have confirmed is sent: before the warning before the idle sign-out opens over it (app-shell.md *28:00 idle
 * on a visible page*), and before sign-in's view C shows, which a modal left open over the hidden screen would make
 * inert (confirm-dialog.md *Behaviour* 8). Closing gives focus back to what opened the dialog, where the warning, once
 * it has closed in turn, gives it back too.
 */
export function closeOpenDialogs(): void {
  for (const dialog of openDialogs) {
    dialog.close();
  }
}
