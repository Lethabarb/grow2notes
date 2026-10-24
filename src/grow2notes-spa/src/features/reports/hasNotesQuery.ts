import { queryOptions } from '@tanstack/react-query';
import { api } from '../../api/api.ts';

/** `GET /api/reports/daily/{date}/has-notes` (design.md §6.5): whether the day has a submitted note to report. */
export type HasNotes = { date: string; hasNotes: boolean };

/**
 * The `['reports', 'daily', date, 'has-notes']` query for a report date. It holds no placeholder from another date, so
 * a step never shows the last day's answer, and it opts in to a refetch as the page becomes visible, which the query
 * client turns off: a worker's first submission for the day then shows without a reload. The query client's defaults
 * give it one silent retry for no connection or a `5xx`.
 */
export function hasNotesQuery(date: string) {
  return queryOptions({
    queryKey: ['reports', 'daily', date, 'has-notes'],
    queryFn: ({ signal }) => api<HasNotes>(`/api/reports/daily/${date}/has-notes`, { signal }),
    refetchOnWindowFocus: true,
  });
}
