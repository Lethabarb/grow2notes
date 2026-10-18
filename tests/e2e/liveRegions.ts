import type { Page } from '@playwright/test';

/**
 * Reports, through what it gives, each live region that a screen at an address starting `path` puts into the document
 * with words already in it, as a screen reader may not announce them (app-shell.md component 3; each screen's
 * *Accessibility checklist*): an element whose role is `status` or `alert`, or one in `<main>` that has `aria-live`,
 * as a character count's polite twin has. The app puts its elements in with `appendChild` and `insertBefore`, which
 * the script wraps before the app's own runs, so that each subtree is looked at as it joins the document.
 */
export async function watchLiveRegions(page: Page, path: string): Promise<string[]> {
  const added: string[] = [];
  await page.exposeFunction('liveRegionAddedWithWords', (region: string) => {
    added.push(region);
  });
  await page.addInitScript((screens) => {
    const roles = '[role="status"], [role="alert"]';
    function check(parent: Node, node: Node) {
      if (!parent.isConnected || !(node instanceof Element) || !location.pathname.startsWith(screens)) {
        return;
      }

      // The subtree is not in the document yet, so whether a region in it is in <main> is read from where it goes.
      const inMain = parent instanceof Element && parent.closest('main') !== null;
      const regions = `${roles}, ${inMain ? '' : 'main '}[aria-live]`;
      for (const region of [node, ...node.querySelectorAll(regions)].filter((element) => element.matches(regions))) {
        if ((region.textContent ?? '').trim() !== '') {
          void (window as unknown as { liveRegionAddedWithWords: (region: string) => Promise<void> })
            .liveRegionAddedWithWords(region.outerHTML);
        }
      }
    }

    const { appendChild, insertBefore } = Node.prototype;
    Node.prototype.appendChild = function <T extends Node>(this: Node, node: T): T {
      check(this, node);
      return appendChild.call(this, node) as T;
    };
    Node.prototype.insertBefore = function <T extends Node>(this: Node, node: T, child: Node | null): T {
      check(this, node);
      return insertBefore.call(this, node, child) as T;
    };
  }, path);
  return added;
}

/** Each live region in the page whose own display is `none`, which would stop it announcing anything. */
export const hiddenLiveRegions = (page: Page) =>
  page.evaluate(() =>
    [...document.querySelectorAll('[role="status"], [role="alert"], main [aria-live]')]
      .filter((region) => getComputedStyle(region).display === 'none')
      .map((region) => region.outerHTML),
  );
