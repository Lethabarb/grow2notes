import { onlineManager, useMutation } from '@tanstack/react-query';
import { useEffect, useEffectEvent, useLayoutEffect, useRef } from 'react';
import {
  api,
  ApiError,
  classify,
  isNotEditable,
  prepareChanges,
  type Failure,
  type StartedBy,
} from '../../api/api.ts';
import { registerSaveFlush, warnAfter } from '../../api/session.ts';
import { limits } from '../../limits.ts';
import { useHiddenBySignIn } from '../../routes/hiddenBySignIn.ts';
import { noteBase, readDraft, type Draft, type DraftAnswer } from './noteQueries.ts';
import type { SaveIndicator } from './SaveIndicator.tsx';

// design.md §3.4: a save "about 2 seconds after the last change".
const quietMs = 2_000;
// autosave-status.md *Behaviour* 1: at most 5 s after the first unsaved change, so that typing or dictation with no
// pause still saves every few seconds (design.md §14 M2).
const longestMs = 5_000;
// autosave-status.md *Behaviour* 4: a save that failed for a transient reason goes again after 1, 2, 4 and 8 s, then
// every 10 s, for as long as the form is open and the person has acted on it within the session warning's 28 minutes
// (A66).
const firstRetryMs = 1_000;
const longestRetryMs = 10_000;
// design.md §5.6: browsers cap a keepalive request's body at 64 KiB, so the page-hidden save uses keepalive only for a
// body under 60,000 bytes.
const keepaliveBytes = 60_000;

// What a later try may get past (T10's Notes): no whole answer, as with no connection or api()'s 10 s timeout, a
// request timeout, too many requests, and a failure of the server or of what is in front of it.
const transientStatuses = new Set([0, 408, 429, 500, 502, 503, 504]);

/**
 * What the form holds, as a save reads it: the Guided notes text, the ticked goals, the picked common item groups and
 * their ticked items, the flag, and the reason as typed, which the form keeps while the box is unticked.
 */
export type DraftContent = {
  narrative: string;
  tickedGoalIds: string[];
  pickedGroupIds: string[];
  tickedCommonItemIds: string[];
  isFlagged: boolean;
  flagReason: string;
};

/** The note's content as `PUT {base}/draft` and `POST {base}/versions` alike send it (design.md §6.3). */
export type SentContent = {
  narrative: string;
  tickedGoalIds: string[];
  pickedGroupIds: string[];
  tickedCommonItemIds: string[];
  isFlagged: boolean;
  flagReason: string | null;
};

/**
 * What a save sends beside its identity: the version it is based on, 0 for a draft and the version being edited for a
 * pending edit, the content, and the lists' version until the note exists.
 */
type SavedFields = SentContent & { baseVersion: number; listsVersion?: string };

/** `PUT {base}/draft`'s body (design.md §6.3), as far as this form fills it. */
type DraftSaveBody = SavedFields & { clientId: string; seq: number };

/** A save's answer, the time the working copy was saved, in UTC. */
type DraftSaved = { savedAtUtc: string };

/** A save answered: the newest change it carried, the content it read, and when the working copy was saved. */
type Answered = { carried: number; content: DraftContent; savedAtUtc: string };

/**
 * What happens to the saves, as `flushNow()` and `checkSave()` wait for it: a save answered, with the content that it
 * read; "Not saved: no connection…" shown; a refusal that is not retried, or the form gone; or a dead end.
 */
type SaveEvent = { kind: 'saved'; content: DraftContent } | { kind: 'notSaved' | 'refused' | 'deadEnd' };

/**
 * This page load's saves of a note: their random ID, the number of the last one sent (design.md §5.6), and whether one
 * of them has been answered.
 */
type SaveIdentity = { clientId: string; seq: number; answered: boolean };

/**
 * Each note's identity of this page load's saves, by participant and date (autosave-status.md *Behaviour* 3), made at
 * the page's first save of the note. In memory for the whole page load, never per form, so that leaving the form and
 * coming back carries on from the last save number, rather than starting again at 1.
 */
const identities = new Map<string, SaveIdentity>();

/** The identity of the note at `key`, its number moved on to the next save's. */
function nextIdentity(key: string): SaveIdentity {
  let identity = identities.get(key);
  if (identity === undefined) {
    identity = { clientId: crypto.randomUUID(), seq: 0, answered: false };
    identities.set(key, identity);
  }

  identity.seq += 1;
  return identity;
}

/**
 * `text` as a save sends it, cut to `limit` characters, counted as UTF-16 code units, as .NET counts a string's
 * length; one fewer when the cut would split a surrogate pair, so that no half of a character is sent. The screen
 * keeps the whole text (note-form.md *Conflicts resolved* 1).
 */
export function capped(text: string, limit: number): string {
  if (text.length <= limit) {
    return text;
  }

  const last = text.charCodeAt(limit - 1);
  const splitsAPair = last >= 0xd800 && last <= 0xdbff;
  return text.slice(0, splitsAPair ? limit - 1 : limit);
}

