import { useState } from 'react';
import { api, classify } from '../api/api.ts';
import { flushSaves } from '../api/session.ts';
import { msg } from '../copy/messages.ts';
import { strings } from '../copy/strings.ts';
import { fullPageLoad } from './fullPageLoad.ts';
import { confirmLeave, disarmUnloadGuard, rearmUnloadGuard } from './leaveGuard.ts';

/**
 * The one way the app signs a person out (*Press Sign out* and *Sign out fails*, app-shell.md), for the Account panel
 * and the Support page. `failure` says why a sign-out failed, and is `undefined` as each sign-out starts, so that a
 * failure that repeats is written into an empty line and announced again.
 */
export function useSignOut(): { signOut: () => Promise<void>; signingOut: boolean; failure: string | undefined } {
  const [signingOut, setSigningOut] = useState(false);
  const [failure, setFailure] = useState<string>();

  async function signOut() {
    // First of all, so that a page's Stay sends nothing and leaves the panel as it was.
    if (!(await confirmLeave())) {
      return;
    }

    setFailure(undefined);
    setSigningOut(true);
    // A note form's change waiting for its save goes first, as the reload would lose it; one that cannot be saved now
    // keeps the person signed in, with the page as it is.
    if (!(await flushSaves())) {
      setFailure(strings.notSignedOutNoteNotSaved);
      setSigningOut(false);
      return;
    }

    // The person has chosen to leave, so the reload that follows the logout asks them nothing more.
    disarmUnloadGuard();
    try {
      await api('/api/auth/logout', { method: 'POST' });
    } catch (error) {
      const cause = classify(error);
      // A 401 counts as done: there was no session left to end.
      if (cause !== 'signedOut') {
        // The page is still there, with any text it has not saved, which a reload must warn about again.
        rearmUnloadGuard();
        setFailure(msg.notSignedOut(cause));
        setSigningOut(false);
        return;
      }
    }

    // Still busy, so that nothing more is sent while the page goes.
    fullPageLoad();
  }

  return { signOut, signingOut, failure };
}
