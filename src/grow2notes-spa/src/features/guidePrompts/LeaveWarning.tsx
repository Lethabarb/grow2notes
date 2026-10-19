import { useCallback, useEffect, useState } from 'react';
import { useBlocker, type BlockerFunction } from 'react-router';
import { ConfirmDialog } from '../../components/ConfirmDialog.tsx';
import { strings } from '../../copy/strings.ts';
import { setLeaveGuard } from '../../routes/leaveGuard.ts';
import { useUnloadWarning } from '../../routes/useUnloadWarning.ts';

type LeaveWarningProps = {
  /** Whether the field holds text that the server does not, as its last input left it. */
  dirty: boolean;
  /** Whether it does at this moment, read from the field itself, as a navigation or Sign out asks. */
  isDirty: () => boolean;
};

/**
 * Guide prompts' warnings before text that is not saved is lost (design.md §4.10, guide-prompts.md *Leaving*), which
 * no other screen has: the leave dialog, for a link, or Back or Forward within the app, to another page, and for Sign
 * out, which asks it first (app-shell.md *Press Sign out*); and the browser's own warning, for a reload, a closed tab
 * or a typed address. Nothing asks while the field holds what the server does, text typed and deleted back included.
 */
export function LeaveWarning({ dirty, isDirty }: LeaveWarningProps) {
  const blocker = useBlocker(
    useCallback<BlockerFunction>(
      ({ currentLocation, nextLocation }) => currentLocation.pathname !== nextLocation.pathname && isDirty(),
      [isDirty],
    ),
  );
  // How Sign out's question is answered, while the dialog asks it.
  const [answerSignOut, setAnswerSignOut] = useState<(leave: boolean) => void>();

  useEffect(() => {
    setLeaveGuard(() =>
      isDirty()
        ? new Promise<boolean>((resolve) => {
            setAnswerSignOut(() => resolve);
          })
        : Promise.resolve(true),
    );
    return () => setLeaveGuard(null);
  }, [isDirty]);

  useUnloadWarning(dirty);

  // Any close but Leave without saving's is Stay: Escape, Android's back gesture and the shell's plain close() too, so
  // that the blocker is never left blocked.
  function answer(leave: boolean) {
    if (blocker.state === 'blocked') {
      if (leave) {
        blocker.proceed();
      } else {
        blocker.reset();
      }
    }

    answerSignOut?.(leave);
    setAnswerSignOut(undefined);
  }

  return blocker.state === 'blocked' || answerSignOut !== undefined ? (
    <ConfirmDialog
      title={strings.guidePrompts.leave.title}
      primary={strings.guidePrompts.leave.stay}
      secondary={strings.guidePrompts.leave.leave}
      confirmButton="secondary"
      initialFocus="primary"
      onClose={answer}
    >
      <p>{strings.guidePrompts.leave.body}</p>
    </ConfirmDialog>
  ) : null;
}
