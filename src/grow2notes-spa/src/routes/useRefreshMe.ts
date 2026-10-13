import { useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { refreshMe } from '../api/me.ts';
import { startClock } from '../api/session.ts';

/**
 * The shell's triggers for `refreshMe()`, for its whole life (app-shell.md *Refresh of `/api/auth/me`*): each new
 * `location.key`, the first being the start-up, window `focus`, and `visibilitychange` to visible, which the session
 * clock listens for, so that it holds on that very refresh (app-shell.md *Page visible again*); the clock runs for the
 * shell's whole life too. No timer sends a refresh, so a page left alone sends nothing (design.md §6.8).
 */
export function useRefreshMe(): void {
  const queryClient = useQueryClient();
  const { key } = useLocation();

  // Every navigation gives a new key, a link to the address already open included, and Back gives its entry's own.
  useEffect(() => {
    refresh(queryClient);
  }, [queryClient, key]);

  useEffect(() => {
    const onFocus = () => {
      refresh(queryClient);
    };

    window.addEventListener('focus', onFocus);
    const stopClock = startClock(() => refreshMe(queryClient));
    return () => {
      window.removeEventListener('focus', onFocus);
      stopClock();
    };
  }, [queryClient]);
}

// A failure is left in the query, beside the answer it had, so that the screen stays as it was and nothing is said
// (app-shell.md *Window regains focus*).
function refresh(queryClient: QueryClient): void {
  refreshMe(queryClient).catch(() => {});
}
