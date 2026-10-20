import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { api, ApiError, classify, type StartedBy } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { ConfirmDialog } from '../../components/ConfirmDialog.tsx';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { dateLong, parseDateOnly, stamp } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { removeToday } from '../today/todayQuery.ts';
import { noteBase } from './noteQueries.ts';
import { invalidatePastNotes } from './pastNotesQuery.ts';

/** How the dialog's request goes, as the dialog shows it. */
type Request = {
  busy: boolean;
  /** Why it failed, in the alert line. */
  error?: string;
  /** False once the draft was found submitted or discarded meanwhile: the confirming button goes. */
  canConfirm: boolean;
};

type DiscardDialogProps = {
  participantId: string;
  noteDate: string;
  /** The participant's full name, as stored. */
  name: string;
  /** Who started someone else's draft, and when, which a manager's dialog says; none for the viewer's own draft. */
  startedBy?: StartedBy;
  /**
   * Called as the confirming button is pressed, and awaited before the request: `false` sends nothing, as when the
   * save that the note form waits for meets a dead end, which closes the dialog.
   */
  prepare?: () => Promise<boolean>;
  /** Called just before the move to Today, as the note form lets its leave guard go. */
  onLeave?: () => void;
  /**
   * Called once the dialog has closed with nothing discarded, by Go back, Escape, Android's back gesture or a plain
   * `close()`; or, if a browser closed it while its request was out, once that request has failed, with why.
   */
  onClose: (failure?: string) => void;
};

/**
 * The Discard dialog (note-form.md component 15, confirm-dialog.md's dialog 2), one for the note form and for a
 * manager's read-only draft, on T03's `ConfirmDialog`, opened on its title: "Discard the draft note for Jane Citizen?";
 * then its body, each line only where it applies, in confirm-dialog.md's order: the note date of a draft dated before
 * today, who started someone else's draft and when, the time as `stamp()` gives it with the note date as its reference
 * day (design.md A64), and always "Nobody will be able to open this draft again."; then "Discard draft for Jane
 * Citizen", the warning style, as nothing brings a discarded draft back (A9), and Go back.
 *
 * Confirming sends `POST {base}/discard` once `prepare()` allows it, and shows "Discarding…" once it has taken 1 s.
 * Its `204` drops Today's lists and the caller's unfinished drafts, so that Today never shows the old status for a
 * moment, marks the participant's Past notes stale, so that a pop back to them lists the draft no more, and puts Today
 * in this page's place, whose `<h1>` the shell then focuses, with no message (note-form.md *Discard draft*, open
 * question 6). A failure keeps the dialog open, saying why, but nothing for a `401`, as the shell closes the dialog
 * before signing in again, after which nothing is sent again (note-form.md *Session*). A `409 note.not_a_draft`, the
 * draft submitted or discarded meanwhile, takes the confirming button away, focus going to Go back, which then goes to
 * Today in the same way. A browser may close the dialog while its request is out (confirm-dialog.md *Behaviour* 7):
 * the request then ends on the page, at Today, or with `onClose()` and why it failed.
 */
export function DiscardDialog({
  participantId,
  noteDate,
  name,
  startedBy,
  prepare,
  onLeave,
  onClose,
}: DiscardDialogProps) {
  const me = useMe();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [request, setRequest] = useState<Request>({ busy: false, canConfirm: true });
  // Read by the close and by the request's end, whichever comes first, rather than from state, which a close that
  // came before the page had rendered the request's end would read as it was before.
  const open = useRef(true);
  const out = useRef(false);
  const noLongerPossible = useRef(false);
  const { mutate } = useMutation({
    mutationFn: () => api(`${noteBase(participantId, noteDate)}/discard`, { method: 'POST' }),
  });
  const date = parseDateOnly(noteDate);
  const lines = [
    ...(noteDate < me.today ? [msg.draftIsFor(dateLong(date))] : []),
    ...(startedBy === undefined
      ? []
      : [msg.startedItAt(startedBy.authorDisplayName, stamp(startedBy.startedAtUtc, date))]),
    strings.note.nobodyCanOpenThisDraft,
  ];

  function leaveForToday() {
    removeToday(queryClient);
    void invalidatePastNotes(queryClient, participantId);
    onLeave?.();
    void navigate('/', { replace: true });
  }

  // TanStack calls the callbacks given to mutate() only while the page is still shown, so that a request that ends
  // after the person has gone elsewhere takes them nowhere.
  async function confirm() {
    out.current = true;
    setRequest({ busy: true, canConfirm: true });
    const allowed = prepare === undefined || (await prepare());
    if (!allowed) {
      return;
    }

    // Closed by a browser while `prepare()` was awaited, so the person went back before anything was sent.
    if (!open.current) {
      out.current = false;
      onClose();
      return;
    }

    mutate(undefined, { onSuccess: leaveForToday, onError: failed });
  }

  function failed(error: unknown) {
    out.current = false;
    const gone = error instanceof ApiError && error.code === 'note.not_a_draft';
    if (!open.current) {
      if (gone) {
        leaveForToday();
      } else {
        onClose(failureWords(error));
      }

      return;
    }

    noLongerPossible.current = gone;
    setRequest(
      gone
        ? { busy: false, error: apiErrorCopy['note.not_a_draft'], canConfirm: false }
        : { busy: false, error: failureWords(error), canConfirm: true },
    );
  }

  // Any close: Go back, Escape, Android's back gesture, or the shell's plain close() before the warning before the idle
  // sign-out or sign-in in place. With the request out, its end decides what follows.
  function closed() {
    open.current = false;
    if (out.current) {
      return;
    }

    if (noLongerPossible.current) {
      leaveForToday();
    } else {
      onClose();
    }
  }

  return (
    <ConfirmDialog
      title={msg.discardTitle(name)}
      primary={msg.discardDraftFor(name)}
      secondary={strings.goBack}
      confirmButton="primary"
      initialFocus="title"
      tone="warning"
      onConfirm={() => void confirm()}
      busy={request.busy}
      busyLabel={strings.note.discarding}
      error={request.error}
      canConfirm={request.canConfirm}
      onClose={closed}
    >
      {lines.map((line) => (
        <p key={line}>{line}</p>
      ))}
    </ConfirmDialog>
  );
}

/** What a failed Discard says, by its cause: nothing for a `401`, which the session flow has. */
function failureWords(error: unknown): string | undefined {
  const cause = classify(error);
  return cause === 'signedOut' ? undefined : msg.notDiscarded(cause);
}
