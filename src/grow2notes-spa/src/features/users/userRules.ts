import { ApiError } from '../../api/api.ts';
import type { Role } from '../../api/me.ts';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import type { AdminUser } from './usersQuery.ts';

// Letters alone, so that neither letter case nor an accent sets two names apart (users.md component A item 6).
const collator = new Intl.Collator('en-AU', { sensitivity: 'base' });

/**
 * Compares two users by name, as the Users list sorts them, since the API sorts nothing (design.md §6.6) and SQL
 * Server's collation would not sort as `Intl.Collator` does.
 */
export function byName(a: AdminUser, b: AdminUser): number {
  return collator.compare(a.displayName, b.displayName);
}

/** An action on a user's page (users.md component C). */
export type UserAction = 'edit' | 'resendInvite' | 'resetSignIn' | 'deactivate' | 'reactivate';

/**
 * Whether the user `id` is the organisation's only Active manager (A26): exactly one of `users` is a manager whose
 * status is Active, and it is them. The page asks it of the viewer, whose ID `['me']` gives.
 */
export function isOnlyActiveManager(users: readonly AdminUser[], id: string): boolean {
  const activeManagers = users.filter(({ role, status }) => role === 'Manager' && status === 'Active');
  return activeManagers.length === 1 && activeManagers[0].id === id;
}

/**
 * The actions that a user's page offers for `user` now, in the order that it shows them (users.md component C, *Which
 * actions show, by status*): Edit for every status; Resend invite while Invited, or Reset sign-in while Active; then,
 * after the rule, Deactivate while Invited or Active, or Reactivate while Deactivated. `onlyManager`, on the viewer's
 * own page while they are the only Active manager, takes away Reset sign-in and Deactivate, whose place the
 * last-manager notice takes. The server is still the gate (`409 user.last_manager`).
 */
export function allowedActions(user: AdminUser, onlyManager: boolean): readonly UserAction[] {
  const actions: UserAction[] = ['edit'];
  if (user.status === 'Invited') {
    actions.push('resendInvite');
  }

  if (user.status === 'Active' && !onlyManager) {
    actions.push('resetSignIn');
  }

  if (user.status === 'Deactivated') {
    actions.push('reactivate');
  } else if (!onlyManager) {
    actions.push('deactivate');
  }

  return actions;
}

/** A user's fields as a manager types them, by the names that the API's JSON gives them (design.md §6.6). */
export type UserValues = { displayName: string; email: string; role: Role };

/** The message of each of a user's fields that is at fault, in the words of users.md's *States*. */
export type UserErrors = Partial<Record<keyof UserValues, string>>;

/**
 * What is wrong with a user's fields before anything is sent (users.md *States* › Invite user), each checked as it is
 * sent, trimmed: a name that is empty or longer than `limits.displayName`, counted in UTF-16 units, as the server
 * counts it; and an address that is empty or not text, one "@" and text, form-validation.md's loose check, which
 * leaves the rest to the server. The role needs no check, as one of its radios is always chosen.
 */
export function validateUser({ displayName, email }: UserValues): UserErrors {
  const errors: UserErrors = {};
  const name = displayName.trim();
  if (name === '') {
    errors.displayName = strings.users.enterName;
  } else if (name.length > limits.displayName) {
    errors.displayName = msg.tooLong(strings.name, limits.displayName);
  }

  const address = email.trim();
  if (address === '') {
    errors.email = strings.users.enterEmail;
  } else if (!/^[^@]+@[^@]+$/.test(address)) {
    errors.email = strings.emailFormat;
  }

  return errors;
}

/** The server's refusal of a user's fields, which the form shows as it shows its own checks. */
export type UserRefusal = {
  errors: UserErrors;
  /** The server named a field that the form does not have, which the error summary lists with no link. */
  unknownField: boolean;
};

/**
 * The fields at fault in the server's answer to `sent` (users.md *States* › Invite user), always in the form's own
 * words, never the server's: `409 user.email_in_use` on the address; and each key of a `422`'s `errors{}`, the name's
 * message for what was sent, the address's format message, as the server refuses only an address that the loose
 * check passes but no account can hold, and Role's, while any other key is an unknown field. Anything else, which
 * says nothing of a field, gives `undefined`.
 */
export function userRefusal(error: unknown, sent: UserValues): UserRefusal | undefined {
  if (!(error instanceof ApiError)) {
    return undefined;
  }

  if (error.status === 409 && error.code === 'user.email_in_use') {
    return { errors: { email: apiErrorCopy['user.email_in_use'] }, unknownField: false };
  }

  const keys = Object.keys(error.errors ?? {});
  if (keys.length === 0) {
    return undefined;
  }

  const refusal: UserRefusal = { errors: {}, unknownField: false };
  for (const key of keys) {
    switch (key) {
      case 'displayName':
        refusal.errors.displayName =
          sent.displayName === '' ? strings.users.enterName : msg.tooLong(strings.name, limits.displayName);
        break;
      case 'email':
        refusal.errors.email = strings.emailFormat;
        break;
      case 'role':
        refusal.errors.role = strings.users.chooseRole;
        break;
      default:
        refusal.unknownField = true;
    }
  }

  return refusal;
}
