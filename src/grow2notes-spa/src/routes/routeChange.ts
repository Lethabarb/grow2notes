import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { NavigationType, useLocation, useNavigationType } from 'react-router';
import { focusInsteadOfHeading, focusPageHeading } from '../components/focusPageHeading.ts';
import { pageName, type PageName } from '../copy/pageNames.ts';
import type { strings } from '../copy/strings.ts';
import type { BackKey, RouteHandle } from './routes.tsx';

/** A history entry, as the rule compares the one on screen with the next, with what its route declares. */
type Entry = {
  key: string;
  pathname: string;
  /** The route's `handle.screenKey`, which routes that are one screen declare alike, such as the Daily report's. */
  screenKey: string | undefined;
  /** The route's `handle.backKey`, by which it is the opener of the screens that it opens. */
  backKey: BackKey | undefined;
  /** The route's `handle.returnFocus`, by which a list's rows are return points. */
  returnFocus: RouteHandle['returnFocus'];
};

/** The row that a list's entry opened: its `data-return-key` and its place among the rows in `<main>`. */
type ReturnRow = { key: string; index: number };

/**
 * Where a screen was opened from, which an in-app push records on the entry that it adds (app-shell.md *Where a screen
 * was opened from*): the entry that opened it, that entry's back key, and its pathname, which holds IDs and dates only.
 * A screen with several openers, as a note and Past notes are, takes its back link's label and `href` from it
 * (`useBackTo`); on a screen with one, it says only whether the back link goes back one entry, which needs no back key.
 */
type OpenedFrom = { entryKey: string; backKey: BackKey | undefined; path: string };

/** What the shell keeps for a history entry. */
type EntryMemory = { scrollY?: number; returnRow?: ReturnRow; openedFrom?: OpenedFrom };

// What the shell keeps for each history entry, by its location key, but for the scroll position of a route that
// declares a screen key, kept by that key, so that every entry of that screen shares one (app-shell.md *Memory per
// history entry*). In memory only, so a reload empties it: never router state, the address or storage, which is why
// React Router's <ScrollRestoration>, which writes to sessionStorage, is not used.
const memory = new Map<string, EntryMemory>();

// The keys of the history's entries in order, as far as the shell has followed them, and the place of the one shown,
// so that the entry before it is known, which React Router does not say. In memory only, as the rest.
let order: string[] = [];
let place = 0;

// The entry on screen, as far as the shell has followed the history: as the next screen first renders, before the
// rule follows the push or replace that brought it, still the entry before it (useBackTo).
let onScreen: Entry | undefined;

/** A pop back to a list, whose return point waits for the list to be ready, with the position kept for its entry. */
type Waiting = { entryKey: string; row: ReturnRow; fallback: 'h1' | 'sameIndex'; scrollY: number };

// The return point that a pop back to a list left waiting for the list, until the list is ready or the location
// changes again.
let waiting: Waiting | undefined;

// The entry whose list has said that it is ready (useReturnFocusReady).
let readyEntry: string | undefined;

/** The memory of the entry kept by `key`, made empty the first time. */
function remember(key: string): EntryMemory {
  let kept = memory.get(key);
  if (kept === undefined) {
    kept = {};
    memory.set(key, kept);
  }

  return kept;
}

/** Where an entry's scroll position is kept. A screen key is told apart from a location key, which has no colon. */
function scrollKey({ key, screenKey }: Entry): string {
  return screenKey === undefined ? key : `screen:${screenKey}`;
}

/** The return points on the page: the row links of the list in `<main>`, in order. */
function returnRows(): HTMLElement[] {
  return Array.from(document.querySelectorAll<HTMLElement>('main [data-return-key]'));
}

/**
 * The shell's route-change rule, the one owner of focus and scroll on a change of screen (app-shell.md *The
 * route-change rule (one owner)*), for the shell to call with the handle of the route that it shows. On a new screen,
 * a push or replace scrolls to the top and a pop to the position kept for its entry, best effort; then focus goes to
 * the screen's `[data-route-focus]` element, which also shows the top; or, on a pop back to a list, to the row opened
 * from it, once the list is ready; or else to its `<h1>`, pending for a heading not in the page yet. A first load moves
 * nothing, and so does a location change that is not a new screen. On the way, it keeps each entry's memory: its scroll
 * position, the row last opened from it, and where its screen was opened from.
 */
