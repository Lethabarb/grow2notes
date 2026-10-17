import { Link, useLocation } from 'react-router';
import { pageHeadingId } from './focusPageHeading.ts';
import styles from './ViewLinks.module.css';

/** One of a screen's two views: its `?view=` keyword, or `null` for the default view, and its name. */
export type ViewOption<V extends string> = { value: V | null; label: string };

type ViewLinksProps<V extends string> = {
  /** The default view first, as Active on Participants and To review on Flagged notes. */
  options: readonly [ViewOption<V>, ViewOption<V>];
  /** The view shown, as `useView` reads it. */
  current: V | null;
};

/**
 * Two views of one list as links (tabs-segmented.md), such as Active and Archived on Participants: a `<nav>` named by
 * the page's `<h1>`, holding a list of two links that change only the address's `?view=`, the current one marked
 * `aria-current="page"`. Not `NavLink`, which matches on the pathname alone, and both share it. A switch is an
 * ordinary push, so Back returns to the view before; the shell's route-change rule takes a change of the search alone
 * as the same screen, so focus stays on the link and the page where it is (app-shell.md), and with no
 * `<ScrollRestoration>` in the app, there is no scroll reset for `preventScrollReset` to stop.
 */
export function ViewLinks<V extends string>({ options, current }: ViewLinksProps<V>) {
  const { pathname } = useLocation();

  return (
    <nav aria-labelledby={pageHeadingId} className={styles.nav}>
      <ul className={styles.list}>
        {options.map(({ value, label }) => (
          // Keyed by the view, so that the same two links stay in the page, focus and all, as the view changes.
          <li key={value ?? ''} className={styles.item}>
            <Link
              to={{ pathname, search: value === null ? '' : `?view=${value}` }}
              aria-current={value === current ? 'page' : undefined}
              className={styles.link}
            >
              {label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
