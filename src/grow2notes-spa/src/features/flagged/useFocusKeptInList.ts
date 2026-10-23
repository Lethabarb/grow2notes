import { useLayoutEffect, useRef, type FocusEvent } from 'react';
import { focusRowInPlace } from '../../routes/routeChange.ts';

/**
 * For a list whose rows a refresh may take away (flagged.md *Interactions and focus*): when the row that has focus is
 * removed, focus would fall to `<body>`, so it goes to the row now in its place, else the last row, else the `<h1>`.
 * `keys` are the rows' return keys in order, the same array while the rows are the same. Spread the result on the
 * element around the list.
 *
 * The row focused is recorded from the focus that reaches the wrapper and forgotten on blur. React runs no event while
 * it removes a row, so the removal does not forget it; the layout effect then finds the key gone and focus on the page.
 */
export function useFocusKeptInList(keys: readonly string[]): {
  onFocus: (event: FocusEvent) => void;
  onBlur: () => void;
} {
  const focused = useRef<string>(undefined);
  const previous = useRef(keys);

  useLayoutEffect(() => {
    const before = previous.current;
    previous.current = keys;
    const key = focused.current;
    if (key === undefined || keys.includes(key)) {
      return;
    }

    focused.current = undefined;
    if (document.activeElement === null || document.activeElement === document.body) {
      focusRowInPlace(before.indexOf(key));
    }
  }, [keys]);

  return {
    onFocus: (event) => {
      focused.current = event.target instanceof HTMLElement ? event.target.dataset.returnKey : undefined;
    },
    onBlur: () => {
      focused.current = undefined;
    },
  };
}
