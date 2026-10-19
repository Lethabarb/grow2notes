import { createContext, useContext } from 'react';

/**
 * Whether the signed-out-in-place view hides the route's screen (app-shell.md component 7), as the shell tells it:
 * from the session's end over it until the same person signs in again, and for good once another person has, as the
 * page then loads afresh. Outside the shell, nothing hides a screen.
 */
export const HiddenBySignInContext = createContext(false);

/** Whether the signed-out-in-place view hides the caller's screen. */
export function useHiddenBySignIn(): boolean {
  return useContext(HiddenBySignInContext);
}
