import { useQuery } from '@tanstack/react-query';
import { useLayoutEffect, type MouseEvent } from 'react';
import { Outlet, useLocation, useMatches, type UIMatch } from 'react-router';
import { meQuery } from '../api/me.ts';
import { msg } from '../copy/messages.ts';
import { strings } from '../copy/strings.ts';
import styles from './AppShell.module.css';
import { Header } from './Header.tsx';
import layout from './layout.module.css';
import type { RouteHandle } from './routes.tsx';

const mainId = 'main-content';

/**
 * The frame around every screen (app-shell.md components 1 to 3a): the skip link, the header, the before-main bar
 * slot and the one `<main>`, which holds the route's screen once `GET /api/auth/me` has answered `200`. Until then,
 * and after a failure, it is the signed-out frame: the wordmark as plain text, an empty `<main>` and the bare title.
 */
export function AppShell() {
  // A later refresh that fails keeps the data it had, so the person stays signed in on screen (app-shell.md *Refresh*).
  const me = useQuery(meQuery).data;
  const signedIn = me !== undefined;
  const { key } = useLocation();
  // The table's type gives every route's handle as a RouteHandle, or none.
  const matches = useMatches() as UIMatch<unknown, RouteHandle | undefined>[];
  const name = matches.findLast(({ handle }) => handle !== undefined)?.handle?.pageName;

  // Set again on each location change, not only when the name changes, so that a title a screen changed itself, such
  // as a form's "Error: " in front, does not outlive the screen. In a layout effect, which React runs before any
  // `useEffect` of the same render, so that a page that sets its own title in one, as Page not found does, comes last.
  useLayoutEffect(() => {
    document.title = signedIn && name !== undefined ? msg.pageTitle(name) : strings.appName;
  }, [signedIn, name, key]);

  return (
    <>
      <a href={`#${mainId}`} className={styles.skipLink} onClick={skipToMain}>
        {strings.skipToMainContent}
      </a>
      <Header me={me} page={name} />
      <div id="page-bar" className={styles.pageBar} />
      <main id={mainId} tabIndex={-1} className={`${layout.container} ${styles.main}`}>
        {signedIn && <Outlet />}
      </main>
    </>
  );
}

// Focuses <main> without following the link, so that #main-content never enters the address or the history, and the
// router sees no navigation (app-shell.md component 1).
function skipToMain(event: MouseEvent<HTMLAnchorElement>) {
  event.preventDefault();
  document.getElementById(mainId)?.focus();
}
