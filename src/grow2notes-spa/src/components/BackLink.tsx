import type { MouseEvent } from 'react';
import { Link, useLocation, useNavigate, useResolvedPath } from 'react-router';
import { openerBefore } from '../routes/routeChange.ts';
import styles from './BackLink.module.css';

type BackLinkProps = {
  /** The destination's address, which holds IDs and dates only. */
  to: string;
  /** The destination's name and nothing more: no "Back to …" and no bare "Back". */
  children: string;
};

/**
 * The one back link of the app (app-shell.md component 3a), for a screen's `PageBar`: a real link to its destination
 * with a decorative chevron, never disabled, as there must always be a way out. A plain click goes back one entry, as
 * browser Back does, rather than adding the destination again, when the shell's opened-from memory says that the entry
 * before is the one that opened this screen, at the destination's pathname, whatever its search, as a list's other
 * view; otherwise, as after a reload or from a pasted address, it pushes the destination.
 */
export function BackLink({ to, children }: BackLinkProps) {
  const navigate = useNavigate();
  const { key } = useLocation();
  const destination = useResolvedPath(to).pathname;

  // Anything else falls through to the Link, which pushes the destination on a plain click and leaves a modified one
  // to the browser, which opens the href in a new tab or window.
  function goBack(event: MouseEvent<HTMLAnchorElement>) {
    if (isPlainClick(event) && openerBefore(key)?.path === destination) {
      event.preventDefault();
      void navigate(-1);
    }
  }

  return (
    <Link to={to} className={styles.backLink} onClick={goBack}>
      <svg className={styles.chevron} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
        <path d="M12.5 4 6.5 10l6 6" fill="none" stroke="currentColor" strokeWidth="2" />
      </svg>
      {children}
    </Link>
  );
}

/** A click of the main button with no modifier key, which is the only one React Router's Link handles itself. */
function isPlainClick(event: MouseEvent) {
  return event.button === 0 && !(event.ctrlKey || event.metaKey || event.shiftKey || event.altKey);
}
