import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState, type MouseEvent } from 'react';
import { Outlet, useMatches, type UIMatch } from 'react-router';
import { classify } from '../api/api.ts';
import { meQuery } from '../api/me.ts';
import { focusPageHeading } from '../components/focusPageHeading.ts';
import { PageBarContext } from '../components/pageBarContext.ts';
import { pageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';
import { SignIn } from '../features/auth/SignIn.tsx';
import styles from './AppShell.module.css';
import { Header } from './Header.tsx';
import layout from './layout.module.css';
import type { RouteHandle } from './routes.tsx';
import { usePageTitle } from './usePageTitle.ts';
import { useRefreshMe } from './useRefreshMe.ts';

const mainId = 'main-content';

/**
 * The frame around every screen (app-shell.md components 1 to 3a): the skip link, the header, the before-main bar
 * slot and the one `<main>`, which holds the route's screen once `GET /api/auth/me` has answered `200`. Until then,
 * and after a failure, it is the signed-out frame, with the wordmark as plain text: from a `401` its `<main>` holds
 * sign-in's view A, at the address that was opened (component 4); otherwise `<main>` is empty and the title bare. A
 * sign-in puts its `Me` in the same query, so the route's screen then shows in sign-in's place, at the same address,
 * with no new history entry. A route whose handle says `signedOutFrame`, setup's, shows its page in the signed-out
 * frame at once, whatever `/me` answers and whoever is signed in (sign-in.md component 1).
 */
export function AppShell() {
  useRefreshMe();
  const { data: me, error } = useQuery(meQuery);
  // Data first, as a refresh that fails keeps the data it had, so the person stays signed in on screen (app-shell.md
  // *Refresh*).
  const signedIn = me !== undefined;
  // The table's type gives every route's handle as a RouteHandle, or none.
  const matches = useMatches() as UIMatch<unknown, RouteHandle | undefined>[];
  const handle = matches.findLast((match) => match.handle !== undefined)?.handle;
  const signedOutFrame = handle?.signedOutFrame === true;
  const shown = signedIn || signedOutFrame;
  // Sign-in's view A shows from the first `401`, while no answer has come, until one does: a refresh leaves it as it
  // was while in flight, when TanStack puts a query with no answer back to pending with no error, and when it fails in
  // any way (app-shell.md *Window regains focus*), so that a passkey prompt open in it is not cut short. Set during
  // render, as an effect that set it would render twice.
  const [answeredSignedOut, setAnsweredSignedOut] = useState(false);
  if (!answeredSignedOut && !signedIn && error !== null && classify(error) === 'signedOut') {
    setAnsweredSignedOut(true);
  }
  const signedOut = !shown && answeredSignedOut;

  // Whether sign-in has shown, so that the screen that a sign-in brings takes focus on its <h1>, as a new screen does
  // (sign-in.md *Success, fresh load*), while a screen shown on a first load moves no focus (app-shell.md component 7).
  const signInShown = useRef(false);
  useEffect(() => {
    if (signedOut) {
      signInShown.current = true;
    } else if (signedIn && signInShown.current) {
      signInShown.current = false;
      focusPageHeading();
    }
  }, [signedOut, signedIn]);

  // The signed-out frame has the bare title, but on a page shown in it, or sign-in's while view A shows (sign-in.md
  // component 1). A page that sets its own, as Page not found does, sets it in a `useEffect`, which comes after this
  // hook's layout effect (app-shell-nav.md *Titles*).
  usePageTitle(shown ? handle?.pageName : signedOut ? pageName.signIn : undefined);
  // The before-main bar slot once it is in the DOM, for a screen's PageBar. State, not a ref, so that a screen shown
  // with the shell's first render, before the slot exists, renders again with it.
  const [pageBar, setPageBar] = useState<HTMLDivElement | null>(null);

  return (
    <>
      <a href={`#${mainId}`} className={styles.skipLink} onClick={skipToMain}>
        {strings.skipToMainContent}
      </a>
      <Header me={signedOutFrame ? undefined : me} page={handle?.pageName} />
      <div id="page-bar" ref={setPageBar} className={styles.pageBar} />
      <PageBarContext value={pageBar}>
        <main id={mainId} tabIndex={-1} className={`${layout.container} ${styles.main}`}>
          {shown && <Outlet />}
          {signedOut && <SignIn />}
        </main>
      </PageBarContext>
    </>
  );
}

// Focuses <main> without following the link, so that #main-content never enters the address or the history, and the
// router sees no navigation (app-shell.md component 1).
function skipToMain(event: MouseEvent<HTMLAnchorElement>) {
  event.preventDefault();
  document.getElementById(mainId)?.focus();
}