/**
 * The reason as a save sends it (note-form.md *Autosave*): `null` while the box is unticked, so that hidden text never
 * reaches the server, and while the reason is blank; otherwise cut to the limit, as the draft's column holds no more,
 * while the screen keeps the whole of it, which Submit checks.
 */
function savedReason({ isFlagged, flagReason }: DraftContent): string | null {
  return isFlagged && flagReason.trim() !== '' ? capped(flagReason, limits.flagReason) : null;
}

/**
 * The form's content as a save sends it, and as Submit sends it too, built here once (T09's Notes): the text cut to
 * its limit, the ticks, the picks, the flag, and the reason as `savedReason()` gives it. Submit's checks have by then
 * refused a text or a reason over its limit, so that Submit sends them whole.
 */
export function sentContent(content: DraftContent): SentContent {
  return {
    narrative: capped(content.narrative, limits.guidedNotes),
    tickedGoalIds: content.tickedGoalIds,
    pickedGroupIds: content.pickedGroupIds,
    tickedCommonItemIds: content.tickedCommonItemIds,
    isFlagged: content.isFlagged,
    flagReason: savedReason(content),
  };
}

/**
 * A refusal of a save that no later save gets past, so that autosave stops (note-form.md *Conflicts and refusals*): a
 * worker's first save of a note for a day before today, as after midnight; a first save for an archived participant;
 * another user's draft, with who started it and when; a note submitted meanwhile, on another device or tab, which
 * Submit can meet too (T09's Notes); and, in edit mode, a newer version saved since the edit was based on, and an edit
 * the person may no longer make, such as a manager made a worker meanwhile (A72), which the form handles itself.
 */
export type DeadEnd =
  | { code: 'note.past_date_manager_only' }
  | { code: 'note.participant_archived' }
  | { code: 'note.version_conflict' }
  | { code: 'note.not_editable' }
  | { code: 'note.in_progress_by_other'; startedBy: StartedBy };

/** A dead end that the page's top bar words in a banner, as the form takes the refused edit and its conflict itself. */
export type BannerDeadEnd = Exclude<DeadEnd, { code: 'note.not_editable' }>;

/**
 * How Load the other version went: `'shown'`, the form showing the other version, or `'deadEnd'`, its read meeting a
 * dead end, after either of which the takeover banner has gone; why its read failed, the banner kept; or `undefined`
 * once the form has gone.
 */
export type LoadOutcome = 'shown' | 'deadEnd' | Failure | undefined;

type AutosaveOptions = {
  participantId: string;
  noteDate: string;
  /** The form as it opened. */
  opened: Draft;
  /** What the form holds now, read as each save is sent, never before. */
  read: () => DraftContent;
  /** Each new state of the save indicator, from the one that the form opens with. */
  onIndicator: (indicator: SaveIndicator) => void;
  /**
   * The form as `GET {base}/draft` gives it again after a first save found the lists changed, whose lists the form
   * shows in place of its own before it returns, so that the save sent next reads the ticks now on screen.
   */
  onListsChanged: (reloaded: Draft) => void;
  /** The other version, as Load the other version read it, which the form shows in place of its own. */
  onOtherVersion: (other: Draft) => void;
  /** A dead end, after which autosave sends nothing more, until Start again, in edit mode. */
  onDeadEnd: (deadEnd: DeadEnd) => void;
};

