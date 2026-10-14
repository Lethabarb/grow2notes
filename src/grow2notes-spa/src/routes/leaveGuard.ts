// The leave guard (settings-textarea.md *Implementation notes*), in memory only (D22). A page with changes it has not
// saved registers how to ask before it is left, which the shell asks before Sign out (app-shell.md *Press Sign out*);
// and the shell disarms the page's `beforeunload` warning before a full page load it makes on purpose, so that the
// browser asks no one about text the person has already chosen to leave, or that was another person's.

let ask: (() => Promise<boolean>) | null = null;
let unloadArmed = true;

/**
 * Registers the page's question, which resolves `true` to leave and `false` to stay, or, with `null`, removes it as
 * the page goes.
 */
export function setLeaveGuard(guard: (() => Promise<boolean>) | null): void {
  ask = guard;
}

/** Whether the person may leave: the registered page's answer, or `true` at once when no page has registered. */
export function confirmLeave(): Promise<boolean> {
  return ask === null ? Promise.resolve(true) : ask();
}

/** Before a full page load made on purpose: a Sign out the person confirmed, or a different person's sign-in. */
export function disarmUnloadGuard(): void {
  unloadArmed = false;
}

/** After a logout that failed, as the page that was to go is still there, with its text. */
export function rearmUnloadGuard(): void {
  unloadArmed = true;
}

/** Whether a page's `beforeunload` listener should ask before the page goes. */
export function unloadGuardArmed(): boolean {
  return unloadArmed;
}
