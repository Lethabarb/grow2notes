import { ConfirmDialog } from '../../components/ConfirmDialog.tsx';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import type { AdminUser } from './usersQuery.ts';

/** An action on a user's page that asks first, in a dialog (users.md component C item 8). */
export type ConfirmedAction = 'resetSignIn' | 'deactivate';

/**
 * A dialog open on a user's page: the action it confirms, the user as the page showed them as it opened, whose stamp
 * its request sends, and how that request goes.
 */
export type Confirmation = {
  action: ConfirmedAction;
  target: AdminUser;
  busy: boolean;
  /** Why the request failed, in the dialog's alert line. */
  error?: string;
  /** False once the request cannot be sent again: the user changed, or you are the only active manager after all. */
  canConfirm: boolean;
};

type Words = { title: string; body: readonly string[]; confirm: string };

/** The Reset sign-in dialog's words (users.md *Dialogs*): for your own account, or naming someone else. */
function resetWords({ displayName, email }: AdminUser, isSelf: boolean): Words {
  return isSelf
    ? {
        title: strings.users.resetDialog.ownTitle,
        body: [msg.resetOwnWhatHappens(email)],
        confirm: strings.users.resetDialog.ownConfirm,
      }
    : {
        title: msg.resetTitle(displayName),
        body: [msg.resetCheckWhoIsAsking(displayName), msg.resetWhatHappens(email)],
        confirm: msg.resetConfirm(displayName),
      };
}

/** The Deactivate dialog's words (users.md *Dialogs*): for your own account, an Invited person, or anyone else. */
function deactivateWords({ displayName, status }: AdminUser, isSelf: boolean): Words {
  if (isSelf) {
    return {
      title: strings.users.deactivateDialog.ownTitle,
      body: [strings.users.deactivateDialog.ownBody],
      confirm: strings.users.deactivateDialog.ownConfirm,
    };
  }

  return {
    title: msg.deactivateTitle(displayName),
    body: [status === 'Invited' ? msg.deactivateInvitedBody(displayName) : msg.deactivateBody(displayName)],
    confirm: msg.deactivateConfirm(displayName),
  };
}

type UserConfirmationProps = {
  confirmation: Confirmation;
  /** On the viewer's own page, where the words are about your own account. */
  isSelf: boolean;
  onConfirm: () => void;
  onClose: () => void;
};

/**
 * The Reset sign-in or Deactivate dialog (users.md component C item 8), opened on its title, with the confirming
 * button first and Go back under it. Reset sign-in's button is red, as a reset cannot be undone (§8.6); Deactivate's is
 * the standard blue one, as Reactivate undoes it (users.md *Conflicts resolved* 3).
 */
export function UserConfirmation({ confirmation, isSelf, onConfirm, onClose }: UserConfirmationProps) {
  const { action, target, busy, error, canConfirm } = confirmation;
  const reset = action === 'resetSignIn';
  const { title, body, confirm } = reset ? resetWords(target, isSelf) : deactivateWords(target, isSelf);
  return (
    <ConfirmDialog
      title={title}
      primary={confirm}
      secondary={strings.goBack}
      confirmButton="primary"
      initialFocus="title"
      tone={reset ? 'warning' : 'standard'}
      onConfirm={onConfirm}
      busy={busy}
      busyLabel={reset ? strings.users.resetDialog.busy : strings.users.deactivateDialog.busy}
      error={error}
      canConfirm={canConfirm}
      onClose={onClose}
    >
      {body.map((line) => (
        <p key={line}>{line}</p>
      ))}
    </ConfirmDialog>
  );
}
