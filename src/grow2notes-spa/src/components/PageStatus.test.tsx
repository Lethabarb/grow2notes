import { renderHook, screen } from '@testing-library/react';
import { renderToStaticMarkup } from 'react-dom/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PageStatus } from './PageStatus.tsx';
import { useAnnounce } from './pageStatusContext.ts';

/** Renders a page inside a `PageStatus`, and returns the `announce` it gives the page and its status region. */
function renderPage() {
  const { result } = renderHook(() => useAnnounce(), { wrapper: PageStatus });
  const announce = result.current;
  if (announce === undefined) {
    throw new Error('PageStatus gave the page nothing to call.');
  }

  return { announce, region: screen.getByRole('status') };
}

describe('PageStatus', () => {
  it('renders the page and its empty status region from the first render', () => {
    // No effect runs here, so a region that an effect added would be missing.
    const markup = renderToStaticMarkup(
      <PageStatus>
        <h1>Page</h1>
      </PageStatus>,
    );

    expect(markup).toBe('<h1>Page</h1><p role="status" class="visually-hidden"></p>');
  });

  describe('announce', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('empties the region, then writes the text on the next frame', () => {
      const { announce, region } = renderPage();
      announce('Saving changes…');
      vi.advanceTimersToNextFrame();

      announce('Changes saved');

      expect(region).toBeEmptyDOMElement();
      vi.advanceTimersToNextFrame();
      expect(region.textContent).toBe('Changes saved');
    });

    it('empties the region and writes the same text again when it is announced twice', () => {
      const { announce, region } = renderPage();
      announce('Already at the top');
      vi.advanceTimersToNextFrame();
      expect(region.textContent).toBe('Already at the top');

      announce('Already at the top');

      expect(region).toBeEmptyDOMElement();
      vi.advanceTimersToNextFrame();
      expect(region.textContent).toBe('Already at the top');
    });
  });
});

describe('useAnnounce', () => {
  it('gives nothing to call outside every PageStatus', () => {
    const { result } = renderHook(() => useAnnounce());

    expect(result.current).toBeUndefined();
  });
});
