import { useLayoutEffect, useRef, useState, useSyncExternalStore, type KeyboardEvent, type Ref } from 'react';
import { Link, matchPath, useLocation } from 'react-router';
import { api, classify } from '../api/api.ts';
import type { Me } from '../api/me.ts';
import { flushSaves, getPhase, isWarning, subscribe } from '../api/session.ts';
import { Button } from '../components/Button.tsx';
import { msg } from '../copy/messages.ts';
import { pageName, type PageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';
import { fullPageLoad } from './fullPageLoad.ts';
import styles from './Header.module.css';
import layout from './layout.module.css';
import { confirmLeave, disarmUnloadGuard, rearmUnloadGuard } from './leaveGuard.ts';

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

const innerClass = `${layout.container} ${styles.inner}`;
const accountPanelId = 'account-panel';

type HeaderProps = {
  /** The signed-in user, or `undefined` while `/me` is pending or has failed, which shows the signed-out frame. */
  me: Me | undefined;
  /** The name of the page the shell shows. */
  page: PageName | undefined;
};

/**
 * The header (app-shell.md component 2): the wordmark, then, once `/me` has answered, the navigation for the person's
 * role and the Account button with its panel.
 */
export function Header({ me, page }: HeaderProps) {
  return me === undefined ? (
    <header className={styles.header}>
      <div className={innerClass}>
        <div className={styles.row}>
          <span className={styles.wordmark}>{strings.appName}</span>
        </div>
      </div>
    </header>
  ) : (
    <SignedInHeader me={me} page={page} />
  );
}

/**
 * A worker's one row, or a manager's two: the wordmark and Account, then the navigation; with the Account panel
 * straight after Account's row, in the flow, so that it pushes the page down rather than covering it (component 2d).
 */
function SignedInHeader({ me, page }: { me: Me; page: PageName | undefined }) {
  const { pathname, key } = useLocation();
  const accountRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  // Any location change closes the panel. It is closed during the render, as React advises for state that follows a
  // value, so that the new screen never shows it open, as it would until an effect had run.
  const [locationKey, setLocationKey] = useState(key);
  if (locationKey !== key) {
    setLocationKey(key);
    setOpen(false);
  }

  // So does the warning before the idle sign-out, as it opens (app-shell.md *28:00 idle on a visible page*): during the
  // render too, so that the panel is closed before the warning's effect opens it.
  const warning = useSyncExternalStore(subscribe, () => isWarning(getPhase()));
  const [warned, setWarned] = useState(warning);
  if (warned !== warning) {
    setWarned(warning);
    if (warning) {
      setOpen(false);
    }
  }

  // And focus in the panel goes to Account, as Escape takes it, so that the warning gives focus back to Account as it
  // closes, never to a hidden Sign out. A layout effect runs before the warning's effect, and the browser leaves focus
  // on a control just hidden until its next focus fix-up, so focus is still found in the panel here.
  useLayoutEffect(() => {
    if (warning && panelRef.current?.contains(document.activeElement)) {
      accountRef.current?.focus();
    }
  }, [warning]);

  // Page not found marks nothing, even at an address the table marks, such as /flagged for a worker, or for a manager
  // whose To review list the API refused, as after a change of role (flagged.md L20).
  const mark =
    page === pageName.pageNotFound ? undefined : markTable.find(({ path }) => matchPath(path, pathname) !== null);
  const isManager = me.role === 'Manager';

  // On the whole header, so that Escape closes the panel from a nav link and from Sign out as well as from Account.
  function closeOnEscape(event: KeyboardEvent<HTMLElement>) {
    if (event.key === 'Escape' && open) {
      setOpen(false);
      accountRef.current?.focus();
    }
  }

  const wordmark = (
    <Link to="/" className={styles.wordmark}>
      {strings.appName}
    </Link>
  );
  const account = (
    <Button
      ref={accountRef}
      className={styles.account}
      aria-expanded={open}
      aria-controls={accountPanelId}
      onClick={() => setOpen(!open)}
    >
      {strings.account}
      <svg className={styles.chevron} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
        <path d="M4.5 7.5 10 13l5.5-5.5" fill="none" stroke="currentColor" strokeWidth="2" />
      </svg>
    </Button>
  );
  const panel = <AccountPanel ref={panelRef} open={open} displayName={me.displayName} />;
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

  return (
    <header className={styles.header} onKeyDown={closeOnEscape}>
      <div className={innerClass}>
        {isManager ? (
          <>
            <div className={styles.row}>
              {wordmark}
              {account}
            </div>
            {panel}
            {nav}
          </>
        ) : (
          <>
            <div className={styles.row}>
              {wordmark}
              {nav}
              {account}
            </div>
            {panel}
          </>
        )}
      </div>
    </header>
  );
}

/**
 * The Account panel (component 2d): whose session it is, Sign out, and the line that says why a sign-out failed. The
 * line is in the panel from the first render, as a screen reader may not announce words written into a live region
 * that was added with them.
 */
function AccountPanel({ ref, open, displayName }: { ref: Ref<HTMLDivElement>; open: boolean; displayName: string }) {
  const [signingOut, setSigningOut] = useState(false);
  // Emptied as each sign-out starts, so that a failure that repeats is written into an empty line and announced again.
  const [failure, setFailure] = useState('');

  // *Press Sign out* and *Sign out fails* (app-shell.md).
  async function signOut() {
    // First of all, so that a page's Stay sends nothing and leaves the panel as it was.
    if (!(await confirmLeave())) {
      return;
    }

    setFailure('');
    setSigningOut(true);
    // A note form's change waiting for its save goes first, as the reload would lose it; one that cannot be saved now
    // keeps the person signed in, with the page as it is.
    if (!(await flushSaves())) {
      setFailure(strings.notSignedOutNoteNotSaved);
      setSigningOut(false);
      return;
    }

    // The person has chosen to leave, so the reload that follows the logout asks them nothing more.
    disarmUnloadGuard();
    try {
      await api('/api/auth/logout', { method: 'POST' });
    } catch (error) {
      const cause = classify(error);
      // A 401 counts as done: there was no session left to end.
      if (cause !== 'signedOut') {
        // The page is still there, with any text it has not saved, which a reload must warn about again.
        rearmUnloadGuard();
        setFailure(msg.notSignedOut(cause));
        setSigningOut(false);
        return;
      }
    }

    // Still busy, so that nothing more is sent while the page goes.
    fullPageLoad();
  }

  return (
    <div ref={ref} id={accountPanelId} className={styles.panel} hidden={!open}>
      <p className={styles.signedInAs}>{msg.signedInAs(displayName)}</p>
      {/* Its busy label is not announced: the panel's line says only why a sign-out failed (app-shell.md). */}
      <Button busy={signingOut} busyLabel={strings.signingOut} announceBusy={false} onClick={() => void signOut()}>
        {strings.signOut}
      </Button>
      <p role="status" className={styles.statusLine}>{failure}</p>
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