/** What the form tells autosave. */
export type Autosave = {
  /** A tick or an input: a change, numbered, which a save is then due to carry. */
  changed: () => void;
  /** A field lost focus: an unsaved change is sent at once, unless a retry, which will carry it, is due. */
  flush: () => void;
  /**
   * Whether leaving the form now would lose what autosave may still save: a change not yet saved, or the takeover
   * banner's answer; never after a dead end, after which nothing more is saved.
   */
  unsaved: () => boolean;
  /**
   * An unsaved change sent now, the wait for a retry cut short, for a leave, the session clock's 28:00 mark or a
   * sign-out that needs it saved first (T10's Notes): `true` at once with nothing unsaved, or after a dead end, and
   * otherwise once a save carrying the newest change is answered, or as a dead end comes; `false` as "Not saved:
   * no connection…" shows, while the retries go on, for a refusal that is not retried, a takeover included, and while
   * the takeover banner shows or the signed-out-in-place view hides the form, so that nothing waits on a save that is
   * retried for ever, or on the banner's answer.
   */
  flushNow: () => Promise<boolean>;
  /**
   * Submit's check save (autosave-status.md *Behaviour* 6): once any save out has settled, one `PUT` now, changed or
   * not, which sends the last change and meets any refusal that the note has had since its last save, retried as any
   * save is. The content that it read and saved, which carries any change made while the save out was being answered;
   * `undefined` as "Not saved: no connection…" shows, for a refusal or a dead end, or for a change made while it was
   * out, so that nothing opens, then or later.
   */
  checkSave: () => Promise<DraftContent | undefined>;
  /**
   * Pauses autosave as Submit's or Discard's request goes, its timers and retries cleared, once any save out, a
   * page-hidden one included, has settled: `false` if a dead end has stopped autosave, as when that save met one, after
   * which the note at this address may no longer be this draft, so that Discard sends nothing (T12's Notes).
   */
  stop: () => Promise<boolean>;
  /** Lets autosave go on after `stop()`, sending at once a change made meanwhile. */
  resume: () => void;
  /** Stops autosave at a dead end that Submit met, for as long as the form is open, as a save's would. */
  end: (deadEnd: DeadEnd) => void;
  /**
   * Keep the text on this screen, after a takeover: what the form holds now, the changes made meanwhile included, sent
   * at once as a change of its own under a new identity, numbered from 1, which takes the note back.
   */
  keep: () => void;
  /**
   * Load the other version, after a takeover: the draft read again, which `onOtherVersion` shows, after which the
   * note's saves start again under a new identity, with "Saved" at the other version's time.
   */
  load: () => Promise<LoadOutcome>;
  /**
   * Start again from the latest version, in edit mode, after a version conflict: the working copy rebased on
   * `baseVersion`, the version now current, what the form holds sent at once under the same identity with the next save
   * number, as a change of its own, which takes the stop away.
   */
  startAgain: (baseVersion: number) => void;
};

/** Where autosave is, kept from render to render. */
type Progress = {
  /** The changes made, the last one numbered so. */
  made: number;
  /** The newest change that an answered save carried. */
  saved: number;
  /**
   * The save out, if one is, sent or waiting for the browser to be online again, whose promise settles, saved or not,
   * as it ends.
   */
  out: Promise<void> | undefined;
  /** The saves sent as the page was hidden, beside the save out, that are still out. */
  hiddenOut: Set<Promise<void>>;
  /** A save fell due while one was out, and goes as that one settles. */
  due: boolean;
  /** The version that saves are based on, which every save sends: 0 for a draft, and Start again moves a pending edit's. */
  baseVersion: number;
  /** The note exists: the draft was saved before it opened, or a save from this form was answered. */
  started: boolean;
  /** The version of the lists that the form shows, which each save sends until the note exists (design.md §5.7). */
  listsVersion: string | undefined;
  /** A dead end stopped autosave, for as long as the form is open. */
  stopped: boolean;
  /** Submit's request is out, from `stop()` to `resume()`, so that nothing is sent meanwhile. */
  paused: boolean;
  /** Another page's save has taken the note over, until Keep or Load, so that nothing is sent meanwhile. */
  takenOver: boolean;
  /**
   * The signed-out-in-place view hides the form, from the session's end until the same person signs in again, so that
   * nothing is sent meanwhile.
   */
  signedOut: boolean;
  /** What the indicator says of a change not yet saved: "Saving…", or "Not saved: no connection…" (`failed`). */
  shown: 'saving' | 'failed' | undefined;
  /** The saves since the last answer that failed for a transient reason, which set the wait before the next. */
  failures: number;
  /** The form is on the page, so that a timer or an answer after it has gone sends and says nothing. */
  mounted: boolean;
  /** The save due 2 s after the last change. */
  quiet: number | undefined;
  /** The save due 5 s after the first unsaved change. */
  longest: number | undefined;
  /** The retry of a save that failed for a transient reason, which carries what the form holds as it goes. */
  retry: number | undefined;
  /**
   * When the person last acted on the form, by `Date.now()`: a change, a blur, the page shown again, their sign-in in
   * place, Keep or Submit; 0 before any. A retry goes only within the session warning's 28 minutes of it (A66).
   */
  attendedAt: number;
  /** What waits on the saves, each told what happens next, and giving `true` once that is its answer. */
  waiters: Set<(event: SaveEvent) => boolean>;
};

