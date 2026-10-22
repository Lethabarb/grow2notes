import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useImperativeHandle, useLayoutEffect, useRef, useState, type Ref } from 'react';
import { flushSync } from 'react-dom';
import { useLocation, useNavigate } from 'react-router';
import { api, ApiError, classify, isNotEditable } from '../../api/api.ts';
import { refreshMe, useMe } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { ButtonGroup } from '../../components/ButtonGroup.tsx';
import { ConfirmDialog } from '../../components/ConfirmDialog.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { focusPageHeading } from '../../components/focusPageHeading.ts';
import { Notice } from '../../components/Notice.tsx';
import { useAnnounce } from '../../components/pageStatusContext.ts';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { TickListField } from '../../components/TickListField.tsx';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { dateLong, parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { strings, type Cause } from '../../copy/strings.ts';
import { messageStore } from '../../routes/messageStore.ts';
import { openerBefore } from '../../routes/routeChange.ts';
import { useUnloadWarning } from '../../routes/useUnloadWarning.ts';
import { invalidateToday, removeToday } from '../today/todayQuery.ts';
import { CommonItemsField } from './CommonItemsField.tsx';
import { isOffered, shownItems } from './commonItemGroups.ts';
import { FlagForManager, reasonId } from './FlagForManager.tsx';
import { GuidedNotes, guidedNotesId } from './GuidedNotes.tsx';
import { MoreActions, type MoreActionsHandle } from './MoreActions.tsx';
import { checkNote, type NoteErrors } from './noteChecks.ts';
import { NoteHeading } from './NoteHeading.tsx';
import styles from './NotePage.module.css';
import { noteBase, type Draft, type DraftGroup } from './noteQueries.ts';
import { invalidatePastNotes } from './pastNotesQuery.ts';
import { PastNotesLink } from './PastNotesLink.tsx';
import type { SaveIndicator } from './SaveIndicator.tsx';
import { sentContent, useAutosave, type BannerDeadEnd, type DraftContent } from './useAutosave.ts';
import { useSaveBeforeLeaving } from './useSaveBeforeLeaving.ts';
import { VersionConflictNotice } from './VersionConflictNotice.tsx';

/**
 * `POST {base}/versions`' answer (design.md §6.3): the number of the version saved, and when; and the note's flag
 * status, the `FlagStatus` name, to every caller, which the form reads only to refresh `me` and shows nowhere.
 */
export type VersionSaved = {
  versionNumber: number;
  createdAtUtc: string;
  flagStatus: 'None' | 'ToReview' | 'Reviewed';
};

/**
 * What the save indicator says while the newest change is not saved, or the takeover banner waits: "Saving…", "Not
 * saved: no connection…", and "Not saved", from the takeover or a dead end, after which it never will be; the browser
 * asks before the page goes while it says one of them.
 */
const atRiskKinds = new Set<SaveIndicator['kind']>(['saving', 'failed', 'takenOver', 'notSaved']);

const idOf = ({ id }: { id: string }) => id;

const isTickedItem = (item: { isTicked: boolean }) => item.isTicked;

/** The IDs of the groups of a snapshot that are picked. */
function pickedIn(groups: readonly DraftGroup[]): Set<string> {
  return new Set(groups.filter((group) => group.isPicked).map(idOf));
}

/** The IDs of the goals and common items of a form that are ticked, in the one set that the form keeps. */
function tickedIn({ goals, commonItemGroups }: Draft): Set<string> {
  return new Set([...goals, ...commonItemGroups.flatMap((group) => group.items)].filter(isTickedItem).map(idOf));
}

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

/**
 * A Save changes attempt: the content it sent, as the check save saved it, with the version it was based on and its
 * `Idempotency-Key`, which a press with the same content sends again, after a failure with no answer (note-form.md
 * *Pressing Submit note*, design.md §6.3).
 */
type Attempt = Pick<Confirmation, 'content' | 'key'> & { baseVersion: number };

/**
 * The version-conflict notice while it shows (note-form.md component 6): how many conflicts have met the form, which
 * opens it anew for each, whether it is due focus, as a press of Save changes met the conflict, and whether Start again
 * has been pressed, after which it stays, closed, for reference, until Save changes succeeds.
 */
type Conflict = { count: number; focus: boolean; restarted: boolean };

/** What the takeover banner in the top bar asks of the form (note-form.md *Changed on another device or tab*). */
export type NoteFormHandle = {
  keep: () => void;
  /** Settles once the read of the other version is answered, shown or not. */
  loadOther: () => Promise<void>;
};

type NoteFormProps = {
  ref?: Ref<NoteFormHandle>;
  participantId: string;
  noteDate: string;
  /** The participant's full name, as stored, which the confirmation and Today's banner name. */
  name: string;
  givenName: string;
  draft: Draft;
  /** What the save indicator says, none until autosave first says it. */
  indicator: SaveIndicator | undefined;
  /** The sentence of the dead end that stopped autosave, in the top bar's second row, if one has. */
  deadEnd: string | undefined;
  onIndicator: (indicator: SaveIndicator) => void;
  /** A dead end for the top bar's banner: not an edit's refusal or version conflict, which the form handles. */
  onDeadEnd: (deadEnd: BannerDeadEnd) => void;
};

/**
 * The note form (note-form.md components 2, 4, 5, 7, 8 and 10 to 14): the error summary after a press of Submit note
 * that its checks refused, then the page's `<h1>`; the earlier-day notice, on a draft saved before the form opened,
 * never on a new note, saved since or not, as note-form.md's *Kinds of note* are set as the form loads, while it is
 * dated before `me.today`, so that it just appears, unannounced, once `me` gives a day that has passed midnight with
 * the form open (status-messages.md *Per-screen notes*); and the Past notes link; then, in a `<form noValidate>`, the
 * lists-changed notice's live region, there and empty from the first render, as a screen reader may not announce
 * words written into one added with them; Goals, its goals and ticks in this page's state, from the draft's; Common
 * items, its groups, picks and ticks in this page's state too, the picks as copied by the template or saved; Guided
 * notes; Flag for manager, its tick and reason in this page's state too, the reason kept while the box is unticked;
 * and Submit note, with the alert above it; then, outside the form, More actions, with Discard draft, while the note
 * is the viewer's own saved draft, reopened or saved since: never on a new note before its first "Saved …", and
 * through the takeover banner's wait, as the draft is still theirs, until a dead end, as the note at this address may
 * then be no draft of theirs, when it goes with its panel and any Discard dialog, nothing sent, focus that was in them
 * going to the `<h1>` (T12's Notes).
 * Each pick, tick and input is a change that autosave saves, and a field that loses focus sends one at once (design.md
 * §3.4). After a dead end, every tick and the text stay as they are, to change and copy, though nothing more is saved
 * (note-form.md *Conflicts and refusals*).
 *
 * Submit note is never disabled: while "Saving…" or "Not saved: no connection…" shows, or a dead end has stopped
 * autosave, it is `aria-disabled`, and it is described by the save indicator, and by the dead end's sentence while one
 * shows. A press, or Enter's implicit submission, which comes as a click on it, runs Submit's checks first; during
 * "Not saved: no connection…" or a dead end it then says that sentence again in the page's polite region and opens
 * nothing; otherwise a check save runs, the button busy, and the confirmation opens once it has saved the newest
 * change, with the content that it saved, checked again, as a change made while an earlier save was out goes with it
 * (note-form.md *Pressing Submit note*). The button stays busy while Submit's request is out, though a browser closes
 * the confirmation meanwhile, so that no press sends a save or opens another confirmation until the request ends.
 *
 * While another page's save has taken the note over, the page's takeover banner asks what to do, and Submit note is
 * unavailable in the same way, described by the banner's sentence too, which a press says again; every tick and the
 * text stay as they are, to change, though nothing is sent (note-form.md *Conflicts and refusals*). Keep the text on
 * this screen gives focus back to what had it as the note was taken over, or else the `<h1>`, and sends everything on
 * screen. Load the other version shows it in place of the form's ticks, text, flag and reason, with focus on the
 * `<h1>`; or, if its read failed, says so in the page's polite region, the banner kept (note-form.md *Changed on
 * another device or tab*).
 *
 * Leaving within the app, by the back link, Past notes, the nav, or Back or Forward, with a change unsaved, sends it
 * now, and goes once it is saved, "Saving…" shown meanwhile; with "Not saved: no connection…" shown, the takeover
 * banner showing, or a save that fails, it stays, with no dialog, and says why in the page's polite region, unless the
 * top bar has just said it; and a save of its own that meets a dead end keeps the person there too, with the text to
 * copy, said by the dead end's own sentence alone. A leave once a dead end shows, and after Submit's `201`, nothing
 * stops (note-form.md *Leaving the form*). The browser asks before the page goes while the indicator says the change
 * is not saved, a dead end's "Not saved" included, and the shell has not disarmed the warning (item 35). Today's lists,
 * its unfinished drafts with the day's, are marked stale as the form goes, whichever way, so that Today shows how the
 * note was left.
 *
 * Confirming a Discard pauses autosave once any save out has settled, sending no save after it, and sends nothing if
 * that save met a dead end; its `204` lets the leave guard let the move to Today go, a change unsaved or not, as the
 * draft has gone. A Discard dialog closed with nothing discarded lets autosave go on, sending at once a change that the
 * pause held, and a failure of a request that a browser's close left out shows above Submit note, as Submit's does.
 *
 * Opened on a pending edit, a draft with a base version above 0 (note-form.md *Kinds of note*), it is the edit mode
 * of a submitted note: the `<h1>` has the caption "Editing submitted note (version 2)", which Start again and Load move
 * to the version they show; the lists that an empty version leaves say "No goals set" and "No common items set"; there
 * is no earlier-day notice and no More actions; and Save changes and Cancel take the place of Submit note. Save changes
 * runs Submit's press, the checks and the check save, then `POST {base}/versions` at the base with no confirmation, its
 * `Idempotency-Key` kept for a press with the same content after a failure with no answer, which skips the check save,
 * as that would meet the version the lost answer made. Cancel stops autosave and sends `DELETE {base}/draft` for any
 * state of the form, a dead end included. Each leaves the form by `replace` to the read view, dropping the note's
 * queries first so that it never shows what it read before, and no message is shown. A newer version met on a press, by
 * the check save or the version, or by a background save, shows the version-conflict notice in place of Save changes,
 * which waits for Start again; a refused edit, from a press, shows the whole-page "You cannot edit this note", while
 * a background save that meets it stops quietly, "Not saved", so that Cancel still works (A70 to A72).
 */
export function NoteForm({
  ref,
  participantId,
  noteDate,
  name,
  givenName,
  draft,
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
  const conflictNotice = useRef<HTMLElement>(null);
  const editing = draft.baseVersion > 0;
  // The version that a pending edit is based on, which Start again and Load move on, and Save changes sends.
  const [base, setBase] = useState(draft.baseVersion);
  const [goals, setGoals] = useState(draft.goals);
  const [groups, setGroups] = useState(draft.commonItemGroups);
  const [picked, setPicked] = useState(() => pickedIn(draft.commonItemGroups));
  // The goals and common items ticked, by ID, in one set, so that one toggle serves both.
  const [ticked, setTicked] = useState(() => tickedIn(draft));
  const [listsChanged, setListsChanged] = useState(false);
  // The text that the Guided notes box opens with, and the versions loaded in place of the draft's, by each of which
  // the uncontrolled box opens anew with the version's text.
  const [textLoaded, setTextLoaded] = useState({ text: draft.narrative, loads: 0 });
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
  // Why Submit or Discard failed after a browser closed its dialog while its request was out (confirm-dialog.md
  // *Behaviour* 7), above Submit note.
  const [failure, setFailure] = useState<string>();
  // Cancel's request is out.
  const [cancelling, setCancelling] = useState(false);
  // An edit that is no longer allowed, which a press of Save changes shows as the whole page, thrown at render.
  const [refusal, setRefusal] = useState<ApiError>();
  const [conflict, setConflict] = useState<Conflict>();
  // A press of Save changes is under way, so that a conflict it meets takes focus; and an edit refused to a background
  // save, which the next press shows.
  const pressing = useRef(false);
  const notEditable = useRef(false);
  const lastAttempt = useRef<Attempt>(undefined);
  // Whether the note exists as the viewer's draft: saved before the form opened, or since, as the indicator has said
  // "Saved …"; no longer once it says nothing again, as after Load found the note discarded meanwhile.
  const [exists, setExists] = useState(draft.isSaved);
  const moreActions = useRef<MoreActionsHandle>(null);
  // Focus was in More actions as a dead end took it away, so the `<h1>` takes it once it has gone.
  const headingDue = useRef(false);
  // Whether the confirmation is still open as its request ends.
  const confirmationOpen = useRef(false);
  // What had focus as the note was taken over, which Keep gives it back to.
  const focusedAtTakeover = useRef<HTMLElement>(null);
  // What the save indicator says, as autosave says it, before the page renders it, which a leave that stays reads.
  const lastSaid = useRef<SaveIndicator['kind']>(undefined);
  const hasPrompts = draft.guidePrompts !== '';
  const takenOver = indicator?.kind === 'takenOver';
  const atRisk = indicator !== undefined && atRiskKinds.has(indicator.kind);

  function read(): DraftContent {
    return {
      narrative: box.current!.value,
      tickedGoalIds: goals.filter((goal) => ticked.has(goal.id)).map((goal) => goal.id),
      pickedGroupIds: [...picked],
      tickedCommonItemIds: shownItems(groups, picked)
        .filter((item) => ticked.has(item.id))
        .map((item) => item.id),
      isFlagged: flagged,
      flagReason: reason,
    };
  }

  const autosave = useAutosave({
    participantId,
    noteDate,
    opened: draft,
    read,
    onIndicator: (said) => {
      lastSaid.current = said.kind;
      if (said.kind === 'takenOver') {
        const focused = document.activeElement;
        focusedAtTakeover.current = focused instanceof HTMLElement && focused !== document.body ? focused : null;
      }

      if (said.kind === 'draft' || said.kind === 'saved') {
        setExists(true);
      } else if (said.kind === 'none') {
        setExists(false);
      }

      onIndicator(said);
    },
    onListsChanged: showListsAsNow,
    onOtherVersion: showOtherVersion,
    onDeadEnd: (stopped) => {
      if (stopped.code === 'note.not_editable') {
        notEditable.current = true;
        return;
      }

      if (editing && stopped.code === 'note.version_conflict') {
        const focus = pressing.current;
        setConflict((shown) => ({ count: (shown?.count ?? 0) + 1, focus, restarted: false }));
        return;
      }

      headingDue.current = moreActions.current?.hasFocus() ?? false;
      onDeadEnd(stopped);
    },
  });

  const leaveFreely = useSaveBeforeLeaving({ unsaved: autosave.unsaved, save: saveBeforeLeaving });
  useUnloadWarning(atRisk);

  useEffect(
    () => () => {
      void invalidateToday(queryClient);
    },
    [queryClient],
  );

  // Once More actions has gone with the dead end, as its dialog, if open, would have kept the `<h1>` inert.
  useLayoutEffect(() => {
    if (deadEnd !== undefined && headingDue.current) {
      headingDue.current = false;
      focusPageHeading();
    }
  }, [deadEnd]);

  // Once the notice shows for a conflict that a press met, focus goes to it, scrolled into view.
  useEffect(() => {
    if (conflict?.focus === true) {
      conflictNotice.current?.focus();
    }
  }, [conflict]);

  // Submit's version, at base 0, and Save changes', at the version being edited: one request, with one key each try.
  const { mutate } = useMutation({
    mutationFn: ({ key, content, baseVersion }: Pick<Confirmation, 'key' | 'content'> & { baseVersion: number }) =>
      api<VersionSaved>(`${noteBase(participantId, noteDate)}/versions`, {
        method: 'POST',
        body: { baseVersion, ...sentContent(content) },
        idempotencyKey: key,
      }),
  });

  const { mutate: cancelEdit } = useMutation({
    mutationFn: () => api<void>(`${noteBase(participantId, noteDate)}/draft`, { method: 'DELETE' }),
  });

  // The lists as the form now has them, after a first save found them changed (design.md §5.7): the picks of the
  // groups still offered kept, by ID, and the ticks of the goals still listed and of the items still shown, so that an
  // item moved into a group that is not picked loses its tick; the text as it is, and the notice above Goals until the
  // person leaves. Committed at once, so that the save sent next reads the picks and ticks shown, and focus, if the box
  // that had it has gone, goes to the notice; otherwise it stays where it is.
  function showListsAsNow(reloaded: Draft) {
    const focused = document.activeElement;
    const offered = new Set(reloaded.commonItemGroups.filter(isOffered).map((group) => group.id));
    const stillPicked = new Set([...picked].filter((id) => offered.has(id)));
    const stillShown = new Set([...reloaded.goals, ...shownItems(reloaded.commonItemGroups, stillPicked)].map(idOf));
    flushSync(() => {
      setGoals(reloaded.goals);
      setGroups(reloaded.commonItemGroups);
      setPicked(stillPicked);
      setTicked((current) => new Set([...current].filter((id) => stillShown.has(id))));
      setListsChanged(true);
    });
    if (focused?.isConnected === false) {
      notice.current?.focus();
    }
  }

  // The other version, as Load read it, in place of what the form held: its goals, groups, picks and ticks, its text,
  // its flag and its reason; and any message of Submit's checks checked again on it, as an input would.
  function showOtherVersion(other: Draft) {
    const content = {
      narrative: other.narrative,
      tickedGoalIds: other.goals.filter(isTickedItem).map(idOf),
      pickedGroupIds: [...pickedIn(other.commonItemGroups)],
      tickedCommonItemIds: other.commonItemGroups
        .flatMap((group) => group.items)
        .filter(isTickedItem)
        .map(idOf),
      isFlagged: other.isFlagged,
      flagReason: other.flagReason ?? '',
    };
    setGoals(other.goals);
    setGroups(other.commonItemGroups);
    setPicked(new Set(content.pickedGroupIds));
    setTicked(tickedIn(other));
    setTextLoaded((loaded) => ({ text: content.narrative, loads: loaded.loads + 1 }));
    setFlagged(content.isFlagged);
    setReason(content.flagReason);
    if (editing) {
      setBase(other.baseVersion);
    }

    recheck(content);
  }

  function keep() {
    const focused = focusedAtTakeover.current;
    focused?.focus();
    // Gone, or hidden since, as Reason is once the flag is unticked.
    if (focused === null || document.activeElement !== focused) {
      focusPageHeading();
    }

    autosave.keep();
  }

  async function loadOther() {
    const outcome = await autosave.load();
    if (outcome === 'shown' || outcome === 'deadEnd') {
      // From Load, which has gone with the banner.
      focusPageHeading();
    } else if (outcome === 'offline' || outcome === 'server') {
      announce?.(msg.loadFailed(strings.note.theNote, outcome, false));
    }
  }

  useImperativeHandle(ref, () => ({ keep, loadOther }));

  // A leave with something unsaved: whether it is all saved now, a change typed while a save was out included. If not,
  // the person stays, told why in the page's polite region, the takeover's sentence while its banner shows and
  // otherwise the failed save's, unless the save itself has just brought "Not saved: …" or the banner, whose own region
  // says it, so that it is said once. A dead end that the leave's save met keeps them there as well, with the text to
  // copy (autosave-status.md *Behaviour* 7, note-form.md *Conflicts and refusals*), said by the form's assertive
  // region alone; as nothing is unsaved after it, the next press leaves. Only a leave with something unsaved is
  // blocked, so a dead end showing now came while this one waited.
  async function saveBeforeLeaving(): Promise<boolean> {
    const before = lastSaid.current;
    while (autosave.unsaved()) {
      if (!(await autosave.flushNow())) {
        const now = lastSaid.current;
        if (now === before || (now !== 'failed' && now !== 'takenOver')) {
          announce?.(now === 'takenOver' ? apiErrorCopy['draft.taken_over'] : strings.note.notSavedRetrying);
        }

        return false;
      }
    }

    return lastSaid.current !== 'notSaved';
  }

  function toggle(itemId: string, isTicked: boolean) {
    setTicked((current) => {
      const next = new Set(current);
      if (isTicked) {
        next.add(itemId);
      } else {
        next.delete(itemId);
      }

      return next;
    });
    autosave.changed();
  }

  // A group picked or unpicked: unpicking takes its items' ticks away at once, with no question, so that what is on
  // screen is what is saved, and picking it again shows them unticked (group-picker.md *Behaviour*).
  function pick(group: DraftGroup, isPicked: boolean) {
    setPicked((current) => {
      const next = new Set(current);
      if (isPicked) {
        next.add(group.id);
      } else {
        next.delete(group.id);
      }

      return next;
    });
    if (!isPicked) {
      setTicked((current) => new Set([...current].filter((id) => !group.items.some((item) => item.id === id))));
    }

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

    if (takenOver) {
      announce?.(apiErrorCopy['draft.taken_over']);
      return;
    }

    if (indicator?.kind === 'failed') {
      announce?.(strings.note.notSavedRetrying);
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
    mutate(
      { ...open, baseVersion: base },
      {
        onSuccess: submitted,
        onError: (error) => {
          notSubmitted(open, error);
        },
      },
    );
  }

  // After `201`, or `200` for a retry whose first answer was lost (note-form.md *After a successful submit*): the
  // banner's sentence in the store, which Today reads; Today's lists dropped, its unfinished drafts with the day's, so
  // that Today shows only the server's rows; `me` asked for again when the note is in To review, so that the Flagged
  // badge counts it (app-shell.md *Refresh of `/api/auth/me`*), a request that the shell's own refresh on the move to
  // Today then shares; the confirmation closed; then Today, which the leave guard never stops, though a change was
  // made after a browser closed the confirmation, as the note is submitted: popped to when it is the entry before, so
  // that Back never reopens the finished form, and else put in the form's place.
  function submitted(saved: VersionSaved) {
    messageStore.set(msg.noteSubmitted(name));
    removeToday(queryClient);
    if (saved.flagStatus === 'ToReview') {
      // A failure leaves `me` as it was, as the shell's refreshes do.
      refreshMe(queryClient).catch(() => {});
    }

    confirmationOpen.current = false;
    setConfirmation(undefined);
    leaveFreely();
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

  // Whether this press repeats the last attempt that went unanswered: the same content on the same base, so the same key.
  function repeatedAttempt(content: DraftContent): Attempt | undefined {
    const last = lastAttempt.current;
    const same = last?.baseVersion === base && JSON.stringify(last.content) === JSON.stringify(content);
    return same ? last : undefined;
  }

  // A press of Save changes that ends without a request out.
  function pressEnded() {
    pressing.current = false;
    setBusy(false);
    if (notEditable.current) {
      setRefusal(new ApiError(403, 'note.not_editable'));
    }
  }

  // Save changes (note-form.md *Pressing Submit note*, autosave-status.md *Behaviour* 6), in Submit's order: the checks;
  // an edit refused to a background save, as the whole page; a conflict waiting, which gets focus again; a takeover or
  // a failed save, said again; then the check save, the checks again on what it saved, autosave stopped, and the
  // version with a new key. A press with the content of the last attempt that went unanswered sends no check save, which
  // would meet the version that attempt may have made, and sends its key again, so that a version it did make answers
  // `200`.
  async function pressSave() {
    if (cancelling) {
      return;
    }

    setUnexplained(false);
    setFailure(undefined);
    const content = read();
    if (!passesChecks(content)) {
      return;
    }

    if (notEditable.current) {
      pressEnded();
      return;
    }

    if (conflict !== undefined && !conflict.restarted) {
      setConflict({ ...conflict, focus: true });
      return;
    }

    if (deadEnd !== undefined) {
      announce?.(deadEnd);
      return;
    }

    if (takenOver) {
      announce?.(apiErrorCopy['draft.taken_over']);
      return;
    }

    if (indicator?.kind === 'failed') {
      announce?.(strings.note.notSavedRetrying);
      return;
    }

    pressing.current = true;
    setBusy(true);
    let attempt = repeatedAttempt(content);
    if (attempt === undefined) {
      const saved = await autosave.checkSave();
      if (saved === undefined || !passesChecks(saved)) {
        pressEnded();
        return;
      }

      attempt = { content: saved, key: crypto.randomUUID(), baseVersion: base };
    }

    if (!(await autosave.stop())) {
      pressEnded();
      return;
    }

    lastAttempt.current = attempt;
    mutate(attempt, {
      onSuccess: () => {
        leaveEditMode(true);
      },
      onError: (error) => {
        notSaved(error);
      },
    });
  }

  // A version not saved by Save changes. A `422` is the summary's one unlinked item and a new key next time; a newer
  // version, shown by the notice; a refused edit, the whole page; a `401` says nothing, as the shell has it, and is
  // never sent again; anything else is told above the buttons, the attempt kept for the press that tries again.
  function notSaved(error: unknown) {
    if (isNotEditable(error)) {
      notEditable.current = true;
      pressEnded();
      return;
    }

    if (error instanceof ApiError && error.code === 'note.version_conflict') {
      // The dead end stops autosave until Start again, and the form shows the notice, focused as a press met it.
      lastAttempt.current = undefined;
      autosave.end({ code: 'note.version_conflict' });
    } else {
      if (error instanceof ApiError && error.status === 422) {
        lastAttempt.current = undefined;
        setErrors({});
        setUnexplained(true);
        setAttempt((count) => count + 1);
      } else {
        setFailure(failureWords(error, msg.changesNotSaved));
      }

      autosave.resume();
    }

    pressing.current = false;
    setBusy(false);
  }

  // Cancel (autosave-status.md *Behaviour* 6): for any state of the form, a dead end and a conflict included, autosave
  // is stopped, whatever stopping answers, and then the pending edit is deleted. `204`, or `404` for one already gone,
  // leaves; a `401` says nothing; anything else is told above the buttons. The server never refuses it with a `403`.
  async function pressCancel() {
    if (busy || cancelling) {
      return;
    }

    setFailure(undefined);
    setCancelling(true);
    await autosave.stop();
    cancelEdit(undefined, {
      onSuccess: () => {
        leaveEditMode(false);
      },
      onError: (error) => {
        if (error instanceof ApiError && error.status === 404) {
          leaveEditMode(false);
          return;
        }

        setCancelling(false);
        setFailure(failureWords(error, msg.notCancelled));
        autosave.resume();
      },
    });
  }

  // After Save changes or Cancel: the note's queries removed, not marked stale, in the same turn as the move, so that
  // the read view that opens never shows the version read before; Past notes and the reviews list marked stale, `me`
  // asked for again after a save, as the flag may have changed the badge; then the read view in the form's place, which
  // the leave guard never stops, and which nothing says anything of.
  function leaveEditMode(saved: boolean) {
    queryClient.removeQueries({ queryKey: ['notes', participantId, noteDate] });
    void invalidatePastNotes(queryClient, participantId);
    void queryClient.invalidateQueries({ queryKey: ['reviews'] });
    if (saved) {
      // A failure leaves `me` as it was, as the shell's refreshes do.
      refreshMe(queryClient).catch(() => {});
    }

    leaveFreely();
    void navigate(`/participants/${participantId}/notes/${noteDate}`, { replace: true });
  }

  // Start again from the latest version: the base moves, so does the caption, and what is on screen is sent at once.
  function startAgain(version: number) {
    autosave.startAgain(version);
    setBase(version);
    setConflict((shown) => shown && { ...shown, focus: false, restarted: true });
    focusPageHeading();
  }

  if (refusal !== undefined) {
    // For the route's error boundary, which shows "You cannot edit this note" in place of the whole screen.
    throw refusal;
  }

  const summary: FormError[] = [
    ...(unexplained ? [{ message: editing ? msg.changesNotSaved('server') : msg.notSubmitted('server') }] : []),
    ...(errors.narrative === undefined ? [] : [{ message: errors.narrative, fieldId: guidedNotesId }]),
    ...(errors.flagReason === undefined ? [] : [{ message: errors.flagReason, fieldId: reasonId }]),
  ];

  return (
    <>
      <ErrorSummary errors={summary} attempt={attempt} />
      <NoteHeading name={name} noteDate={noteDate} caption={editing ? msg.editingSubmittedNote(base) : undefined} />
      {!editing && draft.isSaved && noteDate < me.today && (
        <Notice>{msg.earlierDayDraft(dateLong(parseDateOnly(noteDate)))}</Notice>
      )}
      <PastNotesLink participantId={participantId} />
      {conflict !== undefined && (
        <VersionConflictNotice
          key={conflict.count}
          ref={conflictNotice}
          participantId={participantId}
          noteDate={noteDate}
          conflicts={conflict.count}
          restarted={conflict.restarted}
          onStartAgain={startAgain}
        />
      )}
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          void (editing ? pressSave() : pressSubmit());
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
          emptyText={editing ? strings.readView.noGoals : msg.noGoalsSetUp(givenName)}
          items={goals}
          ticked={ticked}
          onToggle={toggle}
        />
        <CommonItemsField
          className={styles.section}
          groups={groups}
          picked={picked}
          ticked={ticked}
          emptyText={editing ? strings.readView.noCommonItems : strings.note.noCommonItemsSetUp}
          onPick={pick}
          onToggle={toggle}
        />
        <GuidedNotes
          key={textLoaded.loads}
          boxRef={box}
          initialText={textLoaded.text}
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
          {editing ? (
            <ButtonGroup>
              <Button
                type="submit"
                variant="primary"
                busy={busy}
                busyLabel={strings.note.savingChanges}
                unavailable={
                  indicator?.kind === 'saving' ||
                  indicator?.kind === 'failed' ||
                  takenOver ||
                  deadEnd !== undefined ||
                  (conflict !== undefined && !conflict.restarted)
                }
                onUnavailablePress={() => void pressSave()}
                aria-describedby={
                  deadEnd !== undefined ? 'save-status dead-end' : takenOver ? 'save-status taken-over' : 'save-status'
                }
              >
                {strings.note.saveChanges}
              </Button>
              <Button
                busy={cancelling}
                busyLabel={strings.note.cancelling}
                onClick={() => void pressCancel()}
              >
                {strings.cancel}
              </Button>
            </ButtonGroup>
          ) : (
            <Button
              type="submit"
              variant="primary"
              busy={busy}
              unavailable={
                indicator?.kind === 'saving' || indicator?.kind === 'failed' || takenOver || deadEnd !== undefined
              }
              onUnavailablePress={() => void pressSubmit()}
              aria-describedby={
                deadEnd !== undefined ? 'save-status dead-end' : takenOver ? 'save-status taken-over' : 'save-status'
              }
            >
              {strings.note.submit}
            </Button>
          )}
        </div>
      </form>
      {!editing && exists && deadEnd === undefined && (
        <MoreActions
          ref={moreActions}
          participantId={participantId}
          noteDate={noteDate}
          name={name}
          stopSaving={autosave.stop}
          onLeave={leaveFreely}
          onNotDiscarded={(discardFailure) => {
            // Only a failure of its own, so that a plain Go back keeps a Submit failure shown.
            if (discardFailure !== undefined) {
              setFailure(discardFailure);
            }

            autosave.resume();
          }}
        />
      )}
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

/** What a failed request says, by its cause, in Submit's words unless given others: nothing for a `401`. */
function failureWords(error: unknown, words: (cause: Cause) => string = msg.notSubmitted): string | undefined {
  const cause = classify(error);
  return cause === 'signedOut' ? undefined : words(cause);
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
