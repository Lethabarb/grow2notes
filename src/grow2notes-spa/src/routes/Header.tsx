import { Link, matchPath, useLocation } from 'react-router';
import type { Me } from '../api/me.ts';
import { Button } from '../components/Button.tsx';
import { pageName, type PageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';
import styles from './Header.module.css';
import layout from './layout.module.css';

type NavItemKey = keyof typeof strings.navItem;

/** The item an address marks as current, with its `aria-current`. */
type Mark = { item: NavItemKey; current: 'page' | 'true' };

// app-shell.md component 2b's table, first match first. Flagged's views are query strings, so its path alone counts;
// a note's address, and any other, marks nothing.
const markTable: readonly (Mark & { path: string })[] = [
  { path: '/', item: 'today', current: 'page' },
  { path: '/flagged', item: 'flagged', current: 'page' },
  { path: '/reports/daily/:date', item: 'report', current: 'page' },
  { path: '/manage', item: 'manage', current: 'page' },
  { path: '/manage/*', item: 'manage', current: 'true' },
];

type HeaderProps = {
  /** The signed-in user, or `undefined` while `/me` is pending or has failed, which shows the signed-out frame. */
  me: Me | undefined;
  /** The name of the page the shell shows. */
  page: PageName | undefined;
};

/**
 * The header (app-shell.md component 2): the wordmark, then, once `/me` has answered, the navigation for the person's
 * role and the Account button.
 */
export function Header({ me, page }: HeaderProps) {
  return (
    <header className={styles.header}>
      <div className={`${layout.container} ${styles.inner}`}>
        {me === undefined ? (
          <div className={styles.row}>
            <span className={styles.wordmark}>{strings.appName}</span>
          </div>
        ) : (
          <SignedInRows me={me} page={page} />
        )}
      </div>
    </header>
  );
}

/** A worker's one row, or a manager's two: the wordmark and Account, then the navigation. */
function SignedInRows({ me, page }: { me: Me; page: PageName | undefined }) {
  const { pathname } = useLocation();
  // Page not found marks nothing, even at an address the table marks, such as /flagged until E03 builds Flagged.
  const mark =
    page === pageName.pageNotFound ? undefined : markTable.find(({ path }) => matchPath(path, pathname) !== null);
  const isManager = me.role === 'Manager';

  const wordmark = (
    <Link to="/" className={styles.wordmark}>
      {strings.appName}
    </Link>
  );
  const account = <Button className={styles.account}>{strings.account}</Button>;
  // Report opens on the server's today in Melbourne (A33), so that the report needs no redirect to today's date.
  const nav = (
    <nav aria-label={strings.mainNavigation}>
      <ul className={styles.navList}>
        <NavItem item="today" to="/" mark={mark} />
        {isManager && (
          <>
            <NavItem item="flagged" to="/flagged" mark={mark} />
            <NavItem item="report" to={`/reports/daily/${me.today}`} mark={mark} />
            <NavItem item="manage" to="/manage" mark={mark} />
          </>
        )}
      </ul>
    </nav>
  );

  return isManager ? (
    <>
      <div className={styles.row}>
        {wordmark}
        {account}
      </div>
      {nav}
    </>
  ) : (
    <div className={styles.row}>
      {wordmark}
      {nav}
      {account}
    </div>
  );
}

/**
 * One item of the main navigation: a plain `Link`, not React Router's `NavLink`, which would say "page" on a page
 * inside a section, where component 2b's table says "true".
 */
function NavItem({ item, to, mark }: { item: NavItemKey; to: string; mark: Mark | undefined }) {
  return (
    <li>
      <Link to={to} className={styles.navLink} aria-current={mark?.item === item ? mark.current : undefined}>
        {strings.navItem[item]}
      </Link>
    </li>
  );
}
