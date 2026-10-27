/**
 * Takes the browser to `url` as a page of its own (mcp-server.md §4.4 step 5): the same-origin address at which the
 * server issues the authorisation code or refuses, and answers with the redirect to the assistant. In a module of its
 * own, so that a test can stand in for it, as jsdom does not navigate.
 */
export function leaveFor(url: string): void {
  window.location.assign(url);
}
