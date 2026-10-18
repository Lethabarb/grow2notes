import { useEffect } from 'react';
import { unloadGuardArmed } from './leaveGuard.ts';

/**
 * The browser's own warning before the page goes, for a reload, a closed tab or a typed address, while `atRisk`: the
 * listener is added only then, as a page with one loses the browser's back-forward cache (settings-textarea.md
 * *Implementation notes*), and asks only while the shell has not disarmed it, before a full page load that the person
 * chose, or that was another's.
 */
export function useUnloadWarning(atRisk: boolean): void {
  useEffect(() => {
    if (!atRisk) {
      return;
    }

    const warn = (event: BeforeUnloadEvent) => {
      if (unloadGuardArmed()) {
        event.preventDefault();
        // For Chromium before 119, which asks only when this is set.
        event.returnValue = true;
      }
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [atRisk]);
}
