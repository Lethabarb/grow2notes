import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { api, ApiError, classify } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { Notice } from '../../components/Notice.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { fullPageLoad } from '../../routes/fullPageLoad.ts';
import { PageNotFound } from '../../routes/PageNotFound.tsx';
import styles from './EditUserPage.module.css';
import { UserErrorSummary, UserFields } from './UserFields.tsx';
import {
  isNewAddress,
  isOnlyActiveManager,
  userRefusal,
  validateUser,
  type UserErrors,
  type UserValues,
} from './userRules.ts';
import { usersQuery, type AdminUser } from './usersQuery.ts';

/**
 * Where a saved edit takes the manager (users.md *States* › Edit user): to the user's page; to Today, after making
 * yourself a worker, as the Users screens are no longer yours; or to sign-in, by loading the app afresh, after giving
 * yourself a new address, which has ended your session.
 */
type Landing = 'user' | 'today' | 'signIn';

/** Where saving `sent` over `stored`, the user as the form had them, takes the manager, on their own page or not. */
function landingOf(sent: UserValues, stored: AdminUser, isSelf: boolean): Landing {
  if (!isSelf) {
    return 'user';
  }

  if (isNewAddress(sent.email, stored.email)) {
    return 'signIn';
  }

  return sent.role === 'Worker' ? 'today' : 'user';
}

/** A save of the user `target` as the form had them, whose stamp it sends, with the values sent and where it lands. */
type Edit = { target: AdminUser; body: UserValues; landing: Landing };

/** The user's values as the form shows them. */
const valuesOf = ({ displayName, email, role }: AdminUser): UserValues => ({ displayName, email, role });

/**
 * The route component of `/manage/users/{userId}/edit`: the page of the user whose ID the address holds, mounted
 * afresh for each, as the router keeps this component across two entries of the route, so that a history jump from
 * one user's Edit user to another's never keeps the first one's values and stamp.
 */
export function EditUserPage() {
  const { userId } = useParams();
  return <EditUser key={userId} userId={userId} />;
}

/**
 * Edit user at `/manage/users/{userId}/edit` (users.md component D, *States* › Edit user and *Interactions*): the back
 * link to the user's page, named by them, then the error summary after a failed Save, the `<h1>` "Edit user", shown at
 * once, and the region that loads the organisation's users, as there is no single-user endpoint, which holds, once the
 * user is found by ID, the last-manager notice on your own page as the only active manager, Name, Email address with
 * its hint for someone else or for yourself, the Role radios on the person's role, and Save under its alert line; or,
 * for an ID not on the list, Page not found in place of the whole page.
 *
 * The form takes the user's values once, as it first fills, and the stamp that Save sends as `If-Match` with them; a
 * refetch changes neither, and the stamp changes only after a `412`, which keeps every value as typed. Save checks the
 * fields first, the last-manager rule among them, and sends nothing while one is at fault; then it sends them, trimmed,
 * once, however often it is pressed while busy, and is never sent again by itself. The server's refusal of a field
 * shows as the form's own checks do, and a failed request in the alert line, with focus left on Save. A save lands on
 * the user's page by a replace, once the list fetched again holds the change, whose `<h1>` the shell's route-change
 * rule focuses, with no banner; or, for your own change, as `landingOf()` says. The typed values are kept in this
 * page's memory alone, never in the address, the history's state or storage (AC3).
 */
