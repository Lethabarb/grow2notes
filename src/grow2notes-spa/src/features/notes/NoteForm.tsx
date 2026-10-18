import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRef, useState, type ReactNode } from 'react';
import { flushSync } from 'react-dom';
import { useLocation, useNavigate } from 'react-router';
import { api, ApiError, classify } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { ConfirmDialog } from '../../components/ConfirmDialog.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { Notice } from '../../components/Notice.tsx';
import { useAnnounce } from '../../components/pageStatusContext.ts';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { TickListField } from '../../components/TickListField.tsx';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { dateLong, parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { messageStore } from '../../routes/messageStore.ts';
import { openerBefore } from '../../routes/routeChange.ts';
import { removeToday } from '../today/todayQuery.ts';
import { FlagForManager, reasonId } from './FlagForManager.tsx';
import { GuidedNotes, guidedNotesId } from './GuidedNotes.tsx';
import { checkNote, type NoteErrors } from './noteChecks.ts';
import styles from './NotePage.module.css';
import { noteBase, type Draft } from './noteQueries.ts';
import type { SaveIndicator } from './SaveIndicator.tsx';
import { sentContent, useAutosave, type DeadEnd, type DraftContent } from './useAutosave.ts';

/** `POST {base}/versions`' answer (design.md §6.3): the number of the version saved, and when. */
type VersionSaved = { versionNumber: number; createdAtUtc: string };

/** The Submit confirmation while it is open, and how its request goes (note-form.md *Confirmation dialog*). */
type Confirmation = {
  /**
   * The form's content as the check save sent it, which its flag line shows and its request sends: what the server
   * holds, with any change made while an earlier save was out, and what the inert page shows while it is open.
   */
  content: DraftContent;
  /** The request's `Idempotency-Key`, made as the confirmation opened, which each retry in it sends again. */
  key: string;
  /** The request is out. */
  busy: boolean;
  /** Why it failed, in the alert line. */
  error?: string;
  /** False once the note was found submitted meanwhile: the confirming button goes. */
  canConfirm: boolean;
};

type NoteFormProps = {
  participantId: string;
  noteDate: string;
  /** The participant's full name, as stored, which the confirmation and Today's banner name. */
  name: string;
  givenName: string;
  draft: Draft;
  /** The page's `<h1>`, which follows the error summary (note-form.md components 2 and 3). */
  heading: ReactNode;
  /** What the save indicator says, none until autosave first says it. */
  indicator: SaveIndicator | undefined;
  /** The sentence of the dead end that stopped autosave, in the top bar's second row, if one has. */
  deadEnd: string | undefined;
  onIndicator: (indicator: SaveIndicator) => void;
  onDeadEnd: (deadEnd: DeadEnd) => void;
};

/**
 * The note form (note-form.md components 2, 7, 8 and 10 to 14): the error summary after a press of Submit note that
 * its checks refused, then the page's `<h1>`; then, in a `<form noValidate>`, the lists-changed notice's live region,
 * there and empty from the first render, as a screen reader may not announce words written into one added with them;
 * Goals, its goals and ticks in this page's state, from the draft's; Guided notes; Flag for manager, its tick and
 * reason in this page's state too, the reason kept while the box is unticked; and Submit note, with the alert above it.
 * Until T13 adds "2. Common items", the sections go from 1 to 3, keeping design.md's numbers. Each tick and each input
 * is a change that autosave saves, and a field that loses focus sends one at once (design.md §3.4). After a dead end,
 * every tick and the text stay as they are, to change and copy, though nothing more is saved (note-form.md *Conflicts
 * and refusals*).
 *
 * Submit note is never disabled: while "Saving…" shows, or a dead end has stopped autosave, it is `aria-disabled`, and
 * it is described by the save indicator, and by the dead end's sentence while one shows. A press, or Enter's implicit
 * submission, which comes as a click on it, runs Submit's checks first; during a dead end it then says the sentence
 * again in the page's polite region and opens nothing; otherwise a check save runs, the button busy, and the
 * confirmation opens once it has saved the newest change, with the content that it saved, checked again, as a change
 * made while an earlier save was out goes with it (note-form.md *Pressing Submit note*). The button stays busy while
 * Submit's request is out, though a browser closes the confirmation meanwhile, so that no press sends a save or opens
 * another confirmation until the request ends.
 */
export function NoteForm({
  participantId,
  noteDate,
  name,
  givenName,
  draft,
  heading,
  indicator,
  deadEnd,
  onIndicator,
  onDeadEnd,
}: NoteFormProps) {
  const me = useMe();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { key: entryKey } = useLocation();
  const announce = useAnnounce();
  const box = useRef<HTMLTextAreaElement>(null);
  const notice = useRef<HTMLDivElement>(null);
  const [goals, setGoals] = useState(draft.goals);
  const [ticked, setTicked] = useState(
    () => new Set(draft.goals.filter((goal) => goal.isTicked).map((goal) => goal.id)),
  );
  const [listsChanged, setListsChanged] = useState(false);
  const [flagged, setFlagged] = useState(draft.isFlagged);
  const [reason, setReason] = useState(draft.flagReason ?? '');
  // The messages of the fields that the last press refused, each gone with the input that fixes it.
  const [errors, setErrors] = useState<NoteErrors>({});
  // A 422 from Submit, the summary's one unlinked item until the next press.
  const [unexplained, setUnexplained] = useState(false);
  // The presses that the checks refused, and the 422s, so that the summary takes focus on each.
  const [attempt, setAttempt] = useState(0);
  // A request of Submit's is out: the check save, from a press until it is answered, or the version, from the
  // confirming press until it ends, which may be after a browser has closed the confirmation (confirm-dialog.md
  // *Behaviour* 7).
  const [busy, setBusy] = useState(false);
  const [confirmation, setConfirmation] = useState<Confirmation>();
  // Why Submit failed after a browser closed the confirmation while its request was out (confirm-dialog.md
  // *Behaviour* 7), above Submit note.
  const [failure, setFailure] = useState<string>();
  // Whether the confirmation is still open as its request ends.
  const confirmationOpen = useRef(false);
  const hasPrompts = draft.guidePrompts !== '';

  function read(): DraftContent {
    return { narrative: box.current!.value, tickedGoalIds: [...ticked], isFlagged: flagged, flagReason: reason };
  }

  const autosave = useAutosave({
    participantId,
    noteDate,
    opened: draft,
    read,
    onIndicator,
    onListsChanged: showListsAsNow,
    onDeadEnd,
  });

  const { mutate } = useMutation({
    mutationFn: ({ key, content }: Pick<Confirmation, 'key' | 'content'>) =>
      api<VersionSaved>(`${noteBase(participantId, noteDate)}/versions`, {
        method: 'POST',
        body: { baseVersion: draft.baseVersion, ...sentContent(content) },
        idempotencyKey: key,
      }),
  });

  // The goals as the form now has them, after a first save found the lists changed (design.md §5.7): the ticks of the
  // goals still listed kept, by ID, the text as it is, and the notice above Goals until the person leaves. Committed at
  // once, so that the save sent next reads the ticks shown, and focus, if the box that had it has gone, goes to the
  // notice; otherwise it stays where it is.
  function showListsAsNow(reloaded: Draft) {
    const focused = document.activeElement;
    flushSync(() => {
      setGoals(reloaded.goals);
      setTicked((current) => new Set(reloaded.goals.filter((goal) => current.has(goal.id)).map((goal) => goal.id)));
      setListsChanged(true);
    });
    if (focused?.isConnected === false) {
      notice.current?.focus();
    }
  }

  function toggle(goalId: string, isTicked: boolean) {
    setTicked((current) => {
      const next = new Set(current);
      if (isTicked) {
        next.add(goalId);
      } else {
        next.delete(goalId);
      }

      return next;
    });
    autosave.changed();
  }

  // An input into a field with a message checks it again (form-validation.md *Fixing*): the message goes, silently,
  // with the input that fixes it, or says what the field now fails; a field with none gets none while typing.
  function recheck(content: DraftContent) {
    if (errors.narrative === undefined && errors.flagReason === undefined) {
      return;
    }

    const now = checkNote(content, hasPrompts);
    setErrors((shown) => ({
      narrative: shown.narrative === undefined ? undefined : now.narrative,
      flagReason: shown.flagReason === undefined ? undefined : now.flagReason,
    }));
  }

  // Shows the messages of Submit's checks on `content`, the summary taking focus if a field fails; whether all pass.
  function passesChecks(content: DraftContent): boolean {
    const found = checkNote(content, hasPrompts);
    setErrors(found);
    const passes = found.narrative === undefined && found.flagReason === undefined;
    if (!passes) {
      setAttempt((count) => count + 1);
    }

    return passes;
  }

  async function pressSubmit() {
    setUnexplained(false);
    setFailure(undefined);
    if (!passesChecks(read())) {
      return;
    }

    if (deadEnd !== undefined) {
      announce?.(deadEnd);
      return;
    }

    setBusy(true);
    const saved = await autosave.checkSave();
    setBusy(false);
    // The checks again on what the check save sent, which a change made while an earlier save was out has joined.
    if (saved !== undefined && passesChecks(saved)) {
      confirmationOpen.current = true;
      setConfirmation({ content: saved, key: crypto.randomUUID(), busy: false, canConfirm: true });
    }
  }

  // The confirming button (note-form.md *In the confirmation*): autosave paused, once any save out has settled, so that
  // no save lands after the version; then the version, with this opening's key.
  async function confirm(open: Confirmation) {
    setConfirmation({ ...open, busy: true, error: undefined });
    setBusy(true);
    await autosave.stop();
    mutate(open, {
      onSuccess: submitted,
      onError: (error) => {
        notSubmitted(open, error);
      },
    });
  }

  // After `201`, or `200` for a retry whose first answer was lost (note-form.md *After a successful submit*): the
  // banner's sentence in the store, which Today reads; Today's list dropped, so that Today shows only the server's
  // rows; the confirmation closed; then Today, popped to when it is the entry before, so that Back never reopens the
  // finished form, and else put in the form's place.
  function submitted() {
    messageStore.set(msg.noteSubmitted(name));
    removeToday(queryClient);
    confirmationOpen.current = false;
    setConfirmation(undefined);
    if (openerBefore(entryKey)?.path === '/') {
      void navigate(-1);
    } else {
      void navigate('/', { replace: true });
    }
  }

  // A version not saved (note-form.md *Confirmation dialog*). A `422` closes the confirmation for the summary's one
  // unlinked item (T09's Notes): the confirmation sends only content that the form's checks have passed, so the
  // server refuses what they do not explain, such as a text of U+0085 alone, which .NET counts as blank and `trim()`
  // keeps; no server words show, and the summary has an item to take focus. While it is open, it keeps its key for a
  // retry: no connection or a server's failure, with the confirming button ready again; or a note submitted
  // meanwhile, after which Go back leaves the form a dead end; a `401` says nothing, as the shell closes the
  // confirmation before signing in again. Once a browser has closed it, the form shows the result itself: the failure
  // above Submit note, or the dead end.
  function notSubmitted(open: Confirmation, error: unknown) {
    setBusy(false);
    if (error instanceof ApiError && error.status === 422) {
      confirmationOpen.current = false;
      setConfirmation(undefined);
      setErrors({});
      setUnexplained(true);
      setAttempt((count) => count + 1);
      autosave.resume();
      return;
    }

    const conflict = error instanceof ApiError && error.code === 'note.version_conflict';
    if (confirmationOpen.current) {
      setConfirmation(
        conflict
          ? { ...open, busy: false, error: apiErrorCopy['note.version_conflict'], canConfirm: false }
          : { ...open, busy: false, error: failureWords(error) },
      );
    } else if (conflict) {
      autosave.end({ code: 'note.version_conflict' });
    } else {
      setFailure(failureWords(error));
      autosave.resume();
    }
  }

  // Go back, Escape, Android's back gesture, or the shell's plain close() before its warning or sign-in in place: focus
  // goes back to Submit note by itself, nothing is sent, and autosave goes on; or, after the note was found submitted
  // meanwhile, the form is a dead end. A close while the request is out leaves the request to end, and the form shows
  // how.
  function closeConfirmation(closed: Confirmation) {
    confirmationOpen.current = false;
    setConfirmation(undefined);
    if (closed.busy) {
      return;
    }

    if (closed.canConfirm) {
      autosave.resume();
    } else {
      autosave.end({ code: 'note.version_conflict' });
    }
  }

  const summary: FormError[] = [
    ...(unexplained ? [{ message: msg.notSubmitted('server') }] : []),
    ...(errors.narrative === undefined ? [] : [{ message: errors.narrative, fieldId: guidedNotesId }]),
    ...(errors.flagReason === undefined ? [] : [{ message: errors.flagReason, fieldId: reasonId }]),
  ];

  return (
    <>
      <ErrorSummary errors={summary} attempt={attempt} />
      {heading}
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          void pressSubmit();
        }}
        // React's onBlur bubbles, as focusout does, so it hears every field of the form.
        onBlur={autosave.flush}
      >
        <div ref={notice} role="status" tabIndex={-1} className={styles.listsChanged}>
          {listsChanged && <Notice>{apiErrorCopy['note.lists_changed']}</Notice>}
        </div>
        <TickListField
          idPrefix="goal"
          className={styles.section}
          heading={strings.note.goals}
          hint={strings.note.goalsHint}
          emptyText={msg.noGoalsSetUp(givenName)}
          items={goals}
          ticked={ticked}
          onToggle={toggle}
        />
        <GuidedNotes
          boxRef={box}
          initialText={draft.narrative}
          prompts={draft.guidePrompts}
          error={errors.narrative}
          onChange={() => {
            autosave.changed();
            recheck(read());
          }}
        />
        <FlagForManager
          flagged={flagged}
          reason={reason}
          error={errors.flagReason}
          onFlaggedChange={(isFlagged) => {
            setFlagged(isFlagged);
            autosave.changed();
            // Unticking takes the reason's message away everywhere; ticking again brings none until the next press.
            if (!isFlagged) {
              setErrors((shown) => ({ ...shown, flagReason: undefined }));
            }
          }}
          onReasonChange={(text) => {
            setReason(text);
            autosave.changed();
            recheck({ ...read(), flagReason: text });
          }}
        />
        <div className={styles.actions}>
          <RequestAlert>{failure}</RequestAlert>
          <Button
            type="submit"
            variant="primary"
            busy={busy}
            unavailable={indicator?.kind === 'saving' || deadEnd !== undefined}
            onUnavailablePress={() => void pressSubmit()}
            aria-describedby={deadEnd === undefined ? 'save-status' : 'save-status dead-end'}
          >
            {strings.note.submit}
          </Button>
        </div>
      </form>
      {confirmation !== undefined && (
        <SubmitConfirmation
          name={name}
          noteDate={noteDate}
          onToday={noteDate === me.today}
          confirmation={confirmation}
          onConfirm={() => void confirm(confirmation)}
          onClose={() => {
            closeConfirmation(confirmation);
          }}
        />
      )}
    </>
  );
}

