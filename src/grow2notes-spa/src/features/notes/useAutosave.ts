import { useMutation } from '@tanstack/react-query';
import { useLayoutEffect, useRef } from 'react';
import { api, ApiError, type StartedBy } from '../../api/api.ts';
import { limits } from '../../limits.ts';
import { noteBase, readDraft, type Draft, type DraftAnswer } from './noteQueries.ts';
import type { SaveIndicator } from './SaveIndicator.tsx';

// design.md §3.4: a save "about 2 seconds after the last change".
const quietMs = 2_000;
// autosave-status.md *Behaviour* 1: at most 5 s after the first unsaved change, so that typing or dictation with no
// pause still saves every few seconds (design.md §14 M2).
const longestMs = 5_000;

/**
 * What the form holds, as a save reads it: the Guided notes text, the ticked goals, the flag, and the reason as typed,
 * which the form keeps while the box is unticked.
 */
export type DraftContent = { narrative: string; tickedGoalIds: string[]; isFlagged: boolean; flagReason: string };

/** The note's content as `PUT {base}/draft` and `POST {base}/versions` alike send it (design.md §6.3). */
export type SentContent = { narrative: string; tickedGoalIds: string[]; isFlagged: boolean; flagReason: string | null };

/** `PUT {base}/draft`'s body (design.md §6.3), as far as this form fills it. */
type DraftSaveBody = SentContent & { clientId: string; seq: number; listsVersion?: string };

/** A save's answer, the time the working copy was saved, in UTC. */
type DraftSaved = { savedAtUtc: string };

/** This page load's saves of a note: their random ID, and the number of the last one sent (design.md §5.6). */
type SaveIdentity = { clientId: string; seq: number };

/**
 * Each note's identity of this page load's saves, by participant and date (autosave-status.md *Behaviour* 3), made at
 * the page's first save of the note. In memory for the whole page load, never per form, so that leaving the form and
 * coming back carries on from the last save number, rather than starting again at 1.
 */
const identities = new Map<string, SaveIdentity>();

/** The identity of the next save of the note at `key`, with the next number. */
function nextIdentity(key: string): SaveIdentity {
  let identity = identities.get(key);
  if (identity === undefined) {
    identity = { clientId: crypto.randomUUID(), seq: 0 };
    identities.set(key, identity);
  }

  identity.seq += 1;
  return { ...identity };
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
 * its limit, the ticks, the flag, and the reason as `savedReason()` gives it. Submit's checks have by then refused a
 * text or a reason over its limit, so that Submit sends them whole.
 */
export function sentContent(content: DraftContent): SentContent {
  return {
    narrative: capped(content.narrative, limits.guidedNotes),
    tickedGoalIds: content.tickedGoalIds,
    isFlagged: content.isFlagged,
    flagReason: savedReason(content),
  };
}

/**
 * A refusal of a save that no later save gets past, so that autosave stops (note-form.md *Conflicts and refusals*): a
 * worker's first save of a note for a day before today, as after midnight; a first save for an archived participant;
 * another user's draft, with who started it and when; and a note submitted meanwhile, on another device or tab, which
 * Submit can meet too (T09's Notes).
 */
export type DeadEnd =
  | { code: 'note.past_date_manager_only' | 'note.participant_archived' | 'note.version_conflict' }
  | { code: 'note.in_progress_by_other'; startedBy: StartedBy };

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
  /** A dead end, after which autosave sends nothing more. */
  onDeadEnd: (deadEnd: DeadEnd) => void;
};

/** What the form tells autosave. */
export type Autosave = {
  /** A tick or an input: a change, numbered, which a save is then due to carry. */
  changed: () => void;
  /** A field lost focus: an unsaved change is sent at once. */
  flush: () => void;
  /**
   * Submit's check save (autosave-status.md *Behaviour* 6): once any save out has settled, one `PUT` now, changed or
   * not, which sends the last change and meets any refusal that the note has had since its last save. The content that
   * it read and saved, which carries any change made while the save out was being answered; `undefined` for a dead end,
   * a failure, or a change made while it was out, so that nothing opens.
   */
  checkSave: () => Promise<DraftContent | undefined>;
  /** Pauses autosave as Submit's request goes, its timers cleared, once any save out has settled. */
  stop: () => Promise<void>;
  /** Lets autosave go on after `stop()`, sending at once a change made meanwhile. */
  resume: () => void;
  /** Stops autosave at a dead end that Submit met, for as long as the form is open, as a save's would. */
  end: (deadEnd: DeadEnd) => void;
};

