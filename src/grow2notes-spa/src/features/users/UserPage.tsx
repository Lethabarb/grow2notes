import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router';
import { BackLink } from '../../components/BackLink.tsx';
import { buttonClass } from '../../components/buttonClass.ts';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { SummaryList } from '../../components/SummaryList.tsx';
import { SummaryRow } from '../../components/SummaryRow.tsx';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { PageNotFound } from '../../routes/PageNotFound.tsx';
import { usersQuery, type AdminUser } from './usersQuery.ts';
import styles from './UserPage.module.css';

/**
 * A user's detail at `/manage/users/{userId}` (users.md component C): the back link to Users, then the region that
 * loads the organisation's users, as there is no single-user endpoint, and the user's name, facts and Edit; or, for
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
        {() => <UserDetail user={user!} />}
      </LoadRegion>
    </PageStatus>
  );
}

/**
 * The user's name as the `<h1>`, their email, role and status in plain words, and Edit, a link styled as a secondary
 * button, as it changes the address; the other actions come with the stories that build them.
 */
function UserDetail({ user }: { user: AdminUser }) {
  return (
    <>
      <PageHeading>{user.displayName}</PageHeading>
      <SummaryList>
        <SummaryRow term={strings.emailAddress}>{user.email}</SummaryRow>
        <SummaryRow term={strings.users.role}>{strings.role[user.role]}</SummaryRow>
        <SummaryRow term={strings.users.status}>{strings.userStatus[user.status]}</SummaryRow>
      </SummaryList>
      <div className={styles.actions}>
        <Link to="edit" className={buttonClass('secondary')}>
          {strings.users.edit}
        </Link>
      </div>
    </>
  );
}
