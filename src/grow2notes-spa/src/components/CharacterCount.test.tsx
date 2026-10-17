import { act, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CharacterCount } from './CharacterCount.tsx';

type Props = Parameters<typeof CharacterCount>[0];

/** Renders the count, and gives a way to render it again with other props, as its form does at each input. */
function renderCount(props: Props): (next: Props) => void {
  const { rerender } = render(<CharacterCount {...props} />);
  return (next) => {
    rerender(<CharacterCount {...next} />);
  };
}

/** The count that shows, or `null`. */
function shown(): HTMLElement | null {
  return document.querySelector('p[aria-hidden="true"]');
}

/** The polite twin, which screen readers hear. */
function twin(): HTMLElement {
  return document.querySelector<HTMLElement>('[aria-live="polite"]')!;
}

async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('CharacterCount', () => {
  it.each([
    [0, 'You can enter up to 200 characters'],
    [1, 'You have 199 characters remaining'],
    [199, 'You have 1 character remaining'],
    [200, 'You have 0 characters remaining'],
  ])('at %i of 200, shows "%s", hidden from screen readers and not over', (length, words) => {
    renderCount({ length, limit: 200, inputs: 0 });

    expect(shown()?.textContent).toBe(words);
    expect(shown()).toHaveAttribute('aria-hidden', 'true');
    expect(shown()).not.toHaveAttribute('data-over-limit');
  });

  it.each([
    [201, 'You have 1 character too many'],
    [250, 'You have 50 characters too many'],
  ])('at %i of 200, shows "%s", marked over', (length, words) => {
    renderCount({ length, limit: 200, inputs: 0 });

    expect(shown()?.textContent).toBe(words);
    expect(shown()).toHaveAttribute('data-over-limit');
  });

  it('with a threshold, shows nothing below it, the empty field included, and the count from it', () => {
    const rerender = renderCount({ length: 0, limit: 1_000, from: 900, inputs: 0 });
    expect(shown()).toBeNull();

    rerender({ length: 899, limit: 1_000, from: 900, inputs: 1 });
    expect(shown()).toBeNull();
    rerender({ length: 900, limit: 1_000, from: 900, inputs: 2 });
    expect(shown()?.textContent).toBe('You have 100 characters remaining');
  });

  it('renders its twin empty from the first render, and says the count once 1 s has passed with no input', async () => {
    const rerender = renderCount({ length: 0, limit: 200, inputs: 0 });
    expect(twin()).toBeEmptyDOMElement();
    expect(twin()).toHaveClass('visually-hidden');
    expect(twin()).not.toHaveAttribute('aria-hidden');

    rerender({ length: 5, limit: 200, inputs: 1 });
    await wait(999);
    expect(twin()).toBeEmptyDOMElement();
    // An input of the same length starts the second again.
    rerender({ length: 5, limit: 200, inputs: 2 });
    await wait(999);
    expect(twin()).toBeEmptyDOMElement();
    await wait(1);
    expect(twin().textContent).toBe('You have 195 characters remaining');
  });

  it('says nothing for a change that no input made, and calls off what an input before it would say', async () => {
    const rerender = renderCount({ length: 0, limit: 200, inputs: 0 });
    rerender({ length: 19, limit: 200, inputs: 1 });
    await wait(1_000);
    expect(twin().textContent).toBe('You have 181 characters remaining');

    rerender({ length: 36, limit: 200, inputs: 2 });
    await wait(500);
    rerender({ length: 0, limit: 200, inputs: 2 });
    await wait(60_000);

    expect(twin().textContent).toBe('You have 181 characters remaining');
    expect(shown()?.textContent).toBe('You can enter up to 200 characters');
  });

  it('says nothing below its threshold', async () => {
    const rerender = renderCount({ length: 950, limit: 1_000, from: 900, inputs: 0 });
    rerender({ length: 950, limit: 1_000, from: 900, inputs: 1 });
    await wait(1_000);
    expect(twin().textContent).toBe('You have 50 characters remaining');

    rerender({ length: 10, limit: 1_000, from: 900, inputs: 2 });
    await wait(1_000);

    expect(twin()).toBeEmptyDOMElement();
  });
});