export function useRouteChange(handle: RouteHandle | undefined): void {
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
        remember(scrollKey(shown.current)).scrollY = window.scrollY;
      }
    };
    // In the capture phase, before the row's link takes the click, while the list's entry is the one shown.
    const recordReturnRow = ({ target }: MouseEvent) => {
      const rows = returnRows();
      const row = rows.find((element) => target instanceof Node && element.contains(target));
      const key = row?.dataset.returnKey;
      if (row !== undefined && key !== undefined && shown.current !== undefined) {
        remember(shown.current.key).returnRow = { key, index: rows.indexOf(row) };
      }
    };
    window.addEventListener('scroll', keep, { passive: true });
    document.addEventListener('click', recordReturnRow, { capture: true });
    return () => {
      window.removeEventListener('scroll', keep);
      document.removeEventListener('click', recordReturnRow, { capture: true });
    };
  }, []);

  // A layout effect, which runs after the next screen's own, so that its heading is in the page and a list has said
  // whether it is ready, and before the page is painted at the old position.
  useLayoutEffect(() => {
    const from = shown.current;
    const to: Entry = {
      key: location.key,
      pathname: location.pathname,
      screenKey: handle?.screenKey,
      backKey: handle?.backKey,
      returnFocus: handle?.returnFocus,
    };
    // The same entry again, as StrictMode runs an effect twice as it mounts.
    if (from?.key === to.key) {
      return;
    }

    shown.current = to;
    onScreen = to;
    // A return point left waiting was for the entry that has gone.
    waiting = undefined;
    if (from === undefined) {
      order = [to.key];
      place = 0;
      return;
    }

    follow(from, to, action);
    if (isNewScreen(from, to, action, location.state)) {
      arrive(to, action === NavigationType.Pop);
    } else {
      // The same screen's position, kept for this entry too, for a pop back to it before the page next scrolls.
      remember(scrollKey(to)).scrollY = window.scrollY;
    }
  }, [location, action, handle]);
}

/**
 * Follows the history's order, and records where each screen was opened from (app-shell.md *Where a screen was opened
 * from*): a push adds its entry after the one it came from, which it records as its opener; a replace puts its entry
 * in the place of the one it replaces, whose opener it keeps; and a pop goes to its entry's place, or, for an entry not
 * followed, as one from before a reload, starts the order afresh from it, as nothing is known of what is around it.
 */
function follow(from: Entry, to: Entry, action: NavigationType): void {
  switch (action) {
    case NavigationType.Push:
      order = [...order.slice(0, place + 1), to.key];
      place += 1;
      remember(to.key).openedFrom = { entryKey: from.key, backKey: from.backKey, path: from.pathname };
      break;
    case NavigationType.Replace: {
      order[place] = to.key;
      const opener = memory.get(from.key)?.openedFrom;
      if (opener !== undefined) {
        remember(to.key).openedFrom = opener;
      }
      break;
    }
    case NavigationType.Pop:
      place = order.indexOf(to.key);
      if (place === -1) {
        order = [to.key];
        place = 0;
      }
      break;
  }
}

/**
 * Where the screen at the entry `key` was opened from, when the entry that opened it is the one just before it, so
 * that going back one entry returns there: for `BackLink`, which then pops rather than pushes its destination
 * (app-shell.md component 3a). None after a reload, from a pasted address or in a new tab, or once the entry before
 * has been replaced.
 */
export function openerBefore(key: string): OpenedFrom | undefined {
  const opener = memory.get(key)?.openedFrom;
  const index = order.indexOf(key);
  return index > 0 && order[index - 1] === opener?.entryKey ? opener : undefined;
}

/**
 * A back link's label, the destination's name (app-shell.md component 3a): its page name, or, where the nav names it
 * otherwise, the nav item's word, as the note's back link reads "Flagged", not "Flagged notes" (flagged.md
 * component 7).
 */
type BackLabel = PageName | (typeof strings.navItem)[keyof typeof strings.navItem];

/** A back link's destination, with the label that names it (app-shell.md component 3a). */
export type BackTo = { to: string; label: BackLabel };

/**
 * The back link of a screen with several openers (app-shell.md component 3a), such as a note's or Past notes': to the
 * screen that opened it, named by `labels` after its back key, when that screen is the entry before; otherwise, as
 * after a reload, from a pasted address or in a new tab, or from an opener that `labels` does not name, to Today, the
 * default of the screens that use it. As the screen first renders, the shell's rule has not yet followed the push or
 * replace that brought it, so the opener is worked out as `follow` will record it: for a push, the entry still on
 * screen; for a replace, the opener of the entry it replaces.
 */
export function useBackTo(labels: Partial<Record<BackKey, BackLabel>>): BackTo {
  const { key } = useLocation();
  const action = useNavigationType();
  const opener = openerAsRendered(key, action);
  const label = opener?.backKey === undefined ? undefined : labels[opener.backKey];
  return opener === undefined || label === undefined ? { to: '/', label: pageName.today } : { to: opener.path, label };
}

