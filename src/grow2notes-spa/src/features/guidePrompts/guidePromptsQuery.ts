import { queryOptions } from '@tanstack/react-query';
import { apiWithHeaders } from '../../api/api.ts';

/** The organisation's guide prompts, as `GET /api/admin/guide-prompts` gives them (design.md §6.6). */
export type GuidePrompts = {
  /** The prompts as stored, line breaks included, or `""` for none. */
  text: string;
  /** The token of the answer's `ETag`, the organisation's row version, which a save sends back as `If-Match`. */
  etag: string | undefined;
};

/**
 * The `['admin', 'guidePrompts']` query, Guide prompts' one (settings-textarea.md *Implementation notes*). `gcTime: 0`
 * drops it once the page has gone, so that a later visit mounts the editor from a fresh `GET`, never from a cached
 * copy (guide-prompts.md *Loading*). It is not refetched on window focus, the query client's default, as the editor
 * reads it once and a refetch would change nothing on screen.
 */
export const guidePromptsQuery = queryOptions({
  queryKey: ['admin', 'guidePrompts'],
  queryFn: async ({ signal }): Promise<GuidePrompts> => {
    const { body, etag } = await apiWithHeaders<{ text: string }>('/api/admin/guide-prompts', { signal });
    return { text: body.text, etag };
  },
  gcTime: 0,
});
