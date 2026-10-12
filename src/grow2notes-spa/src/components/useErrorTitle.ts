import { useEffect } from 'react';
import { strings } from '../copy/strings.ts';

/**
 * Puts "Error: " in front of the page title while `hasError` is true, and takes it off when it turns false or the
 * caller goes (microcopy.md §4's Page title, sign-in.md component 1): the one way the app adds it, which the error
 * summary and the setup screen's failure lines share. In a `useEffect`, so that it comes after the title that the
 * shell sets in a layout effect; a title changed while the error showed is left as it is.
 */
export function useErrorTitle(hasError: boolean): void {
  useEffect(() => {
    if (!hasError) {
      return;
    }

    const prefix = `${strings.errorPrefix} `;
    document.title = prefix + document.title;
    return () => {
      if (document.title.startsWith(prefix)) {
        document.title = document.title.slice(prefix.length);
      }
    };
  }, [hasError]);
}