/** Where autosave is, kept from render to render. */
type Progress = {
  /** The changes made, the last one numbered so. */
  made: number;
  /** The newest change that an answered save carried. */
  saved: number;
  /** The save out, if one is, whose promise settles, saved or not, as it ends. */
  out: Promise<DraftContent | undefined> | undefined;
  /** A save fell due while one was out, and goes as that one settles. */
  due: boolean;
  /** The note exists: the draft was saved before it opened, or a save from this form was answered. */
  started: boolean;
  /** The version of the lists that the form shows, which each save sends until the note exists (design.md §5.7). */
  listsVersion: string | undefined;
  /** A dead end stopped autosave, for as long as the form is open. */
  stopped: boolean;
  /** Submit's request is out, from `stop()` to `resume()`, so that nothing is sent meanwhile. */
  paused: boolean;
  /** The indicator says "Saving…". */
  saying: boolean;
  /** The form is on the page, so that a timer or an answer after it has gone sends and says nothing. */
  mounted: boolean;
  /** The save due 2 s after the last change. */
  quiet: number | undefined;
  /** The save due 5 s after the first unsaved change. */
  longest: number | undefined;
};

/**
 * The note form's autosave (note-form.md *Autosave*, autosave-status.md *Behaviour* 1 to 3), which saves the caller's
 * working copy with `PUT {base}/draft`, its text and reason cut to their limits: 2 s after the last change, 5 s at most
 * after the first unsaved one, and at once when a field loses focus with a change unsaved. One save is out at a time,
 * and one that falls due meanwhile goes as it settles, with what the form holds then, so that saves are combined, never
 * queued. The first save, which creates the note, sends the template's lists' version (design.md §5.7). Its
 * `409 note.lists_changed` reads the form again, for the form to show the lists as they are now, and sends the save
 * again at once with their version, "Saving…" shown throughout (autosave-status.md *Behaviour* 5). A dead end stops
 * autosave, with "Not saved". A save that fails otherwise leaves its change unsaved, and "Saving…" shown, until the
 * next change or a blur sends it: the retries, "Not saved: no connection…", the page-hidden save and the rules for
 * leaving are T10's (T08's Notes). Submit runs a check save before its confirmation opens, and pauses autosave while
 * its own request is out (autosave-status.md *Behaviour* 6).
 */
