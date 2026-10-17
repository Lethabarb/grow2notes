import { useQuery } from '@tanstack/react-query';
import { useRef } from 'react';
import { useParams } from 'react-router';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { SummaryList } from '../../components/SummaryList.tsx';
import { SummaryRow } from '../../components/SummaryRow.tsx';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { PageNotFound } from '../../routes/PageNotFound.tsx';
import { UserActions } from './UserActions.tsx';
import { isOnlyActiveManager } from './userRules.ts';
import { usersQuery, type AdminUser } from './usersQuery.ts';

/**
 * A user's detail at `/manage/users/{userId}` (users.md component C): the back link to Users, then the region that
 * loads the organisation's users, as there is no single-user endpoint, and the user's name, facts and actions; or, for
 * an ID not on the list, Page not found in place of the whole page.
 */
export function UserPage() {
  const { userId } = useParams();
  const users = useQuery(usersQuery);
  // Found in the list rather than picked out by a `select`, whose `undefined` for an ID not on it the region would
  // take for a list not loaded yet (S00.05.03).
  const user = users.data?.find(({ id }) => id === userId);

  // A 200 that does not hold the ID, so the page shows Page not found itself, with nothing of its own beside it, the
  // back link included, as the route's boundary does for a refusal.
  if (users.data !== undefined && user === undefined) {
    return <PageNotFound />;
  }

  return (
    <PageStatus>
      <PageBar>
        <BackLink to="/manage/users">{pageName.users}</BackLink>
      </PageBar>
      <LoadRegion
        queries={[users]}
        loading={strings.users.userLoading}
        what={strings.users.thisUser}
        fallbackHeading={pageName.user}
      >
        {/* Called only once the query has data, which then holds the user. */}
        {() => <UserDetail user={user!} users={users.data!} />}
      </LoadRegion>
    </PageStatus>
  );
}

/**
 * The user's name as the `<h1>`, their email, role and status in plain words, and the actions for their status, among
 * `users`, the organisation's. The Status row takes focus when an action has changed the status, and so taken its own
 * button away, as it shows what changed, in the place it is shown (users.md component C item 2).
 */
function UserDetail({ user, users }: { user: AdminUser; users: readonly AdminUser[] }) {
  const { userId: viewerId } = useMe();
  const isSelf = user.id === viewerId;
  const statusRow = useRef<HTMLDivElement>(null);
  return (
    <>
      <PageHeading>{user.displayName}</PageHeading>
      <SummaryList>
        <SummaryRow term={strings.emailAddress}>{user.email}</SummaryRow>
        <SummaryRow term={strings.users.role}>{strings.role[user.role]}</SummaryRow>
        <SummaryRow ref={statusRow} tabIndex={-1} term={strings.users.status}>
          {strings.userStatus[user.status]}
        </SummaryRow>
      </SummaryList>
      <UserActions
        user={user}
        isSelf={isSelf}
        onlyManager={isSelf && isOnlyActiveManager(users, viewerId)}
        focusStatus={() => statusRow.current?.focus()}
      />
    </>
  );
}
