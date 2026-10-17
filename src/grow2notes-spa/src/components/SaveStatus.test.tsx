import { render, screen } from '@testing-library/react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { SaveStatus } from './SaveStatus.tsx';

// 06:12 UTC on 1 October 2026 is 4:12 pm in Melbourne, as an answer's Date gives it through apiWithHeaders().
const savedAtUtc = '2026-10-01T06:12:00.000Z';

function status() {
  return screen.getByRole('status');
}

describe('SaveStatus', () => {
  it('is rendered, empty, from the first render, with nothing yet saved', () => {
    // No effect runs here, so a region that an effect added would be missing.
    expect(renderToStaticMarkup(<SaveStatus state="none" />)).toMatch(/^<p role="status" class="[^"]+"><\/p>$/);
  });

  it('is not hidden while empty, has no margin, and is in small secondary text with figures all one width', () => {
    render(<SaveStatus state="none" />);

    expect(status()).toBeEmptyDOMElement();
    expect(status()).toBeVisible();
    expect(status()).toHaveStyle({
      margin: '0px',
      fontSize: 'var(--font-size-small)',
      color: 'var(--colour-text-secondary)',
    });
    expect(getComputedStyle(status()).getPropertyValue('font-variant-numeric')).toBe('tabular-nums');
  });

  it('says "Saving…", for screen readers alone, while a save waits', () => {
    render(<SaveStatus state="saving" />);

    expect(status().textContent).toBe('Saving…');
    expect(status().firstElementChild).toHaveClass('visually-hidden');
  });

  it('says "Saved" and the time of the answer in Melbourne, in a <time> with the UTC time', () => {
    render(<SaveStatus state={{ savedAtUtc }} />);

    expect(status().textContent).toBe('Saved 4:12 pm');
    const time = status().querySelector('time');
    expect(time).toHaveAttribute('dateTime', savedAtUtc);
    expect(time?.textContent).toBe('4:12 pm');
    expect(status().querySelector('.visually-hidden')).toBeNull();
  });

  it('says "Saved" alone when the answer had no Date, as the device\'s clock is never used', () => {
    render(<SaveStatus state={{ savedAtUtc: undefined }} />);

    expect(status().textContent).toBe('Saved');
    expect(status().querySelector('time')).toBeNull();
  });

  it('keeps one region through every state, so that each change is announced', () => {
    const { rerender } = render(<SaveStatus state="none" />);
    const region = status();

    for (const state of ['saving', { savedAtUtc }, 'none', { savedAtUtc: undefined }] as const) {
      rerender(<SaveStatus state={state} />);
      expect(status()).toBe(region);
    }

    expect(region.textContent).toBe('Saved');
  });
});
