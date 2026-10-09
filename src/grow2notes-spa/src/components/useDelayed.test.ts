import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useDelayed } from './useDelayed.ts';

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('useDelayed', () => {
  it('becomes true once active has been true for the whole time, and false at once when it stops', () => {
    const { result, rerender } = renderHook(({ active }) => useDelayed(active, 1000), {
      initialProps: { active: true },
    });

    act(() => {
      vi.advanceTimersByTime(999);
    });
    expect(result.current).toBe(false);

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current).toBe(true);

    rerender({ active: false });
    expect(result.current).toBe(false);
  });

  it('waits the whole time again each time active becomes true', () => {
    const { result, rerender } = renderHook(({ active }) => useDelayed(active, 1000), {
      initialProps: { active: true },
    });
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    rerender({ active: false });

    rerender({ active: true });
    act(() => {
      vi.advanceTimersByTime(999);
    });
    expect(result.current).toBe(false);

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current).toBe(true);
  });

  it('stays false while active is false', () => {
    const { result } = renderHook(() => useDelayed(false, 1000));

    act(() => {
      vi.advanceTimersByTime(60_000);
    });

    expect(result.current).toBe(false);
  });
});
