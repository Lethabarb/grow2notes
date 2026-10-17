import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { ApiError, apiWithHeaders, classify } from '../../api/api.ts';
import { Button } from '../../components/Button.tsx';
import { buttonClass } from '../../components/buttonClass.ts';
import { Notice } from '../../components/Notice.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { time } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { strings, type Cause } from '../../copy/strings.ts';
import { fullPageLoad } from '../../routes/fullPageLoad.ts';
import styles from './UserActions.module.css';
import { UserConfirmation, type Confirmation, type ConfirmedAction } from './UserConfirmation.tsx';
import { allowedActions, type UserAction } from './userRules.ts';
import { usersQuery, type AdminUser } from './usersQuery.ts';

/** An action that sends a request, at once or once its dialog confirms it. */
type RequestAction = Exclude<UserAction, 'edit'>;

// Each action's request (design.md §6.6): Reset sign-in sends an Active user's setup link, which resets their sign-in
// (§8.6), as Resend invite sends an Invited user's.
const requestPaths: Record<RequestAction, string> = {
  resendInvite: 'setup-link',
  resetSignIn: 'setup-link',
  deactivate: 'deactivate',
  reactivate: 'reactivate',
};

// The description above each action's button, which names it (users.md component C items 6 and 7), by an id that is
// fixed, as a page shows one user's actions.
const descriptions: Record<RequestAction, { id: string; text: string }> = {
  resendInvite: { id: 'resend-invite-description', text: strings.users.resendInviteDescription },
  resetSignIn: { id: 'reset-sign-in-description', text: strings.users.resetSignInDescription },
  deactivate: { id: 'deactivate-description', text: strings.users.deactivateDescription },
  reactivate: { id: 'reactivate-description', text: strings.users.reactivateDescription },
};

// The words for a request that failed with no answer or a server's (users.md *States* › User detail and *Dialogs*),
// such as "Not reset: no connection. Try again.".
const notDone: Record<RequestAction, (cause: Cause) => string> = {
  resendInvite: msg.notSent,
  resetSignIn: msg.notReset,
  deactivate: msg.notDeactivated,
  reactivate: msg.notReactivated,
};

// The "you" words for `409 user.last_manager`, which only your own reset or deactivation can meet, in a race with the
// other active manager.
const lastManager: Record<ConfirmedAction, string> = {
  resetSignIn: strings.users.resetDialog.lastManager,
  deactivate: strings.users.deactivateDialog.lastManager,
};

/** Whether the request was refused `412`: the user changed after the page loaded them, so its `If-Match` was stale. */
function isStale(error: unknown): boolean {
  return error instanceof ApiError && error.status === 412;
}

/** Whether the request was refused because you are the organisation's only Active manager (A26). */
function isLastManager(error: unknown): boolean {
  return error instanceof ApiError && error.status === 409 && error.code === 'user.last_manager';
}

/**
 * How a request of `action` on `target` failed: the words that say so, none for a `401`, which the session flow has;
 * and whether it can be sent again, which it cannot once the user has changed (`412`), or once your own reset or
 * deactivation has met `409 user.last_manager`. A dialog's `412` sends the manager back to the page, which by then
 * shows the user as they are now.
 */
function failureOf(action: RequestAction, target: AdminUser, error: unknown, inDialog: boolean) {
  if (isStale(error)) {
    const words = inDialog ? msg.userChangedGoBack(target.displayName) : msg.userChanged(target.displayName);
    return { words, again: false };
  }

  if (isLastManager(error) && (action === 'resetSignIn' || action === 'deactivate')) {
    return { words: lastManager[action], again: false };
  }

  const cause = classify(error);
  return { words: cause === 'signedOut' ? undefined : notDone[action](cause), again: true };
}

/** An action's button, with its description above it (users.md component C items 6 and 7). */
function Described({ action, children }: { action: RequestAction; children: ReactNode }) {
  const { id, text } = descriptions[action];
  return (
    <div className={styles.action}>
      <p id={id} className={styles.description}>
        {text}
      </p>
      {children}
    </div>
  );
}

type UserActionsProps = {
  user: AdminUser;
  /** On the viewer's own page, where a reset or a deactivation ends the viewer's own session. */
  isSelf: boolean;
  /** On the viewer's own page while they are the only Active manager (A26). */
  onlyManager: boolean;
  focusStatus: () => void;
};

