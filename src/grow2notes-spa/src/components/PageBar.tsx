import { useContext, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import layout from '../routes/layout.module.css';
import styles from './PageBar.module.css';
import { PageBarContext } from './pageBarContext.ts';

/**
 * A screen's before-main bar (app-shell.md component 3a): its children, in the page's column, in the shell's
 * `#page-bar` slot between `<header>` and `<main>`, where GOV.UK puts a back link so that "Skip to main content" passes
 * it. Only a back link, or the note form's top bar, goes in it.
 */
export function PageBar({ children }: { children: ReactNode }) {
  const slot = useContext(PageBarContext);
  // No slot while the shell's first render commits; the shell renders again with it before the browser paints.
  return slot === null
    ? null
    : createPortal(<div className={`${layout.container} ${styles.bar}`}>{children}</div>, slot);
}
