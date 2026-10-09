import { useEffect, useState } from 'react';

/**
 * Whether `active` has been true for `ms` without a break: false while it is not, and for the first `ms` each time it
 * becomes true.
 */
export function useDelayed(active: boolean, ms: number): boolean {
  // Set only by the timer, and reset here during render, as an effect that reset it would render twice; the next time
  // `active` becomes true then waits its whole `ms` again.
  const [late, setLate] = useState(false);
  if (late && !active) {
    setLate(false);
  }

  useEffect(() => {
    if (!active) {
      return;
    }

    const timer = window.setTimeout(() => {
      setLate(true);
    }, ms);
    return () => window.clearTimeout(timer);
  }, [active, ms]);

  return active && late;
}
