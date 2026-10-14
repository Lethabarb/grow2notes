import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { Outlet, useMatches, type UIMatch } from 'react-router';
import { classify } from '../api/api.ts';
import { meQuery, useMeAnswers } from '../api/me.ts';
import { getPhase, signInCount, subscribe } from '../api/session.ts';
import { focusInsteadOfHeading, focusPageHeading, signedOutHeadingId } from '../components/focusPageHeading.ts';
import { msg } from '../copy/messages.ts';
import { pageName, type PageName } from '../copy/pageNames.ts';
import { SignIn } from '../features/auth/SignIn.tsx';
import { fullPageLoad } from './fullPageLoad.ts';
import { disarmUnloadGuard } from './leaveGuard.ts';
import { useRouteChange } from './routeChange.ts';
import type { RouteHandle } from './routes.tsx';
import { SessionTimeoutWarning } from './SessionTimeoutWarning.tsx';
import { ShellFrame } from './ShellFrame.tsx';
import { StartUp } from './StartUp.tsx';
import { usePageTitle } from './usePageTitle.ts';
import { useRefreshMe } from './useRefreshMe.ts';
import { WholePageMessageContext } from './wholePageMessage.ts';

/** Where the signed-out-in-place view started: the screen's page name, and how many answers `['me']` had taken. */
type SignedOutInPlace = { page: PageName | undefined; meAnswers: number };

/**
 * The shell around every screen, in its frame (app-shell.md components 1 to 3a), whose `<main>` holds the route's
 * screen once `GET /api/auth/me` has answered `200`. Until then, and after a failure, it is the signed-out frame, with
 * the wordmark as plain text: from a `401` its `<main>` holds sign-in's view A, at the address that was opened;
 * otherwise it holds the start-up's loading line, or its message for a failure, with Try again (component 4). A
 * sign-in puts its `Me` in the same query, so the route's screen then shows in sign-in's place, at the same address,
 * with no new history entry. A route whose handle says `signedOutFrame`, setup's, shows its page in the signed-out
 * frame at once, whatever `/me` answers and whoever is signed in (sign-in.md component 1). After the frame comes the
 * warning before the idle sign-out, which opens over a signed-in screen only (component 6). The shell alone moves focus
 * and scroll as the screen changes (*The route-change rule (one owner)*), and focus after a sign-in or Try again.
 *
 * When the session ends over a signed-in screen, the frame becomes the signed-out frame again, with sign-in's view C in
 * `<main>` and the screen after it, hidden but mounted, with its before-main bar, so that what it holds comes back as
 * it was when the same person signs in again at the same address (component 7; design.md §8.5). A `Me` for anyone else
 * loads the page afresh at Today instead.
 */
