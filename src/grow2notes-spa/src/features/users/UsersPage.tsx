import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { buttonClass } from '../../components/buttonClass.ts';
import { LinkRow } from '../../components/LinkRow.tsx';
import { LinkRowList } from '../../components/LinkRowList.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { pageName } from '../../copy/pageNames.ts';
import { Sep } from '../../copy/Sep.tsx';
import { strings } from '../../copy/strings.ts';
import { byName } from './userRules.ts';
import { usersQuery, type AdminUser } from './usersQuery.ts';
import styles from './UsersPage.module.css';

/**
 * Users at `/manage/users` (users.md component A): its `<h1>` and Invite user at once, then the region that loads the
 * organisation's users, the current ones, Invited and Active, and then, only when someone is Deactivated, their own
 * section. Invite user is a link, as it changes the address, styled as the primary button.
 */
export function UsersPage() {
  const users = useQuery(usersQuery);

  return (
    <PageStatus>
      <PageHeading>{pageName.users}</PageHeading>
      <div className={styles.invite}>
        <Link to="/manage/users/new" className={buttonClass('primary')}>
          {pageName.inviteUser}
        </Link>
      </div>
      <LoadRegion queries={[users]} loading={strings.users.listLoading} what={strings.users.theList}>
        {/* Called only once the query has data. */}
        {() => <UserLists users={users.data!} />}
      </LoadRegion>
    </PageStatus>
  );
}

/** The current users, then the Deactivated under their `<h2>`, each sorted by name. */
function UserLists({ users }: { users: readonly AdminUser[] }) {
  const sorted = users.toSorted(byName);
  const current = sorted.filter(({ status }) => status !== 'Deactivated');
  const deactivated = sorted.filter(({ status }) => status === 'Deactivated');

  return (
    <>
      <UserRows users={current} />
      {deactivated.length > 0 && (
        <>
          <h2 className={styles.section}>{strings.userStatus.Deactivated}</h2>
          <UserRows users={deactivated} />
        </>
      )}
    </>
  );
}

/**
 * A list of users, each a row named by their name alone, the viewer's own with no "(you)", with their email and
 * "{Status} · {Role}" under it, in plain words, never faded or coloured (users.md component A item 6).
 */
function UserRows({ users }: { users: readonly AdminUser[] }) {
  return (
    <LinkRowList>
      {users.map(({ id, displayName, email, status, role }) => (
        <LinkRow
          key={id}
          to={`/manage/users/${id}`}
          name={displayName}
          detail={[
            email,
            <>
              {strings.userStatus[status]}
              <Sep />
              {strings.role[role]}
            </>,
          ]}
        />
      ))}
    </LinkRowList>
  );
}
