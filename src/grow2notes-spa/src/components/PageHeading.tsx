import type { ReactNode } from 'react';
import { pageHeadingId } from './focusPageHeading.ts';
import styles from './PageHeading.module.css';

/**
 * The page's one `<h1>` (app-shell.md component 3), which `focusPageHeading()` can focus, as `tabindex="-1"` lets
 * script focus it without putting it in the tab order.
 */
export function PageHeading({ children }: { children: ReactNode }) {
  return (
    <h1 id={pageHeadingId} tabIndex={-1} className={styles.heading}>
      {children}
    </h1>
  );
}
