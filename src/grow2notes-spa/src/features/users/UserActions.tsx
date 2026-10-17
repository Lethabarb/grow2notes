import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { Link } from 'react-router';
import { ApiError, apiWithHeaders, classify } from '../../api/api.ts';
import { Button } from '../../components/Button.tsx';
import { buttonClass } from '../../components/buttonClass.ts';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { time } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import styles from './UserActions.module.css';
import { allowedActions } from './userRules.ts';
import { usersQuery, type AdminUser } from './usersQuery.ts';

// Fixed, as a page shows one user's actions.
const resendDescriptionId = 'resend-invite-description';

/** Whether the request was refused `412`: the user changed after the page loaded them, so its `If-Match` was stale. */
function isStale(error: unknown): boolean {
  return error instanceof ApiError && error.status === 412;
}

/**
 * The actions on a user's page (users.md component C items 5 and 6): the action error region, rendered empty, then
 * group 1, with Edit, a link styled as a secondary button, as it changes the address, and, while the user is Invited,
 * Resend invite, with its description above it and the line for its result below it, rendered empty with it.
 *
 * Resend invite sends the user's `concurrencyStamp` as `If-Match` and, once the server has sent the link, refetches the
 * users, so that the button stays busy until the page holds the user's new stamp; it is never sent again by itself.
 * Focus stays on it, and its line says when the server sent the link. A failure shows in the error region: for a
 * `412`, once the page shows the user as they are now.
 */
export function UserActions({ user, focusStatus }: { user: AdminUser; focusStatus: () => void }) {
  const queryClient = useQueryClient();
  const actions = allowedActions(user);
  const resendButton = useRef<HTMLButtonElement>(null);
  // Set in the press itself, rather than read from the mutation, whose state TanStack hands on only after a task, by
  // when a second tap of a double tap would have sent the link again.
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState<string>();
  const [failure, setFailure] = useState<string>();

  // The sentence goes with the button when a refetch shows the user no longer Invited, so that it cannot come back
  // with the button, about an older link, should they be Invited again. Set during render, as an effect that set it
  // would render twice.
  if (sent !== undefined && !actions.includes('resendInvite')) {
    setSent(undefined);
  }

  const refetchUsers = () => queryClient.refetchQueries({ queryKey: usersQuery.queryKey });

  const resend = useMutation({
    mutationFn: async (target: AdminUser) => {
      try {
        const { dateUtc } = await apiWithHeaders(`/api/admin/users/${target.id}/setup-link`, {
          method: 'POST',
          ifMatch: target.concurrencyStamp,
        });
        // Awaited, so that the list holds the new stamp before the button can be pressed again (users.md *E. Data and
        // helpers*, Mutations).
        await refetchUsers();
        return dateUtc;
      } catch (error) {
        // The user as they are now, so that the sentence that says the page shows them is true when it shows.
        if (isStale(error)) {
          await refetchUsers();
        }

        throw error;
      }
    },
  });

  function resendInvite() {
    // Emptied, so that a sentence or a failure that repeats is written into an empty region and announced again.
    setSent(undefined);
    setFailure(undefined);
    setSending(true);
    // TanStack calls these only while the page is still shown, so that a resend that ends after the manager has gone
    // elsewhere takes them nowhere.
    resend.mutate(user, {
      onSuccess: (date) => {
        setSending(false);
        // The server's time, never the device's (users.md *States* › User detail).
        setSent(msg.setupLinkSent(user.email, date === undefined ? undefined : time(date)));
      },
      onError: (error) => {
        setSending(false);
        if (isStale(error)) {
          setFailure(msg.userChanged(user.displayName));
          // The refetch can show the user no longer Invited, which takes the button away, so the Status row, which
          // shows why, takes focus rather than the page (users.md component C item 2), whether the button is still
          // there or has already gone and left focus on the page. While the button stays, so does focus.
          const now = queryClient.getQueryData(usersQuery.queryKey)?.find(({ id }) => id === user.id);
          const focused = document.activeElement;
          const onButtonOrPage = focused === resendButton.current || focused === document.body || focused === null;
          if (now !== undefined && !allowedActions(now).includes('resendInvite') && onButtonOrPage) {
            focusStatus();
          }

          return;
        }

        // Nothing for a 401, which the session flow has.
        const cause = classify(error);
        if (cause !== 'signedOut') {
          setFailure(msg.notSent(cause));
        }
      },
    });
  }

  return (
    <div className={styles.actions}>
      <RequestAlert>{failure}</RequestAlert>
      <div className={styles.group}>
        {actions.includes('edit') && (
          <Link to="edit" className={buttonClass('secondary')}>
            {strings.users.edit}
          </Link>
        )}
        {actions.includes('resendInvite') && (
          <div className={styles.action}>
            <p id={resendDescriptionId} className={styles.description}>
              {strings.users.resendInviteDescription}
            </p>
            <Button
              ref={resendButton}
              busy={sending}
              busyLabel={strings.sending}
              aria-describedby={resendDescriptionId}
              onClick={resendInvite}
            >
              {strings.users.resendInvite}
            </Button>
            {/* Rendered with the button, so that it is in the page before its sentence is written into it. */}
            <p role="status" className={styles.result}>
              {sent}
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
