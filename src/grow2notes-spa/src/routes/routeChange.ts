import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { NavigationType, useLocation, useNavigationType } from 'react-router';
import { focusInsteadOfHeading, focusPageHeading } from '../components/focusPageHeading.ts';

/** A history entry, as the rule compares the one on screen with the next. */
type Entry = {
  key: string;
  pathname: string;
  /** The route's `handle.screenKey`, which routes that are one screen declare alike, such as the Daily report's. */
  screenKey: string | undefined;
};

// The scroll position of each history entry, by its location key, or by its route's screen key where it declares one,
// so that every entry of that screen shares one (app-shell.md *Memory per history entry*). In memory only, so a reload
// empties it: never router state, the address or storage, which is why React Router's <ScrollRestoration>, which
// writes to sessionStorage, is not used.
const scrollPositions = new Map<string, number>();

/** Where an entry's scroll position is kept. A screen key is told apart from a location key, which has no colon. */
function scrollKey({ key, screenKey }: Entry): string {
  return screenKey === undefined ? key : `screen:${screenKey}`;
}

/**
 * The shell's route-change rule, the one owner of focus and scroll on a change of screen (app-shell.md *The
 * route-change rule (one owner)*), for the shell to call with the screen key of the route that it shows. On a new
 * screen, a push or replace scrolls to the top and a pop to the position kept for its entry, best effort; then focus
 * goes to the screen's `[data-route-focus]` element, which also shows the top, or else to its `<h1>`, pending for a
 * heading not in the page yet. A first load moves nothing, and so does a location change that is not a new screen.
 */
export function useRouteChange(screenKey: string | undefined): void {
  const location = useLocation();
  const action = useNavigationType();
  // The entry on screen, from the first location on.
  const shown = useRef<Entry>(undefined);

  useEffect(() => {
    // The browser's own restoration would scroll a popped entry before its screen has rendered.
    history.scrollRestoration = 'manual';
    // Kept as the page scrolls, as by the time the next location's effects run, its screen has taken the page's place.
    const keep = () => {
      if (shown.current !== undefined) {
        scrollPositions.set(scrollKey(shown.current), window.scrollY);
      }
    };
    window.addEventListener('scroll', keep, { passive: true });
    return () => {
      window.removeEventListener('scroll', keep);
    };
  }, []);

  // A layout effect, which runs after the next screen's own, so that its heading is in the page, and before the page
  // is painted at the old position.
  useLayoutEffect(() => {
    const from = shown.current;
    const to: Entry = { key: location.key, pathname: location.pathname, screenKey };
    // The same entry again, as StrictMode runs an effect twice as it mounts.
    if (from?.key === to.key) {
      return;
    }

    shown.current = to;
    if (from === undefined) {
      return;
    }

    if (isNewScreen(from, to, action, location.state)) {
      arrive(to, action === NavigationType.Pop);
    } else {
      // The same screen's position, kept for this entry too, for a pop back to it before the page next scrolls.
      scrollPositions.set(scrollKey(to), window.scrollY);
    }
  }, [location, action, screenKey]);
}

/**
 * Whether going from one entry to the next changes screen, alike for a push, a replace and a pop (app-shell.md *What
 * counts as a new screen*). Not when the pathname stays, whatever else changes: the search, as on a `?view=` switch,
 * the fragment, as setup takes its link out of the address, or nothing, as from a nav link to the screen already open.
 * Not on the push or replace whose state says `inScreen`, as after Add participant: it tells how that entry was
 * reached, so a pop back to it from another screen is a new screen. And not between routes of one screen key.
 */
function isNewScreen(from: Entry, to: Entry, action: NavigationType, state: unknown): boolean {
  if (from.pathname === to.pathname || (action !== NavigationType.Pop && isInScreen(state))) {
    return false;
  }

  return to.screenKey === undefined || to.screenKey !== from.screenKey;
}

/** Whether a location's state is `{ inScreen: true }`, the only state that the app gives a location. */
function isInScreen(state: unknown): boolean {
  return typeof state === 'object' && state !== null && 'inScreen' in state && state.inScreen === true;
}

/**
 * Arrival at a new screen (the rule's items 2 and 3): its `[data-route-focus]` element wins on every arrival, with
 * the top of the page, so that Submit's banner on Today shows; otherwise the top for a push or replace, or the entry's
 * kept position for a pop, as far as the page is long enough for it, with the `<h1>`, not scrolled to on a pop.
 */
function arrive(to: Entry, pop: boolean): void {
  const routeFocus = document.querySelector<HTMLElement>('main [data-route-focus]');
  if (routeFocus !== null) {
    window.scrollTo(0, 0);
    focusInsteadOfHeading(routeFocus);
    return;
  }

  window.scrollTo(0, pop ? (scrollPositions.get(scrollKey(to)) ?? 0) : 0);
  focusPageHeading(pop ? { preventScroll: true } : undefined);
}

/**
 * For a whole-page message, Page not found or a crash page: focuses its `<h1>` when it takes the place of what had
 * focus, which has gone with what it replaced, so that focus does not fall to `<body>` with nothing said (app-shell.md
 * *The route-change rule (one owner)*, item 5). That is a change with no location change, such as a screen that
 * crashes after a refresh or a press, or a region refused after the rule has focused a fixed `<h1>`; on a new screen
 * the rule focuses the heading anyway. Where what had focus stays, as a nav link or Account does, focus stays with it,
 * and on a first load nothing had focus.
 */
export function useFocusOnReplace(): void {
  // Read as the message first renders, before the commit that takes away what it replaces.
  const [hadFocus] = useState(() => document.activeElement);

  useLayoutEffect(() => {
    if (hadFocus?.isConnected === false) {
      focusPageHeading();
    }
  }, [hadFocus]);
}