/**
 * The note form's autosave (note-form.md *Autosave*, autosave-status.md *Behaviour* 1 to 4), which saves the caller's
 * working copy with `PUT {base}/draft`, its text and reason cut to their limits: 2 s after the last change, 5 s at most
 * after the first unsaved one, and at once when a field loses focus with a change unsaved. One save is out at a time,
 * and one that falls due meanwhile goes as it settles, with what the form holds then, so that saves are combined, never
 * queued. The first save, which creates the note, sends the template's lists' version (design.md §5.7). Its
 * `409 note.lists_changed` reads the form again, for the form to show the lists as they are now, and sends the save
 * again at once with their version, "Saving…" shown throughout (autosave-status.md *Behaviour* 5). A dead end stops
 * autosave, with "Not saved".
 *
 * A save that fails for a transient reason goes again while the form is open, after the back-off's wait or a
 * `Retry-After`, each attempt reading the form and taking the next number as it is sent (T10's Notes), until 28 minutes
 * after the person last acted on the form, when the retries stop until they act again (A66): "Saving…" shows through
 * the first failure, then "Not saved: no connection…" from the second, or at once for a `Retry-After` longer than the
 * first retry's wait, or while the browser is offline, when the save waits for it to be online again, until a save
 * carrying the newest change is answered. Any other refusal is not retried, and leaves its change for the next change
 * or blur. As the page is hidden, the newest state goes at once, beside any save out, and again as the browser comes
 * online while it is hidden; as it is shown again, an unsaved change goes at once, the wait for a retry cut short. The
 * antiforgery token is fetched as the form opens, so that the page-hidden save is one request. Submit runs a check
 * save before its confirmation opens, and pauses autosave while its own request is out, as Discard does
 * (autosave-status.md *Behaviour* 6).
 *
 * A `409 draft.taken_over`, as another page has saved the note since, from a save or Submit's check save, pauses
 * autosave until the takeover banner is answered, with "Not saved" (autosave-status.md *Behaviour* 5): it is not a
 * dead end. Each change made meanwhile is counted and kept, but neither said nor sent. Keep sends what the form holds
 * then under a new identity, numbered from 1, and Load reads the other version for the form to show, after which the
 * next save does so too (*Behaviour* 3 and 9). To a page none of whose saves of the note has been answered, the `409`
 * comes of the numbers that its failed attempts used up, so its save goes again at once under a new identity,
 * numbered from 1, with no banner (T10's Notes).
 *
 * A pending edit is the same working copy on a submitted note, opened with a base version above 0, which every save
 * sends and the indicator reopens it "Saved" at (note-form.md *Kinds of note*). A `409 note.version_conflict` there is
 * a newer version saved since, and a `403 note.not_editable` an edit no longer allowed: each stops autosave as a dead
 * end, which the form answers itself, and Start again lifts the first by moving the base (A70, A72).
 *
 * The session's end pauses autosave too, while the signed-out-in-place view hides the form, from 30:00 idle or the
 * `401` that ends the session, its timers and retries cleared and every change kept and counted, but not sent
 * (session-timeout.md *The signed-out state*). As the same person's form shows again, a change unsaved goes at once,
 * under the note's next save number, which the server takes as this page's, with no takeover; another person's
 * sign-in loads the page afresh, the form still hidden, with nothing sent. While the form is open, the session clock
 * has a change waiting sent at its 28:00 mark in place of the warning, and Sign out has it saved before the logout,
 * each through `flushNow()` (T10's Notes).
 */
