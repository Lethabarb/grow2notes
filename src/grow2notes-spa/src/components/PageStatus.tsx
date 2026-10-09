import { useCallback, useRef, type ReactNode } from 'react';
import { PageStatusContext } from './pageStatusContext.ts';

/**
 * A page's one status region (app-shell.md component 3): a visually hidden `<p role="status">` after the page's
 * content, rendered from the first render and never conditionally, as a screen reader may not announce text written
 * into a live region that was added with it. The page and the shared `Button` write to it with `useAnnounce()`.
 */
export function PageStatus({ children }: { children: ReactNode }) {
  const region = useRef<HTMLParagraphElement>(null);

  // The text goes straight into the DOM, not through state, so an announcement re-renders nothing. Emptying the region
  // first and writing on the next frame makes a screen reader speak a repeated message again.
  const announce = useCallback((text: string) => {
    const element = region.current;
    if (element === null) {
      return;
    }

    element.textContent = '';
    requestAnimationFrame(() => {
      element.textContent = text;
    });
  }, []);

  return (
    <PageStatusContext value={announce}>
      {children}
      <p ref={region} role="status" className="visually-hidden" />
    </PageStatusContext>
  );
}