export function AppShell() {
  useRefreshMe();
  const queryClient = useQueryClient();
  const { data: me, error } = useQuery(meQuery);
  const meAnswers = useMeAnswers();
  const phase = useSyncExternalStore(subscribe, getPhase);
  // Data first, as a refresh that fails keeps the data it had, so the person stays signed in on screen (app-shell.md
  // *Refresh*).
  const signedIn = me !== undefined;
  // The table's type gives every route's handle as a RouteHandle, or none.
  const matches = useMatches() as UIMatch<unknown, RouteHandle | undefined>[];
  const handle = matches.findLast((match) => match.handle !== undefined)?.handle;
  useRouteChange(handle?.screenKey);
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
  // A signed-in screen: the route's, for the person whom /me has answered for, not a page in the signed-out frame.
  const screen = signedIn && !signedOutFrame;

  // The person whose screens the shell shows, from the first that it showed. Once ['me'] holds anyone else, from a
  // refresh or a sign-in in place, the page loads afresh at Today (below), so that nothing of the first person's, typed
  // or fetched, ever shows or is sent under the other's name (app-shell.md *Sign in again, a different person*; T01's
  // Notes).
  const [person, setPerson] = useState<string>();
  if (screen && person === undefined) {
    setPerson(me.userId);
  }
  const anotherPerson = screen && person !== undefined && me.userId !== person;

  // The signed-out-in-place view, from when the session clock ends over a signed-in screen, at 30:00 idle or on a 401
  // that ends the session, a refresh's included (session-timeout.md *The signed-out state*). It stays as the clock
  // starts again on the passkey options that it asks for, and closes once ['me'] has a new answer for the same person,
  // from a sign-in or from a refresh that finds the session alive, as the clock says too (*Page visible again*). Set
  // during render, as an effect that set it would show the screen once more first.
  const [ended, setEnded] = useState<SignedOutInPlace>();
  if (ended === undefined) {
    if (screen && phase === 'expired') {
      setEnded({ page, meAnswers });
    }
  } else if (meAnswers > ended.meAnswers && phase === 'active' && !anotherPerson) {
    setEnded(undefined);
  }
  const inPlace = ended !== undefined;

  useEffect(() => {
    if (anotherPerson) {
      // What the page holds was the first person's, so the browser asks no one about leaving it.
      disarmUnloadGuard();
      fullPageLoad();
    }
  }, [anotherPerson]);

  // Whether sign-in has shown, so that the screen that a sign-in brings takes focus on its <h1>, as a new screen does
  // (sign-in.md *Success, fresh load*), while a screen shown on a first load moves no focus (app-shell.md component 7).
  const signInShown = useRef(false);
  // Set once the start-up message's Try again has taken focus or been pressed, so that the screen or sign-in that the
  // answer brings takes focus on its <h1>, as the button has gone (app-shell.md *Accessibility checklist*). Not cleared
  // as focus leaves it, as a blur can come as the button itself goes.
  const tryAgainUsed = useRef(false);
  // Neither move changes the location, so the route-change rule does not make them, but they go to the <h1> as its
  // own do: pending for a heading that is data still loading, such as a user's name, which takes focus as it mounts.
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

  // What view C took over as it showed, the screen's title and the sign-ins until then, to give back as it closes.
  const shownOver = useRef<{ title: string; signIns: number }>(undefined);
  // As view C shows, it takes the title and focus, as the element focused is now hidden: in the shell's effect, after
  // the warning's, which closes as the clock ends, as a page behind an open modal cannot take focus, and in place of
  // the page's <h1>, so that a heading of the hidden screen that mounts meanwhile takes none that it had pending. As it closes over
  // the same person's screen, after every effect of the sign-in view's own has ended, it gives the screen back its
  // title, "Error: " and all, unless Back has moved the screen on meanwhile; after a sign-in, though not after a
  // refresh, which shows that the session was never lost, it fetches the screen's queries again, as invalidating every
  // query does, `['me']` having no observer that fetches; and it focuses the screen's <h1>. No change that met the 401
  // is sent again (app-shell.md *Sign in again, same person*).
  useEffect(() => {
    const signInTitle = msg.pageTitle(pageName.signIn);
    if (inPlace) {
      shownOver.current = { title: document.title, signIns: signInCount() };
      document.title = signInTitle;
      const signedOutHeading = document.getElementById(signedOutHeadingId);
      if (signedOutHeading !== null) {
        focusInsteadOfHeading(signedOutHeading);
      }
      return;
    }

    const over = shownOver.current;
    if (over === undefined) {
      return;
    }

    shownOver.current = undefined;
    if (document.title === signInTitle) {
      document.title = over.title;
    }

    if (signInCount() > over.signIns) {
      void queryClient.invalidateQueries();
    }

    focusPageHeading();
  }, [inPlace, queryClient]);

  // The signed-out frame has the bare title, but on a page shown in it, or sign-in's while view A shows (sign-in.md
  // component 1). A whole-page message in place of the screen, such as Page not found, names it while it shows. Under
  // view C, which sets its own, the name is the one that the screen had as it showed.
  usePageTitle(shown ? (ended === undefined ? page : ended.page) : signedOut ? pageName.signIn : undefined);

  // The signed-in user while the shell shows a signed-in screen, which neither a page shown in the signed-out frame nor
  // view C is.
  const frameMe = signedOutFrame || inPlace ? undefined : me;

  // The warning comes after <main>, outside anything that can be made hidden or inert (app-shell.md component 6).
  return (
    <>
      <ShellFrame me={frameMe} page={page} pageBarHidden={inPlace}>
        {inPlace && <SignIn inPlace />}
        {shown && (
          // Always in the same place, so that view C hides the screen rather than mounting it again.
          <div hidden={inPlace}>
            <WholePageMessageContext value={setMessage}>
              <Outlet />
            </WholePageMessageContext>
          </div>
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
      <SessionTimeoutWarning signedIn={frameMe !== undefined} />
    </>
  );
}