/**
 * The actions on a user's page (users.md component C items 5 to 8): the action error region, rendered empty; group 1,
 * with Edit, a link styled as a secondary button, as it changes the address, then Resend invite while the user is
 * Invited, with the line for its result below it, or Reset sign-in while they are Active; and, after a rule, group 2,
 * with Deactivate while they are Invited or Active, or Reactivate while Deactivated, or, on your own page as the only
 * Active manager, the last-manager notice in place of Reset sign-in and Deactivate. Nothing is ever disabled.
 *
 * Each request sends the user's `concurrencyStamp` as `If-Match`, is never sent again by itself, and, once the server
 * has made the change, fetches the users again, so that its button stays busy until the page holds the user's new
 * stamp; a refusal that says the page is out of date fetches them too, before it shows. Reset sign-in and Deactivate
 * ask first, in a dialog, which shows how the request goes; your own reset or deactivation, which ends your session,
 * loads the app afresh, on sign-in. Once a change has taken its own button away, the Status row takes focus, as it
 * shows what changed, with no banner (item 2).
 */
export function UserActions({ user, isSelf, onlyManager, focusStatus }: UserActionsProps) {
  const queryClient = useQueryClient();
  const actions = allowedActions(user, onlyManager);
  const offers = (action: UserAction) => actions.includes(action);
  // The page button whose request is out. Set in the press itself, rather than read from the mutation, whose state
  // TanStack hands on only after a task, by when a second tap of a double tap would have sent it again.
  const [pressed, setPressed] = useState<'resendInvite' | 'reactivate'>();
  const [sent, setSent] = useState<string>();
  const [failure, setFailure] = useState<string>();
  const [confirmation, setConfirmation] = useState<Confirmation>();
  // Whether the dialog is still open as its request ends: a browser may close it while it is busy (confirm-dialog.md
  // *Behaviour* 7), and the page then shows how the request went.
  const confirmationOpen = useRef(false);
  // The user as the list gave them once a change, or a refusal of one, had been fetched again: the Status row's focus
  // waits until the page shows them.
  const statusFocusOn = useRef<AdminUser>(undefined);

  // The sentence goes with the button when a refetch shows the user no longer Invited, so that it cannot come back
  // with the button, about an older link, should they be Invited again. Set during render, as an effect that set it
  // would render twice.
  if (sent !== undefined && !offers('resendInvite')) {
    setSent(undefined);
  }

  // The Status row takes focus only where the change left it on the page itself: the button pressed, or the dialog
  // confirmed in, has gone, and the row shows why, in the place it is shown. While the button stays, so does focus.
  // Only once the page shows the user as fetched again, so that the row is heard with their new status, and not while
  // a dialog is open, which makes the rest of the page inert.
  useLayoutEffect(() => {
    if (statusFocusOn.current !== user || confirmation !== undefined) {
      return;
    }

    statusFocusOn.current = undefined;
    const focused = document.activeElement;
    if (focused === null || focused === document.body) {
      focusStatus();
    }
  });

  const refetchUsers = () => queryClient.refetchQueries({ queryKey: usersQuery.queryKey });

  /** Has the Status row take focus, should the change leave focus on the page, once the page shows `target` anew. */
  function focusStatusOnceShown(target: AdminUser) {
    statusFocusOn.current = queryClient.getQueryData(usersQuery.queryKey)?.find(({ id }) => id === target.id) ?? user;
  }

  /**
   * Sends `action`'s request for `target`, as the page showed them, and gives the time in the answer's `Date` header,
   * if any; then, unless `refetch` is false, fetches the users again, awaited (users.md *E. Data and helpers*,
   * Mutations). A refusal that says the page is out of date fetches them before it is thrown.
   */
  async function send(action: RequestAction, target: AdminUser, refetch: boolean) {
    try {
      const { dateUtc } = await apiWithHeaders(`/api/admin/users/${target.id}/${requestPaths[action]}`, {
        method: 'POST',
        ifMatch: target.concurrencyStamp,
      });
      if (refetch) {
        await refetchUsers();
      }

      return dateUtc;
    } catch (error) {
      if (isStale(error) || isLastManager(error)) {
        await refetchUsers();
      }

      throw error;
    }
  }

  // One for each button whose requests can be out at the same time, such as Resend invite and a Deactivate confirmed
  // while it is out.
  const resend = useMutation({ mutationFn: (target: AdminUser) => send('resendInvite', target, true) });
  const reactivate = useMutation({ mutationFn: (target: AdminUser) => send('reactivate', target, true) });
  const confirmed = useMutation({
    // Your own reset or deactivation has ended your session, so there is no list to fetch.
    mutationFn: ({ action, target, self }: { action: ConfirmedAction; target: AdminUser; self: boolean }) =>
      send(action, target, !self),
  });

  /**
   * Shows in the error region why a page button's request failed, or a dialog's that a browser closed while it was
   * busy; once the user had changed, after the page shows them as they are now, which may take the button away.
   */
  function showFailure(action: RequestAction, target: AdminUser, error: unknown) {
    const { words, again } = failureOf(action, target, error, false);
    setFailure(words);
    if (!again) {
      focusStatusOnceShown(target);
    }
  }

  // TanStack calls the callbacks given to mutate() only while the page is still shown, so that a request that ends
  // after the manager has gone elsewhere takes them nowhere.

  function resendInvite() {
    // Emptied, so that a sentence or a failure that repeats is written into an empty region and announced again.
    setSent(undefined);
    setFailure(undefined);
    setPressed('resendInvite');
    resend.mutate(user, {
      onSuccess: (date) => {
        setPressed(undefined);
        // The server's time, never the device's (users.md *States* › User detail). Focus stays on the button.
        setSent(msg.setupLinkSent(user.email, date === undefined ? undefined : time(date)));
      },
      onError: (error) => {
        setPressed(undefined);
        showFailure('resendInvite', user, error);
      },
    });
  }

  function reactivateUser() {
    setFailure(undefined);
    setPressed('reactivate');
    reactivate.mutate(user, {
      onSuccess: () => {
        setPressed(undefined);
        focusStatusOnceShown(user);
      },
      onError: (error) => {
        setPressed(undefined);
        showFailure('reactivate', user, error);
      },
    });
  }

  function openConfirmation(action: ConfirmedAction) {
    setFailure(undefined);
    confirmationOpen.current = true;
    setConfirmation({ action, target: user, busy: false, canConfirm: true });
  }

  function confirm(open: Confirmation) {
    const { action, target } = open;
    setConfirmation({ ...open, busy: true, error: undefined });
    confirmed.mutate(
      { action, target, self: isSelf },
      {
        onSuccess: () => {
          if (isSelf) {
            // Still busy, so that nothing more is sent while the page goes.
            fullPageLoad();
            return;
          }

          confirmationOpen.current = false;
          setConfirmation(undefined);
          focusStatusOnceShown(target);
        },
        onError: (error) => {
          if (!confirmationOpen.current) {
            showFailure(action, target, error);
            return;
          }

          const { words, again } = failureOf(action, target, error, true);
          setConfirmation({ ...open, busy: false, error: words, canConfirm: again });
        },
      },
    );
  }

  // Any close is Go back: its button, Escape, Android's back gesture, or the warning before the idle sign-out and
  // sign-in's view C closing it. Once confirming was no longer possible, the page behind it shows the user as fetched
  // again, which may have taken away the button that opened it.
  function closeConfirmation(closed: Confirmation) {
    confirmationOpen.current = false;
    setConfirmation(undefined);
    if (!closed.canConfirm) {
      focusStatusOnceShown(closed.target);
    }
  }

  return (
    <div className={styles.actions}>
      <RequestAlert>{failure}</RequestAlert>
      <div className={styles.group}>
        {offers('edit') && (
          <Link to="edit" className={buttonClass('secondary')}>
            {strings.users.edit}
          </Link>
        )}
        {offers('resendInvite') && (
          <Described action="resendInvite">
            <Button
              busy={pressed === 'resendInvite'}
              busyLabel={strings.sending}
              aria-describedby={descriptions.resendInvite.id}
              onClick={resendInvite}
            >
              {strings.users.resendInvite}
            </Button>
            {/* Rendered with the button, so that it is in the page before its sentence is written into it. */}
            <p role="status" className={styles.result}>
              {sent}
            </p>
          </Described>
        )}
        {offers('resetSignIn') && (
          <Described action="resetSignIn">
            <Button aria-describedby={descriptions.resetSignIn.id} onClick={() => openConfirmation('resetSignIn')}>
              {strings.users.resetSignIn}
            </Button>
          </Described>
        )}
      </div>
      <hr className={styles.rule} />
      {onlyManager ? (
        <Notice>{strings.users.lastManagerNotice}</Notice>
      ) : (
        <div className={styles.group}>
          {offers('deactivate') && (
            <Described action="deactivate">
              <Button aria-describedby={descriptions.deactivate.id} onClick={() => openConfirmation('deactivate')}>
                {strings.users.deactivate}
              </Button>
            </Described>
          )}
          {offers('reactivate') && (
            <Described action="reactivate">
              <Button
                busy={pressed === 'reactivate'}
                busyLabel={strings.users.reactivating}
                aria-describedby={descriptions.reactivate.id}
                onClick={reactivateUser}
              >
                {strings.users.reactivate}
              </Button>
            </Described>
          )}
        </div>
      )}
      {confirmation !== undefined && (
        <UserConfirmation
          confirmation={confirmation}
          isSelf={isSelf}
          onConfirm={() => confirm(confirmation)}
          onClose={() => closeConfirmation(confirmation)}
        />
      )}
    </div>
  );
}
