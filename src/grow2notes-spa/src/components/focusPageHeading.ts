/** The id of the page's one `<h1>`, `PageHeading`. */
export const pageHeadingId = 'page-heading';

/**
 * The id of the `<h1>` of the signed-out-in-place view (app-shell.md component 7), which shows over the page while the
 * page's own `<h1>` stays in the DOM, hidden with it.
 */
export const signedOutHeadingId = 'signed-out-heading';

/** Focuses the page's `<h1>`, when it has one, as when a Try again has loaded what the focused button stood for. */
export function focusPageHeading(): void {
  document.getElementById(pageHeadingId)?.focus();
}
