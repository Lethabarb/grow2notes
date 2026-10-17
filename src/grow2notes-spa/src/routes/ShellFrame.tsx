import { useState, type MouseEvent, type ReactNode } from 'react';
import type { Me } from '../api/me.ts';
import { PageBarContext } from '../components/pageBarContext.ts';
import type { PageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';
import { Header } from './Header.tsx';
import layout from './layout.module.css';
import styles from './ShellFrame.module.css';

const mainId = 'main-content';

type ShellFrameProps = {
  /** The signed-in user, for the header's nav and Account, or `undefined` for the wordmark as plain text alone. */
  me: Me | undefined;
  /** The name of the page that `<main>` shows, for the header. */
  page: PageName | undefined;
  /**
   * Whether the before-main bar slot is `hidden`, with the screen whose bar it holds, under the signed-out-in-place
   * view (app-shell.md component 3a), so that its back link can be neither seen nor reached by Tab.
   */
  pageBarHidden?: boolean;
  /** What `<main>` holds. */
  children: ReactNode;
};

/**
 * The frame of every page (app-shell.md components 1 to 3a): the skip link, the header, the before-main bar slot and
 * the one `<main>`. The shell's, and the shell's crash page's, which takes its place.
 */
export function ShellFrame({ me, page, pageBarHidden = false, children }: ShellFrameProps) {
  // The before-main bar slot once it is in the DOM, for a screen's PageBar. State, not a ref, so that a screen shown
  // with the frame's first render, before the slot exists, renders again with it.
  const [pageBar, setPageBar] = useState<HTMLDivElement | null>(null);

  return (
    <>
      <a href={`#${mainId}`} className={styles.skipLink} onClick={skipToMain}>
        {strings.skipToMainContent}
      </a>
      <Header me={me} page={page} />
      <div id="page-bar" ref={setPageBar} className={styles.pageBar} hidden={pageBarHidden} />
      <PageBarContext value={pageBar}>
        <main id={mainId} tabIndex={-1} className={`${layout.container} ${styles.main}`}>
          {children}
        </main>
      </PageBarContext>
    </>
  );
}

// Focuses <main> without following the link, so that #main-content never enters the address or the history, and the
// router sees no navigation (app-shell.md component 1).
function skipToMain(event: MouseEvent<HTMLAnchorElement>) {
  event.preventDefault();
  document.getElementById(mainId)?.focus();
}
