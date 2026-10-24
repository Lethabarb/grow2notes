import { useEffect, useEffectEvent } from 'react';

/**
 * Calls `onFocus` each time the window takes focus. TanStack Query 5 refetches on `visibilitychange` alone, which a
 * click back into a window left visible beside another never fires. `onFocus` may be an inline arrow: the listener
 * is added once and always calls the latest one.
 */
export function useOnWindowFocus(onFocus: () => void): void {
  const handle = useEffectEvent(onFocus);

  useEffect(() => {
    const listener = () => {
      handle();
    };

    window.addEventListener('focus', listener);
    return () => {
      window.removeEventListener('focus', listener);
    };
  }, []);
}
