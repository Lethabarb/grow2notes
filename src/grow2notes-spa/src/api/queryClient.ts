import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './api.ts';

/**
 * The app's query client, made once in `main.tsx`; each test makes its own. Its cache is in memory only, with no
 * persister, so nothing reaches device storage (D22, design.md §9.6).
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // In TanStack's default 'online' mode, a query started while the browser reports offline is paused with no
        // error, so its screen would say "Loading…" for ever (empty-loading-error.md *Timing*).
        networkMode: 'always',
        // One silent retry, after TanStack's 1 s delay, when trying again might work; a 4xx would get the same answer.
        retry: (failureCount, error) => failureCount < 1 && isTransient(error),
        // Off but for the screens that app-shell.md lists, which opt in.
        refetchOnWindowFocus: false,
      },
      mutations: {
        // In 'online' mode, a change made while the browser reports offline would wait, busy, and then be sent by
        // itself when the browser is back online, rather than fail and say so (form-validation.md *Server responses*).
        // Autosave, which should wait, sets 'online' on its own mutation (autosave-status.md).
        networkMode: 'always',
        // TanStack's own default, set so that it stays: most changes have no idempotency key, so a change sent again
        // after a lost answer could be made twice (primary-actions.md §8).
        retry: 0,
      },
    },
  });
}

// No connection, a timeout or a 5xx, which a second try might get past.
function isTransient(error: Error): boolean {
  return error instanceof ApiError && (error.status === 0 || error.status >= 500);
}