export function useAutosave({
  participantId,
  noteDate,
  opened,
  read,
  onIndicator,
  onListsChanged,
  onOtherVersion,
  onDeadEnd,
}: AutosaveOptions): Autosave {
  const noteKey = `${participantId}/${noteDate}`;
  const callbacks = useRef({ read, onIndicator, onListsChanged, onOtherVersion, onDeadEnd });
  useLayoutEffect(() => {
    callbacks.current = { read, onIndicator, onListsChanged, onOtherVersion, onDeadEnd };
  });

  const progress = useRef<Progress>({
    made: 0,
    saved: 0,
    out: undefined,
    hiddenOut: new Set(),
    due: false,
    baseVersion: opened.baseVersion,
    started: opened.isSaved,
    listsVersion: opened.listsVersion,
    stopped: false,
    paused: false,
    takenOver: false,
    signedOut: false,
    shown: undefined,
    failures: 0,
    mounted: false,
    quiet: undefined,
    longest: undefined,
    retry: undefined,
    attendedAt: 0,
    waiters: new Set(),
  });

  const { mutateAsync } = useMutation({
    mutationFn: () => put(progress.current, false),
    // Not the app's 'always': offline, a save waits, sending nothing, and goes as the browser is online again.
    networkMode: 'online',
    // Retried by autosave itself, whose wait a page shown again or a press cuts short, and `stop()` ends.
    retry: 0,
  });

  // Before the browser paints the form, so that the indicator holds its first words as the form appears.
  useLayoutEffect(() => {
    const state = progress.current;
    state.mounted = true;
    callbacks.current.onIndicator(
      opened.isSaved && opened.savedAtUtc !== undefined
        ? { kind: opened.baseVersion > 0 ? 'saved' : 'draft', savedAtUtc: opened.savedAtUtc }
        : { kind: 'none' },
    );
    return () => {
      state.mounted = false;
      state.due = false;
      stopTimers(state);
      stopRetry(state);
      tell(state, { kind: 'refused' });
    };
  }, [opened]);

  // The page-hidden save must go in one request, as the browser may freeze the page once it is hidden, so the
  // antiforgery token is fetched as the form opens, rather than as that save is sent (T10's Notes).
  useEffect(() => {
    prepareChanges();
  }, []);

  // Hidden, as on a switch to another app, the page may be frozen or closed next, so the newest state goes at once;
  // shown again, an unsaved change goes at once too (autosave-status.md *Behaviour* 1).
  const onVisibilityChange = useEffectEvent(() => {
    const state = progress.current;
    if (!canSend(state) || state.saved === state.made) {
      return;
    }

    if (document.visibilityState === 'hidden') {
      sendHidden(state);
    } else {
      state.attendedAt = Date.now();
      sendNow(state);
    }
  });

  // Offline, a save waits for the browser to be online again, so "Not saved: no connection…" shows at once for one out
  // or due (autosave-status.md *Behaviour* 4). Online again, TanStack sends a save that waited only once the page is
  // shown, so a hidden page, which may be closed unseen, sends the newest state at once, as it did as it was hidden.
  const onOnlineChange = useEffectEvent((online: boolean) => {
    const state = progress.current;
    if (!canSend(state)) {
      return;
    }

    if (!online) {
      if (state.saved < state.made || state.out !== undefined || state.retry !== undefined) {
        sayFailed(state);
      }
    } else if (document.visibilityState === 'hidden' && state.saved < state.made) {
      sendHidden(state);
    }
  });

  useEffect(() => {
    const listener = () => {
      onVisibilityChange();
    };
    document.addEventListener('visibilitychange', listener);
    const stopListening = onlineManager.subscribe((online) => {
      onOnlineChange(online);
    });
    return () => {
      document.removeEventListener('visibilitychange', listener);
      stopListening();
    };
  }, []);

  // In a layout effect, which runs before the shell's own, so that autosave has paused before the shell closes a Submit
  // confirmation as the signed-out-in-place view shows, which lets autosave go on.
  const hidden = useHiddenBySignIn();
  const onHiddenChange = useEffectEvent((isHidden: boolean) => {
    const state = progress.current;
    if (isHidden) {
      state.signedOut = true;
      stopTimers(state);
      stopRetry(state);
    } else if (state.signedOut) {
      state.signedOut = false;
      // The sign-in dropped the antiforgery token, which the page-hidden save needs held.
      prepareChanges();
      if (canSend(state) && state.saved < state.made) {
        state.attendedAt = Date.now();
        sayUnsaved(state);
        sendNow(state);
      }
    }
  });

  useLayoutEffect(() => {
    onHiddenChange(hidden);
  }, [hidden]);

  // For the session clock's 28:00 mark and Sign out, for as long as the form is open.
  const unsavedNow = useEffectEvent(() => unsaved());
  const flushNowEvent = useEffectEvent(() => flushNow());

  useEffect(() => registerSaveFlush({ unsaved: () => unsavedNow(), flushNow: () => flushNowEvent() }), []);

  function changed() {
    const state = progress.current;
    if (state.stopped) {
      return;
    }

    state.made += 1;
    state.attendedAt = Date.now();
    // Kept for Keep to send, while "Not saved" stays, or, hidden by the signed-out-in-place view, for the same person's
    // sign-in to send, the indicator as it was.
    if (state.takenOver || state.signedOut) {
      return;
    }

    sayUnsaved(state);
    window.clearTimeout(state.quiet);
    state.quiet = window.setTimeout(flush, quietMs);
    state.longest ??= window.setTimeout(flush, longestMs);
  }

  // "Saving…" for a change that a save is due to carry, or at once "Not saved: no connection…" while offline, when the
  // save waits for the browser to be online again.
  function sayUnsaved(state: Progress) {
    if (onlineManager.isOnline()) {
      saySaving(state);
    } else {
      sayFailed(state);
    }
  }

  function saySaving(state: Progress) {
    if (state.shown === undefined) {
      state.shown = 'saving';
      callbacks.current.onIndicator({ kind: 'saving' });
    }
  }

  // Once for each failure, so that it is said once, however many retries fail.
  function sayFailed(state: Progress) {
    if (state.shown !== 'failed') {
      state.shown = 'failed';
      callbacks.current.onIndicator({ kind: 'failed' });
      tell(state, { kind: 'notSaved' });
    }
  }

  function flush() {
    const state = progress.current;
    stopTimers(state);
    if (!canSend(state) || state.saved === state.made || state.retry !== undefined) {
      return;
    }

    if (state.out !== undefined) {
      state.due = true;
      return;
    }

    send(state);
  }

  // An unsaved change sent now, the wait for a retry cut short, or as the save out settles.
  function sendNow(state: Progress) {
    stopTimers(state);
    stopRetry(state);
    if (state.out !== undefined) {
      state.due = true;
      return;
    }

    send(state);
  }

  function send(state: Progress) {
    if (!onlineManager.isOnline()) {
      sayFailed(state);
    }

    state.out = attempt(state);
  }

  async function attempt(state: Progress): Promise<void> {
    try {
      const answer = await mutateAsync();
      if (answer !== undefined) {
        answered(state, answer);
      }
    } catch (error) {
      await failed(state, error);
    } finally {
      state.out = undefined;
      if (state.due) {
        state.due = false;
        flush();
      }
    }
  }

  // The form as it is now, under the note's next save number, while autosave may send; otherwise nothing, as for a
  // save that waited for the browser to be online while Submit's request went out or the form was left.
  async function put(state: Progress, hidden: boolean): Promise<Answered | undefined> {
    if (!canSend(state)) {
      return undefined;
    }

    const carried = state.made;
    const content = callbacks.current.read();
    const fields: SavedFields = {
      baseVersion: state.baseVersion,
      ...(state.started || state.listsVersion === undefined ? {} : { listsVersion: state.listsVersion }),
      ...sentContent(content),
    };
    try {
      return { carried, content, savedAtUtc: await saveAs(fields, hidden) };
    } catch (error) {
      // A takeover's 409 to a page that has had none of its saves of the note answered comes of the numbers that its
      // failed attempts used up, as when its first never left the browser: this page has not saved the note, so the
      // save goes again at once as a new page's first, which takes the note over (design.md §5.6; T10's Notes). A
      // page-hidden save leaves it to the regular saves, as it leaves its other failures.
      const firstSave = identities.get(noteKey)?.answered === false;
      if (hidden || !firstSave || !(error instanceof ApiError) || error.code !== 'draft.taken_over') {
        throw error;
      }

      identities.delete(noteKey);
      return { carried, content, savedAtUtc: await saveAs(fields, false) };
    }
  }

  // One `PUT` of `fields` under the note's next identity, which its answer marks as answered. The page-hidden save
  // goes with keepalive while its body fits under the browser's cap.
  async function saveAs(fields: SavedFields, hidden: boolean): Promise<string> {
    const identity = nextIdentity(noteKey);
    const body: DraftSaveBody = { clientId: identity.clientId, seq: identity.seq, ...fields };
    const keepalive = hidden ? new TextEncoder().encode(JSON.stringify(body)).length < keepaliveBytes : undefined;
    const { savedAtUtc } = await api<DraftSaved>(`${noteBase(participantId, noteDate)}/draft`, {
      method: 'PUT',
      body,
      keepalive,
    });
    identity.answered = true;
    return savedAtUtc;
  }

  // Its answer counts as any save's; its failure is left to the retries of the saves that the timers or the save out
  // send, as the browser may run nothing more of the page.
  function sendHidden(state: Progress) {
    const sent: Promise<void> = put(state, true)
      .then(
        (answer) => {
          if (answer !== undefined) {
            answered(state, answer);
          }
        },
        () => {},
      )
      .finally(() => {
        state.hiddenOut.delete(sent);
      });
    state.hiddenOut.add(sent);
  }

  // A save answered, a page-hidden one alike, which may be older than one answered already: "Saved 9:44 am" once the
  // newest change is saved, which ends a failure, but not again for an older save, whose time would put it back; or
  // "Saving…" again after "Not saved…", as saves go through again. Nothing is said for one answered after a takeover,
  // as the note is another page's until the banner is answered.
  function answered(state: Progress, { carried, content, savedAtUtc }: Answered) {
    const newer = carried > state.saved;
    state.started = true;
    state.saved = Math.max(state.saved, carried);
    state.failures = 0;
    const shows = state.mounted && !state.stopped && !state.takenOver;
    if (state.saved === state.made) {
      stopRetry(state);
      if (shows && (newer || state.shown !== undefined)) {
        state.shown = undefined;
        callbacks.current.onIndicator({ kind: 'saved', savedAtUtc });
      }
    } else if (shows && state.shown === 'failed') {
      state.shown = 'saving';
      callbacks.current.onIndicator({ kind: 'saving' });
    }

    tell(state, { kind: 'saved', content });
  }

  async function failed(state: Progress, error: unknown) {
    if (!state.mounted) {
      return;
    }

    if (isTransient(error)) {
      retryLater(state, error);
      return;
    }

    state.failures = 0;
    if (error instanceof ApiError) {
      await refused(state, error);
    } else {
      tell(state, { kind: 'refused' });
    }
  }

  // After the back-off's wait, or the server's Retry-After in its place, with "Not saved: no connection…" once the
  // first retry has failed too, so that one dropped request says nothing; or at once while offline, or for a wait
  // longer than the first retry's, so that "Saving…", and a press of Submit waiting on it, never last through it
  // (note-form.md *Pressing Submit note* 4).
  function retryLater(state: Progress, error: ApiError) {
    state.failures += 1;
    if (!canSend(state)) {
      return;
    }

    const wait = error.retryAfterMs ?? Math.min(firstRetryMs * 2 ** (state.failures - 1), longestRetryMs);
    if (state.failures > 1 || wait > firstRetryMs || !onlineManager.isOnline()) {
      sayFailed(state);
    }

    state.retry = window.setTimeout(() => {
      state.retry = undefined;
      // Nobody has acted on the form for as long as the session warning waits, so the retries stop until the next
      // change, blur, return to the page or sign-in, "Not saved: …" kept: a failing server's answers would otherwise
      // keep an unattended session alive, never letting the clock warn and end it (A66).
      if (Date.now() - state.attendedAt >= warnAfter) {
        sayFailed(state);
        return;
      }

      send(state);
    }, wait);
  }

  // Until no save is out, a page-hidden one included, taking the place of the save due to follow one, as what comes
  // next reads the form anew.
  async function settled(state: Progress) {
    while (state.out !== undefined || state.hiddenOut.size > 0) {
      state.due = false;
      await Promise.all([state.out, ...state.hiddenOut]);
    }
  }

  function flushNow(): Promise<boolean> {
    if (!unsaved()) {
      return Promise.resolve(true);
    }

    const state = progress.current;
    if (!mayWait(state)) {
      return Promise.resolve(false);
    }

    const newest = state.made;
    const flushed = new Promise<boolean>((resolve) => {
      state.waiters.add((event) => {
        if (event.kind === 'saved' && state.saved < newest) {
          return false;
        }

        resolve(event.kind === 'saved' || event.kind === 'deadEnd');
        return true;
      });
    });
    sendNow(state);
    return flushed;
  }

  async function checkSave(): Promise<DraftContent | undefined> {
    const state = progress.current;
    if (!mayWait(state)) {
      return undefined;
    }

    state.attendedAt = Date.now();
    stopTimers(state);
    stopRetry(state);
    saySaving(state);
    await settled(state);
    if (!mayWait(state)) {
      return undefined;
    }

    // The save out may have failed, and this check save takes the place of its retry.
    stopRetry(state);
    const sentWith = state.made;
    const checked = new Promise<DraftContent | undefined>((resolve) => {
      state.waiters.add((event) => {
        resolve(event.kind === 'saved' && state.made === sentWith ? event.content : undefined);
        return true;
      });
    });
    send(state);
    return checked;
  }

  async function stop() {
    const state = progress.current;
    state.paused = true;
    stopTimers(state);
    stopRetry(state);
    await settled(state);
    return !state.stopped;
  }

  function resume() {
    progress.current.paused = false;
    flush();
  }

  // A refusal that autosave answers itself (autosave-status.md *Behaviour* 5). Any other leaves the change unsaved,
  // with "Saving…", for the next change or blur to send.
  async function refused(state: Progress, error: ApiError) {
    if (error.code === 'note.lists_changed') {
      await reloadLists(state);
      return;
    }

    if (error.code === 'draft.taken_over') {
      takeOver(state);
      return;
    }

    const deadEnd = deadEndOf(error);
    if (deadEnd === undefined) {
      tell(state, { kind: 'refused' });
    } else {
      stopAt(state, deadEnd);
    }
  }

  // Outside the draft's query, which the form read once as it opened, so that the form's load states never show
  // again. A reload that fails fails the save: retried for a transient reason, the retry meeting the same refusal and
  // reloading, and otherwise left for the next change or blur, whose save does.
  async function reloadLists(state: Progress) {
    let answer: DraftAnswer;
    try {
      answer = await readDraft(participantId, noteDate);
    } catch (error) {
      if (isTransient(error)) {
        retryLater(state, error);
      } else {
        tell(state, { kind: 'refused' });
      }

      return;
    }

    if (!state.mounted) {
      return;
    }

    if ('startedBy' in answer) {
      stopAt(state, { code: 'note.in_progress_by_other', startedBy: answer.startedBy });
      return;
    }

    // A draft of the caller's own, if another of their pages started the note meanwhile, takes later saves.
    state.started = answer.draft.isSaved;
    state.listsVersion = answer.draft.listsVersion;
    callbacks.current.onListsChanged(answer.draft);
    state.due = true;
  }

  function stopAt(state: Progress, deadEnd: DeadEnd) {
    state.stopped = true;
    state.due = false;
    state.shown = undefined;
    stopTimers(state);
    stopRetry(state);
    callbacks.current.onIndicator({ kind: 'notSaved' });
    callbacks.current.onDeadEnd(deadEnd);
    tell(state, { kind: 'deadEnd' });
  }

  // Another page saved the note since this one last did (design.md §5.6), so a later save would overwrite its words
  // unasked: nothing is sent until the takeover banner is answered.
  function takeOver(state: Progress) {
    state.takenOver = true;
    state.due = false;
    state.shown = undefined;
    stopTimers(state);
    stopRetry(state);
    callbacks.current.onIndicator({ kind: 'takenOver' });
    tell(state, { kind: 'refused' });
  }

  function keep() {
    const state = progress.current;
    identities.delete(noteKey);
    state.takenOver = false;
    // What the form holds differs from what the server does, the other version, as a change does.
    state.made += 1;
    state.attendedAt = Date.now();
    sayUnsaved(state);
    sendNow(state);
  }

  // Outside the draft's query, as the lists-changed reload reads it (T10's Notes). On a draft's page, a `404` (the
  // note discarded and its day gone, A65) and an answer that is a pending edit's, with a base above 0, the note
  // submitted meanwhile, are the version-conflict dead end; on an edit page, the other version is the pending edit as
  // it is now saved, whose base is adopted, as Load means showing the other version. Another user's draft, which the
  // note can be only once discarded and started again, is the in-progress dead end; and a note discarded meanwhile
  // loads as a new note's form.
  async function load(): Promise<LoadOutcome> {
    const state = progress.current;
    let answer: DraftAnswer;
    try {
      answer = await readDraft(participantId, noteDate);
    } catch (error) {
      if (!state.mounted) {
        return undefined;
      }

      if (error instanceof ApiError && error.status === 404) {
        stopAt(state, { code: 'note.version_conflict' });
        return 'deadEnd';
      }

      if (isNotEditable(error)) {
        stopAt(state, { code: 'note.not_editable' });
        return 'deadEnd';
      }

      return classify(error);
    }

    if (!state.mounted) {
      return undefined;
    }

    if ('startedBy' in answer) {
      stopAt(state, { code: 'note.in_progress_by_other', startedBy: answer.startedBy });
      return 'deadEnd';
    }

    const { draft } = answer;
    if (opened.baseVersion === 0 && draft.baseVersion > 0) {
      stopAt(state, { code: 'note.version_conflict' });
      return 'deadEnd';
    }

    identities.delete(noteKey);
    state.takenOver = false;
    state.baseVersion = draft.baseVersion;
    state.started = draft.isSaved;
    state.listsVersion = draft.listsVersion;
    state.saved = state.made;
    callbacks.current.onOtherVersion(draft);
    callbacks.current.onIndicator(
      draft.savedAtUtc === undefined ? { kind: 'none' } : { kind: 'saved', savedAtUtc: draft.savedAtUtc },
    );
    return 'shown';
  }

  // The conflict's way out (note-form.md component 6): the base moves to the version now current, and what is on screen
  // is sent as a change, under the same identity, so that the server rebases the working copy without a takeover. It
  // also lifts the pause of the Save changes that met the conflict, whose request has ended.
  function startAgain(baseVersion: number) {
    const state = progress.current;
    state.baseVersion = baseVersion;
    state.stopped = false;
    state.paused = false;
    state.made += 1;
    state.attendedAt = Date.now();
    sayUnsaved(state);
    sendNow(state);
  }

  function unsaved(): boolean {
    const state = progress.current;
    return !state.stopped && (state.saved < state.made || state.takenOver);
  }

  return {
    changed,
    flush: () => {
      progress.current.attendedAt = Date.now();
      flush();
    },
    unsaved,
    flushNow,
    checkSave,
    stop,
    resume,
    end: (deadEnd) => {
      stopAt(progress.current, deadEnd);
    },
    keep,
    load,
    startAgain,
  };
}

