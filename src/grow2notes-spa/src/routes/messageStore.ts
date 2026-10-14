// The one success message waiting for the screen that shows it. In memory only, never router state, the address or
// storage, as a browser may write history state to disk (status-messages.md *Implementation notes*), so a reload or
// signing out, which reloads the page, empties it.
let message: string | undefined;

/**
 * The success-message store (app-shell.md component 3): at most one message, such as "Note for Jane Citizen
 * submitted", which a screen writes before it moves on and the next screen reads once and clears, so that a second
 * one replaces the first.
 */
export const messageStore = {
  set(text: string): void {
    message = text;
  },
  peek(): string | undefined {
    return message;
  },
  clear(): void {
    message = undefined;
  },
};
