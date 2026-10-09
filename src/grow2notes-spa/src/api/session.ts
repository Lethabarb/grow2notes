// The session-ended event (session-timeout.md): `api()` raises it, and nothing else does, when a request is answered
// `401` because the person's session has ended, so that the shell can show sign-in.
const listeners = new Set<() => void>();

/** Calls `listener` each time the session-ended event is raised, and returns the function that stops it. */
export function onSessionEnded(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** For `api()` only, which raises the event before it throws, so the shell can act before the caller's own handler. */
export function sessionEnded(): void {
  for (const listener of listeners) {
    listener();
  }
}
