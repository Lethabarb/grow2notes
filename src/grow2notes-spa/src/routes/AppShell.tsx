import { useQuery } from '@tanstack/react-query';
import type { MouseEvent } from 'react';
import { Outlet, useMatches, type UIMatch } from 'react-router';
import { meQuery } from '../api/me.ts';
import { strings } from '../copy/strings.ts';
import styles from './AppShell.module.css';
import { Header } from './Header.tsx';
import layout from './layout.module.css';
import type { RouteHandle } from './routes.tsx';
import { usePageTitle } from './usePageTitle.ts';

const mainId = 'main-content';

/**
 * The frame around every screen (app-shell.md components 1 to 3a): the skip link, the header, the before-main bar
 * slot and the one `<main>`, which holds the route's screen once `GET /api/auth/me` has answered `200`. Until then,
 * and after a failure, it is the signed-out frame: the wordmark as plain text, an empty `<main>` and the bare title.
 * A route whose handle says `signedOutFrame`, setup's, shows its page in the signed-out frame at once, whatever `/me`
 * answers and whoever is signed in (sign-in.md component 1).
 */
export function AppShell() {
  // A later refresh that fails keeps the data it had, so the person stays signed in on screen (app-shell.md *Refresh*).
  const me = useQuery(meQuery).data;
  const signedIn = me !== undefined;
  // The table's type gives every route's handle as a RouteHandle, or none.
  const matches = useMatches() as UIMatch<unknown, RouteHandle | undefined>[];
  const handle = matches.findLast((match) => match.handle !== undefined)?.handle;
  const signedOutFrame = handle?.signedOutFrame === true;
  const shown = signedIn || signedOutFrame;
  // The signed-out frame has the bare title, but on a page shown in it. A page that sets its own, as Page not found
  // does, sets it in a `useEffect`, which comes after this hook's layout effect (app-shell-nav.md *Titles*).
  usePageTitle(shown ? handle?.pageName : undefined);

  return (
    <>
      <a href={`#${mainId}`} className={styles.skipLink} onClick={skipToMain}>
        {strings.skipToMainContent}
      </a>
      <Header me={signedOutFrame ? undefined : me} page={handle?.pageName} />
      <div id="page-bar" className={styles.pageBar} />
      <main id={mainId} tabIndex={-1} className={`${layout.container} ${styles.main}`}>
        {shown && <Outlet />}
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
