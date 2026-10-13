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

// The session clock (session-timeout.md *The session clock*) predicts when the server will end the session for being
// idle: 30 minutes after the newest request that it accepted, the session cookie's `ExpireTimeSpan` (design.md §8.4),
// with the warning from 28 minutes (A24). It never sends a request, which would extend the session, and the server's
// answer always wins over it.
const warnAfter = 28 * 60_000;
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
  /** Whether a `401` has ended the session since. */
  ended: boolean;
  /** How many promises are holding the phase as it is. */
  holds: number;
  timer: ReturnType<typeof setTimeout> | undefined;
};

// The clock's state, which belongs to one start, so that a hold that settles after its stop changes nothing.
let clock: Clock | undefined;

const phaseListeners = new Set<() => void>();

/** For `api()` only: the server accepted a request sent at `sentAt`, a `Date.now()`, so the clock starts afresh. */
export function requestAccepted(sentAt: number): void {
  if (clock === undefined) {
    return;
  }

  clock.ended = false;
  clock.lastAccepted = Math.max(clock.lastAccepted, sentAt);
  update(clock);
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
  const started: Clock = { phase: 'active', lastAccepted: Date.now(), ended: false, holds: 0, timer: undefined };
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
