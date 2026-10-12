import { useLayoutEffect } from 'react';
import { msg } from '../copy/messages.ts';
import type { PageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';

/**
 * Sets `document.title` to "Grow2Notes – {name}" (app-shell.md component 3), or to "Grow2Notes", as `index.html` has
 * it, for a page with no name. In a layout effect, which React runs before every `useEffect` of the same commit, so
 * that the "Error: " a screen puts in front of the title in one comes after it, not under it.
 */
export function usePageTitle(name: PageName | undefined): void {
  useLayoutEffect(() => {
    document.title = name === undefined ? strings.appName : msg.pageTitle(name);
  }, [name]);
}