/** What a failed Submit says, by its cause: nothing for a `401`, which the session flow has. */
function failureWords(error: unknown): string | undefined {
  const cause = classify(error);
  return cause === 'signedOut' ? undefined : msg.notSubmitted(cause);
}

type SubmitConfirmationProps = {
  name: string;
  noteDate: string;
  /** Whether the note is dated the server's Melbourne today, which "today's note" then names. */
  onToday: boolean;
  confirmation: Confirmation;
  onConfirm: () => void;
  onClose: () => void;
};

/**
 * The Submit confirmation (note-form.md component 14, confirm-dialog.md's dialog 1), on T03's `ConfirmDialog`, opened
 * on its title, so that no Enter or second tap confirms before the name is seen: "Submit today's note for", or "Submit
 * the note for" for any other day's, then the name on its own line; the note date, and whether the note is flagged for
 * a manager, from the values that Submit sends, and nothing else; then "Submit note for Jane Citizen", and Go back.
 * Its request shows "Submitting…" once it has taken 1 s.
 */
function SubmitConfirmation({ name, noteDate, onToday, confirmation, onConfirm, onClose }: SubmitConfirmationProps) {
  const { content, busy, error, canConfirm } = confirmation;
  return (
    <ConfirmDialog
      title={onToday ? strings.note.submitTodaysNoteFor : strings.note.submitTheNoteFor}
      titleName={name}
      primary={msg.submitNoteFor(name)}
      secondary={strings.goBack}
      confirmButton="primary"
      initialFocus="title"
      onConfirm={onConfirm}
      busy={busy}
      busyLabel={strings.note.submitting}
      error={error}
      canConfirm={canConfirm}
      onClose={onClose}
    >
      <p>
        <time dateTime={noteDate}>{dateLong(parseDateOnly(noteDate))}</time>
      </p>
      <p>{content.isFlagged ? strings.note.flaggedYes : strings.note.flaggedNo}</p>
    </ConfirmDialog>
  );
}
