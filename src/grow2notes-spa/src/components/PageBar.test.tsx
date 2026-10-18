import { QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../api/queryClient.ts';
import { pageName } from '../copy/pageNames.ts';
import layout from '../routes/layout.module.css';
import { routes, type AppRoute, type RouteHandle } from '../routes/routes.tsx';
import { BackLink } from './BackLink.tsx';
import { PageBar } from './PageBar.tsx';

// Shown in the signed-out frame at once, as setup's pages are, so that a screen renders with the shell's first render.
const handle: RouteHandle = { pageName: pageName.setUpYourAccount, signedOutFrame: true };

// Made-up screens under the shell: one with a bar holding a back link, and one with none.
const madeUp: AppRoute[] = [
  {
    path: 'with-bar',
    element: (
      <>
        <PageBar>
          <BackLink to="/without-bar">Without bar</BackLink>
        </PageBar>
        <h1>With bar</h1>
      </>
    ),
    handle,
  },
  { path: 'without-bar', element: <h1>Without bar</h1>, handle },
  {
    path: 'with-top-bar',
    element: (
      <>
        <PageBar sticky>
          <BackLink to="/without-bar">Without bar</BackLink>
          <p role="status">Saved</p>
        </PageBar>
        <h1>With top bar</h1>
      </>
    ),
    handle,
  },
];

/** Renders the app's shell at `path`, with the made-up screens under it, while `/me` never answers. */
function renderShell(path: string) {
  const table = routes.map((route) =>
    route.index ? route : { ...route, children: [...(route.children ?? []), ...madeUp] },
  );
  const router = createMemoryRouter(table, { initialEntries: [path] });
  render(
    <QueryClientProvider client={createQueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return router;
}

/** The shell's before-main bar slot. */
function slot(): HTMLElement {
  const element = document.getElementById('page-bar');
  if (element === null) {
    throw new Error('The shell rendered no #page-bar.');
  }

  return element;
}

/** Whether `later` comes after `earlier` in the document. */
function follows(later: Node, earlier: Node): boolean {
  return (earlier.compareDocumentPosition(later) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
}

beforeEach(() => {
  // Fake, so that the pending request's timeout never fires.
  vi.useFakeTimers();
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})));
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('PageBar', () => {
  it('shows its content between <header> and <main>, in the page\'s column, from the shell\'s first render', () => {
    renderShell('/with-bar');

    const link = screen.getByRole('link', { name: 'Without bar' });
    const main = screen.getByRole('main');
    expect(slot()).toContainElement(link);
    expect(slot()).toBeVisible();
    expect(main).not.toContainElement(link);
    expect(follows(slot(), screen.getByRole('banner'))).toBe(true);
    expect(follows(main, slot())).toBe(true);
    expect(slot().firstElementChild).toHaveClass(layout.container);
  });

  it('fills the slot when its screen comes, and leaves it empty and not shown when its screen goes', async () => {
    const router = renderShell('/without-bar');
    expect(slot()).toBeEmptyDOMElement();
    expect(slot()).not.toBeVisible();

    await act(() => router.navigate('/with-bar'));
    expect(slot()).toContainElement(screen.getByRole('link', { name: 'Without bar' }));
    expect(slot()).toBeVisible();

    await act(() => router.navigate('/without-bar'));
    expect(slot()).toBeEmptyDOMElement();
    expect(slot()).not.toBeVisible();
  });
});

describe('PageBar, as the note form\'s top bar', () => {
  /** What the stand-in ResizeObserver was given: the callback, and the elements it observes. */
  let observed: { callback: ResizeObserverCallback; targets: Element[] } | undefined;

  beforeEach(() => {
    observed = undefined;
    vi.stubGlobal(
      'ResizeObserver',
      class {
        constructor(callback: ResizeObserverCallback) {
          observed = { callback, targets: [] };
        }

        observe(target: Element) {
          observed?.targets.push(target);
        }

        unobserve() {}

        disconnect() {
          observed = undefined;
        }
      },
    );
  });

  afterEach(() => {
    document.documentElement.style.removeProperty('--topbar-h');
  });

  /** Reports `blockSize` as the observed bar's height, as the browser does once it has laid the bar out. */
  function reportHeight(blockSize: number) {
    const target = observed!.targets[0];
    const entry = { target, borderBoxSize: [{ blockSize, inlineSize: 375 }] } as unknown as ResizeObserverEntry;
    act(() => {
      observed!.callback([entry], {} as ResizeObserver);
    });
  }

  it('sticks to the top of the window, opaque across it, with its row in the page\'s column', () => {
    renderShell('/with-top-bar');

    const link = screen.getByRole('link', { name: 'Without bar' });
    const bar = slot().firstElementChild as HTMLElement;
    expect(bar).toContainElement(link);
    expect(bar).toContainElement(screen.getByRole('status'));
    expect(bar).toHaveStyle({ position: 'sticky' });
    expect(bar.firstElementChild).toHaveClass(layout.container);
    expect(follows(screen.getByRole('main'), bar)).toBe(true);
  });

  it('writes its height to --topbar-h as it changes, and takes it away as its screen goes', async () => {
    const router = renderShell('/with-top-bar');
    expect(observed?.targets).toEqual([slot().firstElementChild]);

    reportHeight(57);
    expect(document.documentElement.style.getPropertyValue('--topbar-h')).toBe('57px');
    reportHeight(93);
    expect(document.documentElement.style.getPropertyValue('--topbar-h')).toBe('93px');

    await act(() => router.navigate('/without-bar'));
    expect(document.documentElement.style.getPropertyValue('--topbar-h')).toBe('');
    expect(observed).toBeUndefined();
  });

  it('is not sticky in the slot of a screen that gives no top bar', () => {
    renderShell('/with-bar');

    expect(slot().firstElementChild).not.toHaveStyle({ position: 'sticky' });
    expect(observed).toBeUndefined();
  });
});
