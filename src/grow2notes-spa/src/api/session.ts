// The session-ended event (session-timeout.md): `api()` raises it, and nothing else does, when a request is answered
// `401` because the person's session has ended, so that the shell can show sign-in.
const endedListeners = new Set<() => void>();

/** Calls `listener` each time the session-ended event is raised, and returns the function that stops it. */
export function onSessionEnded(listener: () => void): () => void {
  endedListeners.add(listener);
  return () => {
    endedListeners.delete(listener);
  };
}

/**
 * For `api()` only, which raises the event before it throws, so the shell can act before the caller's own handler.
 * It ends the session clock too.
 */
export function sessionEnded(): void {
  if (clock !== undefined) {
    clock.ended = true;
    update(clock);
  }

  for (const listener of endedListeners) {
    listener();
  }
}

// How many sign-ins the server has answered `200` since the page loaded, each with a new session cookie. A request sent
// before the newest went with the cookie from before, so its `401` says nothing of the session now held (S00.04.08's
// Notes); and the signed-out-in-place view tells by it whether the `Me` that closes it came from a sign-in.
let signIns = 0;

/** For `api()` only: a sign-in, or a setup, has been answered `200`, with a new session cookie. */
export function signedIn(): void {
  signIns += 1;
}

/** How many sign-ins the server has answered `200` since the page loaded. */
export function signInCount(): number {
  return signIns;
}

// The session clock (session-timeout.md *The session clock*) predicts when the server will end the session for being
// idle: 30 minutes after the newest request that it accepted, the session cookie's `ExpireTimeSpan` (design.md §8.4),
// with the warning from 28 minutes (A24). It never sends a request, which would extend the session, though at 28:00 it
// has autosave send a note form's change waiting, in place of the warning; and the server's answer always wins over it.

/** When the warning opens, in idle milliseconds, at which autosave's unattended retries stop too (A66). */
export const warnAfter = 28 * 60_000;
const endAfter = 30 * 60_000;

// The idle times at which the phase moves on: the warning, its countdown's steps (app-shell.md component 6), the end.
const marks = [warnAfter, endAfter - 60_000, endAfter - 40_000, endAfter - 20_000, endAfter];

// Each phase, after as many marks as its index.
const phases = ['active', 'warn-2m', 'warn-1m', 'warn-40s', 'warn-20s', 'expired'] as const;

/** Where the session stands: active, at one of the warning's four countdown steps, or expired. */
export type Phase = (typeof phases)[number];

/** One of the warning's four countdown steps. */
export type WarningPhase = Exclude<Phase, 'active' | 'expired'>;

/** Whether `phase` is one of the warning's countdown steps, in which the warning shows. */
export function isWarning(phase: Phase): phase is WarningPhase {
  return phase !== 'active' && phase !== 'expired';
}

type Clock = {
  phase: Phase;
  /** The send time of the newest request that the server accepted, or the start's time. */
  lastAccepted: number;
  /** When the latest answer to a request that the server accepted came, or the start's time. */
  lastAnswered: number;
  /** Whether a `401` has ended the session since. */
  ended: boolean;
  /** How many promises are holding the phase as it is. */
  holds: number;
  /** The idle period, by its `lastAccepted`, in which the clock has had a waiting change sent at 28:00. */
  flushedFor: number | undefined;
  timer: ReturnType<typeof setTimeout> | undefined;
};

// The clock's state, which belongs to one start, so that a hold that settles after its stop changes nothing.
let clock: Clock | undefined;

const phaseListeners = new Set<() => void>();

/**
 * For `api()` only, as an answer's head comes: the server accepted a request sent at `sentAt`, a `Date.now()`, so the
 * clock starts afresh.
 */
export function requestAccepted(sentAt: number): void {
  if (clock === undefined) {
    return;
  }

  clock.ended = false;
  clock.lastAccepted = Math.max(clock.lastAccepted, sentAt);
  clock.lastAnswered = Date.now();
  update(clock);
}

// More than the device's clock and the server's can drift apart over 30 minutes, which is well under a second.
const driftAllowance = 5_000;

/**
 * How long until the server has surely ended the session for being idle, in milliseconds: none once a `401` has said
 * that it has, or while the clock is stopped. The server starts its 30 minutes as it takes a request, which is after
 * the send, from which the clock counts, and before the answer comes, so the session has surely ended 30 minutes after
 * the latest accepted answer came, give or take the clocks' drift. The signed-out-in-place view asks for nothing
 * before then, as a request with the session cookie would renew the session that the screen says has ended (A61).
 */
export function untilSessionSurelyEnded(): number {
  if (clock === undefined || clock.ended) {
    return 0;
  }

  return Math.max(0, clock.lastAnswered + endAfter + driftAllowance - Date.now());
}

