import { useEffect, useEffectEvent, useRef } from 'react';
import { useBlocker } from 'react-router';

type SaveBeforeLeavingOptions = {
  /** Whether leaving now would lose what the form may still save. */
  unsaved: () => boolean;
  /** Saves it now, giving whether the person may then leave. */
  save: () => Promise<boolean>;
};

/**
 * The note form's guard on a navigation within the app to another screen, by a link, the nav, or the browser's Back or
 * Forward (note-form.md *Leaving the form*, autosave-status.md *Behaviour* 7): React Router's `useBlocker`, as T03's
 * `LeaveWarning` has it, with no dialog. With nothing unsaved the navigation goes; otherwise it waits on `save()`, and
 * goes once that gives `true`, else stays. Gives `leaveFreely()`, which lets the form's own navigation go unasked, as
 * after Submit's `201`.
 */
export function useSaveBeforeLeaving({ unsaved, save }: SaveBeforeLeavingOptions): () => void {
  const free = useRef(false);
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      !free.current && currentLocation.pathname !== nextLocation.pathname && unsaved(),
  );

  // The blocker as it is once the save is answered, which a second press meanwhile has blocked again.
  const settle = useEffectEvent((leave: boolean) => {
    if (blocker.state !== 'blocked') {
      return;
    }

    if (leave) {
      blocker.proceed();
    } else {
      blocker.reset();
    }
  });

  const saveFirst = useEffectEvent(() => {
    void save().then((leave) => {
      settle(leave);
    });
  });

  useEffect(() => {
    if (blocker.state === 'blocked') {
      saveFirst();
    }
  }, [blocker.state]);

  return () => {
    free.current = true;
  };
}
