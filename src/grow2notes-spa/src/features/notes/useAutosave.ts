import { useMutation } from '@tanstack/react-query';
import { useLayoutEffect, useRef } from 'react';
import { api } from '../../api/api.ts';
import { limits } from '../../limits.ts';
import { noteBase, type Draft } from './noteQueries.ts';
import type { SaveIndicator } from './SaveIndicator.tsx';

// design.md §3.4: a save "about 2 seconds after the last change".
const quietMs = 2_000;
// autosave-status.md *Behaviour* 1: at most 5 s after the first unsaved change, so that typing or dictation with no
// pause still saves every few seconds (design.md §14 M2).
const longestMs = 5_000;

/** What the form holds, as a save sends it. */
export type DraftContent = { narrative: string; tickedGoalIds: string[] };

/** `PUT {base}/draft`'s body (design.md §6.3), as far as this form fills it. */
type DraftSaveBody = DraftContent & { clientId: string; seq: number; listsVersion?: string };

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

type AutosaveOptions = {
  participantId: string;
  noteDate: string;
  /** The form as it opened. */
  opened: Draft;
  /** What the form holds now, read as each save is sent, never before. */
  read: () => DraftContent;
  /** Each new state of the save indicator, from the one that the form opens with. */
  onIndicator: (indicator: SaveIndicator) => void;
};

/** What the form tells autosave. */
export type Autosave = {
  /** A tick or an input: a change, numbered, which a save is then due to carry. */
  changed: () => void;
  /** A field lost focus: an unsaved change is sent at once. */
  flush: () => void;
};

/** Where autosave is, kept from render to render. */
type Progress = {
  /** The changes made, the last one numbered so. */
  made: number;
  /** The newest change that an answered save carried. */
  saved: number;
  /** A save is out. */
  sending: boolean;
  /** A save fell due while one was out, and goes as that one settles. */
  due: boolean;
  /** The note exists: the draft was saved before it opened, or a save from this form was answered. */
  started: boolean;
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
 * working copy with `PUT {base}/draft`: 2 s after the last change, 5 s at most after the first unsaved one, and at once
 * when a field loses focus with a change unsaved. One save is out at a time, and one that falls due meanwhile goes as
 * it settles, with what the form holds then, so that saves are combined, never queued. The first save, which creates
 * the note, sends the template's lists' version (design.md §5.7). A save that fails leaves its change unsaved, and
 * "Saving…" shown, until the next change or a blur sends it: the retries, "Not saved", the page-hidden save and the
 * rules for leaving are T10's (T08's Notes).
 */
export function useAutosave({ participantId, noteDate, opened, read, onIndicator }: AutosaveOptions): Autosave {
  const callbacks = useRef({ read, onIndicator });
  useLayoutEffect(() => {
    callbacks.current = { read, onIndicator };
  });

  const progress = useRef<Progress>({
    made: 0,
    saved: 0,
    sending: false,
    due: false,
    started: opened.isSaved,
    saying: false,
    mounted: false,
    quiet: undefined,
    longest: undefined,
  });

  const { mutateAsync } = useMutation({
    mutationFn: ({ content, started }: { content: DraftContent; started: boolean }) => {
      const body: DraftSaveBody = {
        ...nextIdentity(`${participantId}/${noteDate}`),
        ...(started ? {} : { listsVersion: opened.listsVersion }),
        narrative: capped(content.narrative, limits.guidedNotes),
        tickedGoalIds: content.tickedGoalIds,
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
    state.made += 1;
    if (!state.saying) {
      state.saying = true;
      callbacks.current.onIndicator({ kind: 'saving' });
    }

    window.clearTimeout(state.quiet);
    state.quiet = window.setTimeout(flush, quietMs);
    state.longest ??= window.setTimeout(flush, longestMs);
  }

  function flush() {
    const state = progress.current;
    stopTimers(state);
    if (!state.mounted || state.saved === state.made) {
      return;
    }

    if (state.sending) {
      state.due = true;
      return;
    }

    void send(state);
  }

  async function send(state: Progress) {
    state.sending = true;
    const carried = state.made;
    try {
      const { savedAtUtc } = await mutateAsync({ content: callbacks.current.read(), started: state.started });
      state.started = true;
      state.saved = carried;
      if (state.mounted && state.saved === state.made) {
        state.saying = false;
        callbacks.current.onIndicator({ kind: 'saved', savedAtUtc });
      }
    } catch {
      // The change stays unsaved, with "Saving…", for the next change or blur to send.
    } finally {
      state.sending = false;
      if (state.due) {
        state.due = false;
        flush();
      }
    }
  }

  return { changed, flush };
}

function stopTimers(state: Progress) {
  window.clearTimeout(state.quiet);
  window.clearTimeout(state.longest);
  state.quiet = undefined;
  state.longest = undefined;
}
