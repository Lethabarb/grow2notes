import { useContext, useLayoutEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import layout from '../routes/layout.module.css';
import styles from './PageBar.module.css';
import { PageBarContext } from './pageBarContext.ts';

type PageBarProps = {
  children: ReactNode;
  /**
   * The note form's top bar (note-form.md component 1), the one sticky element of the app (foundations.md): it stays
   * at the top of the window as the form scrolls under it, but on a short window.
   */
  sticky?: boolean;
};

/**
 * A screen's before-main bar (app-shell.md component 3a): its children, in the page's column, in the shell's
 * `#page-bar` slot between `<header>` and `<main>`, where GOV.UK puts a back link so that "Skip to main content" passes
 * it. Only a back link, or the note form's top bar, goes in it.
 */
export function PageBar({ children, sticky = false }: PageBarProps) {
  const slot = useContext(PageBarContext);
  // No slot while the shell's first render commits; the shell renders again with it before the browser paints.
  if (slot === null) {
    return null;
  }

  return createPortal(
    sticky ? <TopBar>{children}</TopBar> : <div className={`${layout.container} ${styles.bar}`}>{children}</div>,
    slot,
  );
}

/**
 * The note form's top bar (note-identity-header.md *Top bar*): opaque across the window, its row at least 3.5rem tall
 * and its contents in the form's 40rem column. Its height, which its contents change as they wrap, goes to
 * `--topbar-h`, from which the page keeps a focused field clear of it (SC 2.4.11, technique C43).
 */
function TopBar({ children }: { children: ReactNode }) {
  const bar = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const root = document.documentElement;
    const observer = new ResizeObserver(([entry]) => {
      root.style.setProperty('--topbar-h', `${entry.borderBoxSize[0].blockSize}px`);
    });
    // In the page by now, as layout effects run once the portal has put it there.
    observer.observe(bar.current!);
    return () => {
      observer.disconnect();
      root.style.removeProperty('--topbar-h');
    };
  }, []);

  return (
    <div ref={bar} className={styles.topBar}>
      <div className={layout.container}>
        <div className={styles.topBarRow}>{children}</div>
      </div>
    </div>
  );
}
