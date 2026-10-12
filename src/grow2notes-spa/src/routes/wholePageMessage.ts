import { createContext, useContext, useLayoutEffect } from 'react';
import type { PageName } from '../copy/pageNames.ts';

/**
 * How a whole-page message tells the shell that it shows in place of the route's screen, with its page name, or
 * `undefined` once it has gone. Outside the shell, as on the shell's own crash page, there is no shell to tell.
 */
export const WholePageMessageContext = createContext<(name: PageName | undefined) => void>(() => {});

/**
 * Tells the shell, while the caller is mounted, that it is a whole-page message named `name` in place of the route's
 * screen, so that the shell titles the page with `name` and gives it to the header (app-shell-nav.md *Titles*). As it
 * goes, the route's title comes back, also when the route's page name is the same as before it showed: React Router
 * clears a crash on the next location, to the same route included. In a layout effect, so that the title changes
 * before the page is painted.
 */
export function useWholePageMessage(name: PageName): void {
  const tell = useContext(WholePageMessageContext);

  useLayoutEffect(() => {
    tell(name);
    return () => {
      tell(undefined);
    };
  }, [tell, name]);
}
