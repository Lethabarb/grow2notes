import type { ReactNode } from 'react';
import { pageHeadingId } from './focusPageHeading.ts';
import styles from './PageHeading.module.css';

type PageHeadingProps = {
  children: ReactNode;
  /**
   * `signedOutHeadingId` for the signed-out-in-place view's, which shows while the page's own stays in the DOM,
   * hidden; left out for the page's own, which `focusPageHeading()` focuses.
   */
  id?: string;
};

/**
 * The page's one `<h1>` (app-shell.md component 3), which script can focus, as `tabindex="-1"` lets it without
 * putting it in the tab order.
 */
export function PageHeading({ children, id = pageHeadingId }: PageHeadingProps) {
  return (
    <h1 id={id} tabIndex={-1} className={styles.heading}>
      {children}
    </h1>
  );
}