/**
 * Keeps the phase as it is until `promise` settles, as a request that is out may start the clock afresh or end it.
 * The holds are counted, and the clock decides once the last has settled.
 */
export function holdWhile(promise: Promise<unknown>): void {
  const held = clock;
  if (held === undefined) {
    return;
  }

  held.holds += 1;
  const release = () => {
    held.holds -= 1;
    update(held);
  };
  void promise.then(release, release);
}

/** What the session clock and Sign out ask of a mounted note form's autosave (T10's Notes). */
export type SaveFlush = {
  /** Whether a change is not yet saved, or the takeover banner waits for its answer. */
  unsaved: () => boolean;
  /**
   * Sends a change waiting now: `true` once it is saved, or with nothing to save; `false` as soon as it will not be,
   * so that nothing waits on a save that is retried for ever.
   */
  flushNow: () => Promise<boolean>;
};

// The note form's autosave while one is mounted, of which there is one at most.
let saveFlush: SaveFlush | undefined;

/** Registers a mounted note form's autosave, and returns the function that removes it as the form goes. */
export function registerSaveFlush(flush: SaveFlush): () => void {
  saveFlush = flush;
  return () => {
    if (saveFlush === flush) {
      saveFlush = undefined;
    }
  };
}

/**
 * For Sign out (app-shell.md *Press Sign out* step 2): every change of a mounted note form saved, a change made while
 * its save was out included, as the full page load that follows would lose it. `true` at once with no form mounted or
 * nothing unsaved; `false` once a save will not be soon, or while the takeover banner waits for its answer.
 */
export async function flushSaves(): Promise<boolean> {
  while (saveFlush?.unsaved() === true) {
    if (!(await saveFlush.flushNow())) {
      return false;
    }
  }

  return true;
}

/** Calls `listener` each time the phase changes, and returns the function that stops it. */
export function subscribe(listener: () => void): () => void {
  phaseListeners.add(listener);
  return () => {
    phaseListeners.delete(listener);
  };
}

/** The clock's phase, which is `active` while it is stopped. */
export function getPhase(): Phase {
  return clock?.phase ?? 'active';
}

/**
 * Starts the clock, active from now, and returns the function that stops it and drops its state. As the page becomes
 * visible, the clock holds on `checkSession()`, the shell's refresh of `/api/auth/me`, whose answer decides
 * (app-shell.md *Page visible again*): one listener sends the request, and the clock holds on that very promise.
 */
export function startClock(checkSession: () => Promise<unknown>): () => void {
  const now = Date.now();
  const started: Clock = {
    phase: 'active',
    lastAccepted: now,
    lastAnswered: now,
    ended: false,
    holds: 0,
    flushedFor: undefined,
    timer: undefined,
  };
  clock = started;
  const onVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      holdWhile(checkSession());
    } else {
      clearTimeout(started.timer);
    }
  };
  document.addEventListener('visibilitychange', onVisibilityChange);
  update(started);

  return () => {
    document.removeEventListener('visibilitychange', onVisibilityChange);
    clearTimeout(started.timer);
    clock = undefined;
    // getPhase() gives `active` from now on, which a subscriber reading it must be told of.
    if (started.phase !== 'active') {
      tell();
    }
  };
}

// Works the phase out from the time, never from ticks counted, and sets one timer for the next mark, so that a page
// whose timers were throttled or frozen catches up as it wakes. Nobody can see a hidden page's warning, so nothing
// moves while the page is hidden, and the refresh as it becomes visible decides.
function update(current: Clock): void {
  if (clock !== current) {
    return;
  }

  clearTimeout(current.timer);
  if (current.holds > 0 || document.visibilityState === 'hidden') {
    return;
  }

  const idle = Date.now() - current.lastAccepted;
  const passed = current.ended ? marks.length : marks.filter((mark) => mark <= idle).length;
  // A change waiting for autosave as the phase would leave `active` goes now, in place of the warning, once in each
  // idle period, holding the phase: its accepted save starts the clock afresh, and one that fails lets the warning
  // open as the hold settles (session-timeout.md *The session clock* step 3).
  if (
    current.phase === 'active'
    && passed > 0
    && !current.ended
    && current.flushedFor !== current.lastAccepted
    && saveFlush?.unsaved() === true
  ) {
    current.flushedFor = current.lastAccepted;
    holdWhile(saveFlush.flushNow());
    return;
  }

  if (phases[passed] !== current.phase) {
    current.phase = phases[passed];
    tell();
  }

  if (passed < marks.length) {
    current.timer = setTimeout(() => {
      update(current);
    }, marks[passed] - idle);
  }
}

function tell(): void {
  for (const listener of phaseListeners) {
    listener();
  }
}