/** `openerBefore(key)`, also before the shell's rule has followed the push or replace that reached the entry. */
function openerAsRendered(key: string, action: NavigationType): OpenedFrom | undefined {
  if (onScreen === undefined || onScreen.key === key) {
    return openerBefore(key);
  }

  switch (action) {
    case NavigationType.Push:
      return { entryKey: onScreen.key, backKey: onScreen.backKey, path: onScreen.pathname };
    case NavigationType.Replace:
      return openerBefore(onScreen.key);
    case NavigationType.Pop:
      return openerBefore(key);
  }
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
 * kept position for a pop, as far as the page is long enough for it; then, on a pop back to a list, the row opened
 * from it, at once if the list is ready and else once it is; or else the `<h1>`, not scrolled to on a pop.
 */
function arrive(to: Entry, pop: boolean): void {
  const routeFocus = document.querySelector<HTMLElement>('main [data-route-focus]');
  if (routeFocus !== null) {
    window.scrollTo(0, 0);
    focusInsteadOfHeading(routeFocus);
    return;
  }

  const scrollY = pop ? (memory.get(scrollKey(to))?.scrollY ?? 0) : 0;
  window.scrollTo(0, scrollY);
  const row = pop ? memory.get(to.key)?.returnRow : undefined;
  if (to.returnFocus === undefined || row === undefined) {
    focusPageHeading(pop ? { preventScroll: true } : undefined);
    return;
  }

  const due: Waiting = { entryKey: to.key, row, fallback: to.returnFocus.fallback ?? 'h1', scrollY };
  if (readyEntry === to.key) {
    returnFocus(due);
  } else {
    waiting = due;
  }
}

/**
 * Focuses a list's return point (the rule's item 3b), without scrolling: the row opened from it, by its key; or, if
 * that row has gone, the list's fallback, the row now at its place, else the last row, for `'sameIndex'`, else the
 * `<h1>`, as when the list did not load.
 */
function returnFocus({ row, fallback }: Waiting): void {
  const rows = returnRows();
  const opened = rows.find((element) => element.dataset.returnKey === row.key);
  const target = opened ?? (fallback === 'sameIndex' ? (rows.at(row.index) ?? rows.at(-1)) : undefined);
  if (target === undefined) {
    focusPageHeading({ preventScroll: true });
  } else {
    focusInsteadOfHeading(target, { preventScroll: true });
  }
}

/**
 * For a list whose route declares `handle.returnFocus`: tells the shell that the list is ready, its rows in the page or
 * the failure to load them in their place, so that a pop back to it that waited for them then focuses its return point
 * (app-shell.md *The route-change rule (one owner)*, item 3b), at the position kept for its entry, which the page was
 * too short for as it arrived.
 */
export function useReturnFocusReady(isReady: boolean): void {
  const { key } = useLocation();

  // A layout effect, which runs before the shell's on the same commit, so that a pop that finds the list ready at
  // once focuses the row before the page is painted.
  useLayoutEffect(() => {
    if (!isReady) {
      return;
    }

    readyEntry = key;
    if (waiting?.entryKey === key) {
      const due = waiting;
      waiting = undefined;
      window.scrollTo(0, due.scrollY);
      returnFocus(due);
    }

    return () => {
      if (readyEntry === key) {
        readyEntry = undefined;
      }
    };
  }, [isReady, key]);
}

/**
 * For a whole-page message, Page not found or a crash page: focuses its `<h1>` when it takes the place of what had
 * focus, which has gone with what it replaced, so that focus does not fall to `<body>` with nothing said (app-shell.md
 * *The route-change rule (one owner)*, item 5). That is a change with no location change, such as a screen that
 * crashes after a refresh or a press, or a region refused after the rule has focused a fixed `<h1>`; on a new screen
 * the rule focuses the heading anyway. Where what had focus stays, as a nav link or Account does, focus stays with it,
 * and on a first load nothing had focus. It also focuses its `<h1>` in place of a list whose return point was waiting
 * for it, as a pop back to the list owed it the focus, which the list, refused, never took.
 */
export function useFocusOnReplace(): void {
  // Read as the message first renders, before the commit that takes away what it replaces.
  const [hadFocus] = useState(() => document.activeElement);
  const { key } = useLocation();

  useLayoutEffect(() => {
    const owed = waiting?.entryKey === key;
    if (owed) {
      waiting = undefined;
    }

    if (owed || hadFocus?.isConnected === false) {
      focusPageHeading();
    }
  }, [hadFocus, key]);
}
