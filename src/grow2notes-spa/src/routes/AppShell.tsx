import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { Outlet, useMatches, type UIMatch } from 'react-router';
import { classify } from '../api/api.ts';
import { meQuery } from '../api/me.ts';
import { focusPageHeading } from '../components/focusPageHeading.ts';
import { pageName, type PageName } from '../copy/pageNames.ts';
import { SignIn } from '../features/auth/SignIn.tsx';
import type { RouteHandle } from './routes.tsx';
import { ShellFrame } from './ShellFrame.tsx';
import { StartUp } from './StartUp.tsx';
import { usePageTitle } from './usePageTitle.ts';
import { useRefreshMe } from './useRefreshMe.ts';
import { WholePageMessageContext } from './wholePageMessage.ts';

/**
 * The shell around every screen, in its frame (app-shell.md components 1 to 3a), whose `<main>` holds the route's
 * screen once `GET /api/auth/me` has answered `200`. Until then, and after a failure, it is the signed-out frame, with
 * the wordmark as plain text: from a `401` its `<main>` holds sign-in's view A, at the address that was opened;
 * otherwise it holds the start-up's loading line, or its message for a failure, with Try again (component 4). A
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
  // The page name of a whole-page message in place of the route's screen, such as the crash page, which the title and
  // the header take from the route's while it shows (useWholePageMessage).
  const [message, setMessage] = useState<PageName>();
  const page = message ?? handle?.pageName;
  // Sign-in's view A shows from the first `401`, while no answer has come, until one does: a refresh leaves it as it
  // was while in flight, when TanStack puts a query with no answer back to pending with no error, and when it fails in
  // any way (app-shell.md *Window regains focus*), so that a passkey prompt open in it is not cut short. Set during
  // render, as an effect that set it would render twice.
  const [answeredSignedOut, setAnsweredSignedOut] = useState(false);
  if (!answeredSignedOut && !signedIn && error !== null && classify(error) === 'signedOut') {
    setAnsweredSignedOut(true);
  }
  const signedOut = !shown && answeredSignedOut;
  const startingUp = !shown && !answeredSignedOut;

  // Whether sign-in has shown, so that the screen that a sign-in brings takes focus on its <h1>, as a new screen does
  // (sign-in.md *Success, fresh load*), while a screen shown on a first load moves no focus (app-shell.md component 7).
  const signInShown = useRef(false);
  // Set once the start-up message's Try again has taken focus or been pressed, so that the screen or sign-in that the
  // answer brings takes focus on its <h1>, as the button has gone (app-shell.md *Accessibility checklist*). Not cleared
  // as focus leaves it, as a blur can come as the button itself goes.
  const tryAgainUsed = useRef(false);
  useEffect(() => {
    if (!signedIn && !signedOut) {
      return;
    }

    // Only when focus is on the page itself, where it falls as Try again goes, as LoadRegion's does: a person who has
    // moved on to another control before the answer came stays there, while focus that a click on blank space left on
    // the page goes to the <h1> too.
    const afterTryAgain =
      tryAgainUsed.current && (document.activeElement === document.body || document.activeElement === null);
    const afterSignIn = signedIn && signInShown.current;
    tryAgainUsed.current = false;
    signInShown.current = signedOut;
    if (afterTryAgain || afterSignIn) {
      focusPageHeading();
    }
  }, [signedOut, signedIn]);

  // The signed-out frame has the bare title, but on a page shown in it, or sign-in's while view A shows (sign-in.md
  // component 1). A whole-page message in place of the screen names it while it shows. A page that sets its own, as
  // Page not found does, sets it in a `useEffect`, which comes after this hook's layout effect (app-shell-nav.md
  // *Titles*).
  usePageTitle(shown ? page : signedOut ? pageName.signIn : undefined);

  return (
    <ShellFrame me={signedOutFrame ? undefined : me} page={page}>
      {shown && (
        <WholePageMessageContext value={setMessage}>
          <Outlet />
        </WholePageMessageContext>
      )}
      {signedOut && <SignIn />}
      {startingUp && (
        <StartUp
          onTryAgainUsed={() => {
            tryAgainUsed.current = true;
          }}
        />
      )}
    </ShellFrame>
  );
}
