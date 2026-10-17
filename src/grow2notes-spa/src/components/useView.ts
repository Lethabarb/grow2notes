import { useEffect } from 'react';
import { useSearchParams } from 'react-router';

/**
 * The view that the address's `?view=` names, for `ViewLinks`: one of `views`, or `null` for the screen's default
 * view, which any other value also shows, as the address is put right with a replace (tabs-segmented.md *Behaviour*).
 * The address is the view's only memory, so Back and a reload keep it, and nothing is stored on the device (D22).
 */
export function useView<V extends string>(views: readonly V[]): V | null {
  const [params, setParams] = useSearchParams();
  const named = params.get('view');
  const view = views.find((candidate) => candidate === named) ?? null;
  const wrong = named !== null && view === null;

  useEffect(() => {
    if (wrong) {
      setParams(
        (current) => {
          current.delete('view');
          return current;
        },
        { replace: true },
      );
    }
  }, [wrong, setParams]);

  return view;
}
