import { createContext, useContext } from 'react';

export const PageStatusContext = createContext<((text: string) => void) | undefined>(undefined);

/**
 * The nearest `PageStatus`'s `announce(text)`, or `undefined` outside every one, so that a caller with no page around
 * it, such as the session warning with its own status line, announces nothing.
 */
export function useAnnounce(): ((text: string) => void) | undefined {
  return useContext(PageStatusContext);
}