function EditUser({ userId }: { userId: string | undefined }) {
  const me = useMe();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const users = useQuery(usersQuery);
  // Found in the list rather than picked out by a `select`, whose `undefined` for an ID not on it the region would
  // take for a list not loaded yet (S00.05.03).
  const user = users.data?.find(({ id }) => id === userId);
  // The user as the form took them, as it first filled or from the list fetched again after a `412`: whose stamp Save
  // sends, and against whose values it tells a new address and a change to Worker.
  const [stored, setStored] = useState<AdminUser>();
  const [values, setValues] = useState<UserValues>();
  const [errors, setErrors] = useState<UserErrors>({});
  const [unknownField, setUnknownField] = useState(false);
  // The number of failed presses, so that the summary takes focus on each, even with the same words.
  const [attempt, setAttempt] = useState(0);
  // Set in the press itself, rather than read from the mutation, whose state TanStack hands on only after a task, by
  // when a second tap of a double tap would have sent the save again.
  const [saving, setSaving] = useState(false);
  const [failure, setFailure] = useState<string>();

  // Filled once, as the user is first found. Set during render, as an effect that set it would render twice.
  if (stored === undefined && user !== undefined) {
    setStored(user);
    setValues(valuesOf(user));
  }

  const refetchUsers = () => queryClient.refetchQueries({ queryKey: usersQuery.queryKey });
  const edit = useMutation({
    mutationFn: async ({ target, body, landing }: Edit) => {
      try {
        await api(`/api/admin/users/${target.id}`, { method: 'PUT', body, ifMatch: target.concurrencyStamp });
      } catch (error) {
        // For the user's new stamp after a 412, and for the notice that the page shows after the last-manager race.
        if (error instanceof ApiError && (error.status === 412 || error.code === 'user.last_manager')) {
          await refetchUsers();
        }

        throw error;
      }

      // Awaited, so that the user's page shows the change, Status included (users.md *E. Data and helpers*,
      // Mutations). Not after your own new address, which has ended your session, or your own change to Worker, after
      // which the list is no longer yours to fetch (design.md §8.4).
      if (landing === 'user') {
        await refetchUsers();
      }
    },
  });

  // A 200 that does not hold the ID, so the page shows Page not found itself, with nothing of its own beside it, the
  // back link included, as UserPage does.
  if (users.data !== undefined && user === undefined) {
    return <PageNotFound />;
  }

  const isSelf = userId === me.userId;
  // As the list gives it now, so that the notice and the rule's checks follow a manager added or gone meanwhile.
  const onlyManager = isSelf && users.data !== undefined && isOnlyActiveManager(users.data, me.userId);
  // You as the form took you, against whom the rule tells a new address and a change to Worker, while it applies.
  const ruledBy = onlyManager ? stored : undefined;

  function change(next: UserValues, field: keyof UserValues) {
    setValues(next);
    // Once a field shows a message, each change checks that field again, and the message goes as soon as it passes
    // (form-validation.md *Fixing*); a field with no message is checked on the next press only.
    if (errors[field] !== undefined) {
      setErrors((shown) => ({ ...shown, [field]: validateUser(next, ruledBy)[field] }));
    }
  }

  // Save, pressed or reached by Enter in a field. While busy, the button ignores both.
  function save(event: FormEvent<HTMLFormElement>, target: AdminUser, typed: UserValues) {
    event.preventDefault();
    // Emptied, so that a failure that repeats is written into an empty region and announced again.
    setFailure(undefined);
    setUnknownField(false);
    const found = validateUser(typed, ruledBy);
    setErrors(found);
    if (Object.keys(found).length > 0) {
      setAttempt((count) => count + 1);
      return;
    }

    const body: UserValues = { displayName: typed.displayName.trim(), email: typed.email.trim(), role: typed.role };
    const landing = landingOf(body, target, isSelf);
    setSaving(true);
    // TanStack calls these only while the page is still shown, so that a save that ends after the manager has gone
    // elsewhere takes them nowhere.
    edit.mutate(
      { target, body, landing },
      {
        // Each in place of the form, so that Back does not return to it (users.md *Interactions*). On a new entry,
        // which the shell refreshes `/me` for, so that your own new name shows in the Account panel, and your own
        // change to Worker takes Manage from the nav.
        onSuccess: () => {
          switch (landing) {
            case 'user':
              void navigate(`/manage/users/${target.id}`, { replace: true });
              break;
            case 'today':
              void navigate('/', { replace: true });
              break;
            case 'signIn':
              // Still busy, so that nothing more is sent while the page goes.
              fullPageLoad();
              break;
          }
        },
        onError: (error) => {
          setSaving(false);
          if (error instanceof ApiError && error.status === 412) {
            const now = queryClient.getQueryData(usersQuery.queryKey)?.find(({ id }) => id === target.id);
            if (now !== undefined) {
              setStored(now);
            }

            setFailure(strings.users.changedWhileEditing);
            return;
          }

          const refusal = userRefusal(error, body, target);
          if (refusal !== undefined) {
            setErrors(refusal.errors);
            setUnknownField(refusal.unknownField);
            setAttempt((count) => count + 1);
            return;
          }

          // Nothing for a 401, which the session flow has.
          const cause = classify(error);
          if (cause !== 'signedOut') {
            setFailure(msg.notSaved(cause));
          }
        },
      },
    );
  }

  return (
    <PageStatus>
      <PageBar>
        {/* Named by the user once the list has them, and until then by the page that the link goes to. */}
        <BackLink to={`/manage/users/${userId}`}>{user?.displayName ?? pageName.user}</BackLink>
      </PageBar>
      <div className={styles.view}>
        <UserErrorSummary
          errors={errors}
          unknownField={unknownField ? msg.notSaved('server') : undefined}
          attempt={attempt}
        />
        <PageHeading>{pageName.editUser}</PageHeading>
        <LoadRegion queries={[users]} loading={strings.users.userLoading} what={strings.users.thisUser}>
          {() =>
            stored !== undefined &&
            values !== undefined && (
              <>
                {onlyManager && (
                  <div className={styles.notice}>
                    <Notice>{strings.users.editLastManagerNotice}</Notice>
                  </div>
                )}
                <form noValidate onSubmit={(event) => save(event, stored, values)}>
                  <UserFields
                    values={values}
                    errors={errors}
                    emailHint={isSelf ? strings.users.ownEmailHint : strings.users.editEmailHint}
                    onChange={change}
                  />
                  <RequestAlert>{failure}</RequestAlert>
                  <Button type="submit" variant="primary" busy={saving} busyLabel={strings.saving}>
                    {strings.save}
                  </Button>
                </form>
              </>
            )
          }
        </LoadRegion>
      </div>
    </PageStatus>
  );
}
