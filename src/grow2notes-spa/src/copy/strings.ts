/** Words that have no slots (microcopy.md §8), each written here once. */
export const strings = {
  tryAgain: 'Try again',
  /** Try again's busy label. */
  loading: 'Loading…',
  /** Why a request failed, as the failure sentences put it (microcopy.md §9). */
  cause: {
    /** `fetch` rejected, or no whole answer came in time. */
    offline: 'no connection',
    /** Any other failure but a `401`, which the session flow has. */
    server: 'something went wrong',
  },
} as const;

export type Cause = keyof typeof strings.cause;
