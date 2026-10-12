/**
 * Loads `/` afresh in place of this page (app-shell.md *Press Sign out*): the new page starts with nothing in memory,
 * and `replace` keeps the page it replaces out of Back. In a module of its own, so that a test can stand in for it, as
 * jsdom does not navigate.
 */
export function fullPageLoad(): void {
  window.location.replace('/');
}