/**
 * Whether autosave may send: the form is open, with no dead end, Submit's request is not out, no takeover waits for
 * its banner's answer, and the signed-out-in-place view does not hide the form.
 */
function canSend(state: Progress): boolean {
  return state.mounted && !state.stopped && !state.paused && !state.takenOver && !state.signedOut;
}

/**
 * Whether a flush or a check save may send and wait for its answer: autosave may send, and no failure is being retried,
 * as "Not saved: no connection…" already answers them.
 */
function mayWait(state: Progress): boolean {
  return canSend(state) && state.shown !== 'failed';
}

/** Whether `error` is a failure that a later try may get past. */
function isTransient(error: unknown): error is ApiError {
  return error instanceof ApiError && transientStatuses.has(error.status);
}

/** Tells each waiter what happened, letting go of each that has its answer. */
function tell(state: Progress, event: SaveEvent) {
  for (const waiter of [...state.waiters]) {
    if (waiter(event)) {
      state.waiters.delete(waiter);
    }
  }
}

/** The dead end that `error` is, if it is one. */
function deadEndOf(error: ApiError): DeadEnd | undefined {
  switch (error.code) {
    case 'note.past_date_manager_only':
    case 'note.participant_archived':
    case 'note.version_conflict':
    case 'note.not_editable':
      return { code: error.code };
    case 'note.in_progress_by_other':
      return error.startedBy === undefined ? undefined : { code: error.code, startedBy: error.startedBy };
    default:
      return undefined;
  }
}

function stopTimers(state: Progress) {
  window.clearTimeout(state.quiet);
  window.clearTimeout(state.longest);
  state.quiet = undefined;
  state.longest = undefined;
}

function stopRetry(state: Progress) {
  window.clearTimeout(state.retry);
  state.retry = undefined;
}
