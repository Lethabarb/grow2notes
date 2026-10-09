/** The id of the page's one `<h1>`, `PageHeading`. */
export const pageHeadingId = 'page-heading';

/** Focuses the page's `<h1>`, when it has one, as when a Try again has loaded what the focused button stood for. */
export function focusPageHeading(): void {
  document.getElementById(pageHeadingId)?.focus();
}
