import { useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { refreshMe } from '../api/me.ts';

/**
 * The shell's triggers for `refreshMe()`, for its whole life (app-shell.md *Refresh of `/api/auth/me`*): each new
 * `location.key`, the first being the start-up, window `focus`, and `visibilitychange` to visible. There is no timer,
 * so a page left alone sends nothing (design.md §6.8).
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
    const onVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        refresh(queryClient);
      }
    };

    window.addEventListener('focus', onFocus);
    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => {
      window.removeEventListener('focus', onFocus);
      document.removeEventListener('visibilitychange', onVisibilityChange);
    };
  }, [queryClient]);
}

// A failure is left in the query, beside the answer it had, so that the screen stays as it was and nothing is said
// (app-shell.md *Window regains focus*).
function refresh(queryClient: QueryClient): void {
  refreshMe(queryClient).catch(() => {});
}