export function useAutosave({
  participantId,
  noteDate,
  opened,
  read,
  onIndicator,
  onListsChanged,
  onDeadEnd,
}: AutosaveOptions): Autosave {
  const callbacks = useRef({ read, onIndicator, onListsChanged, onDeadEnd });
  useLayoutEffect(() => {
    callbacks.current = { read, onIndicator, onListsChanged, onDeadEnd };
  });

  const progress = useRef<Progress>({
    made: 0,
    saved: 0,
    out: undefined,
    due: false,
    started: opened.isSaved,
    listsVersion: opened.listsVersion,
    stopped: false,
    paused: false,
    saying: false,
    mounted: false,
    quiet: undefined,
    longest: undefined,
  });

  const { mutateAsync } = useMutation({
    mutationFn: ({ content, listsVersion }: { content: DraftContent; listsVersion: string | undefined }) => {
      const body: DraftSaveBody = {
        ...nextIdentity(`${participantId}/${noteDate}`),
        ...(listsVersion === undefined ? {} : { listsVersion }),
        ...sentContent(content),
      };
      return api<DraftSaved>(`${noteBase(participantId, noteDate)}/draft`, { method: 'PUT', body });
    },
    // A failed save waits for the next change or blur, which sends the newest state; T10 adds autosave's retries.
    retry: 0,
  });

  // Before the browser paints the form, so that the indicator holds its first words as the form appears.
  useLayoutEffect(() => {
    const state = progress.current;
    state.mounted = true;
    callbacks.current.onIndicator(
      opened.isSaved && opened.savedAtUtc !== undefined
        ? { kind: 'draft', savedAtUtc: opened.savedAtUtc }
        : { kind: 'none' },
    );
    return () => {
      state.mounted = false;
      state.due = false;
      stopTimers(state);
    };
  }, [opened]);

  function changed() {
    const state = progress.current;
    if (state.stopped) {
      return;
    }

    state.made += 1;
    saySaving(state);
    window.clearTimeout(state.quiet);
    state.quiet = window.setTimeout(flush, quietMs);
    state.longest ??= window.setTimeout(flush, longestMs);
  }

  function saySaving(state: Progress) {
    if (!state.saying) {
      state.saying = true;
      callbacks.current.onIndicator({ kind: 'saving' });
    }
  }

  function flush() {
    const state = progress.current;
    stopTimers(state);
    if (!state.mounted || state.stopped || state.paused || state.saved === state.made) {
      return;
    }

    if (state.out !== undefined) {
      state.due = true;
      return;
    }

    void send(state);
  }

  function send(state: Progress): Promise<DraftContent | undefined> {
    state.out = save(state);
    return state.out;
  }

  // The form's content as the save read it, once saved; `undefined` if it was not.
  async function save(state: Progress): Promise<DraftContent | undefined> {
    const carried = state.made;
    try {
      const content = callbacks.current.read();
      const { savedAtUtc } = await mutateAsync({
        content,
        listsVersion: state.started ? undefined : state.listsVersion,
      });
      state.started = true;
      state.saved = carried;
      if (state.mounted && state.saved === state.made) {
        state.saying = false;
        callbacks.current.onIndicator({ kind: 'saved', savedAtUtc });
      }

      return content;
    } catch (error) {
      if (state.mounted && error instanceof ApiError) {
        await refused(state, error);
      }

      return undefined;
    } finally {
      state.out = undefined;
      if (state.due) {
        state.due = false;
        flush();
      }
    }
  }

  // Until no save is out, taking the place of the save due to follow one, as what comes next reads the form anew.
  async function settled(state: Progress) {
    while (state.out !== undefined) {
      state.due = false;
      await state.out;
    }
  }

  async function checkSave(): Promise<DraftContent | undefined> {
    const state = progress.current;
    if (!state.mounted || state.stopped) {
      return undefined;
    }

    stopTimers(state);
    saySaving(state);
    await settled(state);
    if (!state.mounted || state.stopped) {
      return undefined;
    }

    const content = await send(state);
    return state.saved === state.made ? content : undefined;
  }

  async function stop() {
    const state = progress.current;
    state.paused = true;
    stopTimers(state);
    await settled(state);
  }

  function resume() {
    progress.current.paused = false;
    flush();
  }

  // A refusal that autosave answers itself (autosave-status.md *Behaviour* 5). Any other failure leaves the change
  // unsaved, with "Saving…", for the next change or blur to send.
  async function refused(state: Progress, error: ApiError) {
    if (error.code === 'note.lists_changed') {
      await reloadLists(state);
      return;
    }

    const deadEnd = deadEndOf(error);
    if (deadEnd !== undefined) {
      stopAt(state, deadEnd);
    }
  }

  // Outside the draft's query, which the form read once as it opened, so that the form's load states never show
  // again. A reload that fails leaves the change unsaved, so that its next save meets the same refusal and reloads.
  async function reloadLists(state: Progress) {
    let answer: DraftAnswer;
    try {
      answer = await readDraft(participantId, noteDate);
    } catch {
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
    stopTimers(state);
    callbacks.current.onIndicator({ kind: 'notSaved' });
    callbacks.current.onDeadEnd(deadEnd);
  }

  return {
    changed,
    flush,
    checkSave,
    stop,
    resume,
    end: (deadEnd) => {
      stopAt(progress.current, deadEnd);
    },
  };
}

/** The dead end that `error` is, if it is one. */
function deadEndOf(error: ApiError): DeadEnd | undefined {
  switch (error.code) {
    case 'note.past_date_manager_only':
    case 'note.participant_archived':
    case 'note.version_conflict':
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
