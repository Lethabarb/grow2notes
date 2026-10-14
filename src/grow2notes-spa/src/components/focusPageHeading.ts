/** The id of the page's one `<h1>`, `PageHeading`. */
export const pageHeadingId = 'page-heading';

/**
 * The id of the `<h1>` of the signed-out-in-place view (app-shell.md component 7), which shows over the page while the
 * page's own `<h1>` stays in the DOM, hidden with it.
 */
export const signedOutHeadingId = 'signed-out-heading';

// Whether the page's <h1> is due focus that it could not take, as it was not in the page yet: on a screen whose <h1> is
// data, which mounts with its data, or as the failed load's fallback or Page not found in its place
// (empty-loading-error.md *Focus on screen change*). In memory only, so a reload empties it.
let pending = false;

/**
 * Focuses the page's `<h1>`, or, while the page has none, leaves the focus pending for the `PageHeading` that mounts
 * next: the one way to the heading, for the shell on a new screen, after a sign-in or the start-up's Try again and for
 * a whole-page message in place of what had focus (app-shell.md *The route-change rule (one owner)*), and for a region
 * after its own Try again.
 */
export function focusPageHeading(options?: FocusOptions): void {
  const heading = document.getElementById(pageHeadingId);
  pending = heading === null;
  heading?.focus(options);
}

/**
 * Focuses `element`, such as Today's success banner or a list's row, in place of the page's `<h1>`, which then takes
 * no focus that it had pending (app-shell.md *The route-change rule (one owner)*, items 3a and 3b). Without
 * `preventScroll` unless `options` say so, so that a banner scrolls into view.
 */
export function focusInsteadOfHeading(element: HTMLElement, options?: FocusOptions): void {
  pending = false;
  element.focus(options);
}

/** `PageHeading`'s ref: gives the heading, as it mounts, the focus pending for it, without scrolling. */
export function takePendingFocus(heading: HTMLElement | null): void {
  if (heading !== null && pending) {
    pending = false;
    heading.focus({ preventScroll: true });
  }
}
