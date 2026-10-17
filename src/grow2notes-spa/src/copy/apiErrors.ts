/**
 * The API's error codes that the app words (design.md §6.9). A story that words a code adds it here, with its words
 * below, as the server's `ErrorCode` holds only the codes the app answers.
 */
export type ApiErrorCode = 'auth.setup_link_invalid' | 'user.email_in_use';

/**
 * The words for each code (microcopy.md §8), in the shape its screen shows them. Checked against the union rather than
 * typed as one record, so that a code in the union with no words fails the type check while each entry keeps its own
 * shape.
 */
export const apiErrorCopy = {
  /** sign-in.md's *Link expired, used or replaced*: a whole-page message, design.md §4.1's sentence split in two. */
  'auth.setup_link_invalid': { heading: 'This link has expired', body: 'Ask a manager to send a new one.' },
  /** The Email address field's message on Invite user (users.md *States*, microcopy.md's words). */
  'user.email_in_use':
    'Another account already uses this email address. Use a different one, or find the person in the Users list.',
} as const satisfies Record<ApiErrorCode, unknown>;
