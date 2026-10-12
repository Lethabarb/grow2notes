import { useCallback, useMemo, useState } from 'react';
import { Navigate, Outlet, useMatch } from 'react-router';
import { SetupContext, type SetupAccount } from './setupContext.ts';

/**
 * The layout over `/setup`, `/setup/password` and `/setup/app`, whose state is setup's memory: in React only, never in
 * storage, the URL or router state (sign-in.md *Interactions* 7). It lives while the person moves between those
 * addresses, so that leaving them for any other drops it (*Interactions* 13).
 */
export function SetupLayout() {
  const [account, setAccount] = useState<SetupAccount>();
  const [password, setHeldPassword] = useState<string>();
  const [passwordRefused, setPasswordRefused] = useState(false);
  const [finished, setFinished] = useState(false);
  const atSetup = useMatch('/setup') !== null;
  const setPassword = useCallback((taken: string) => {
    setHeldPassword(taken);
    setPasswordRefused(false);
  }, []);
  const refusePassword = useCallback(() => {
    setPasswordRefused(true);
  }, []);
  const finish = useCallback(() => {
    setFinished(true);
  }, []);
  // Stable, as view E's prompt and prefetch would start again for a new one.
  const drop = useCallback(() => {
    setAccount(undefined);
    setHeldPassword(undefined);
    setPasswordRefused(false);
  }, []);
  const setup = useMemo(
    () => ({ account, setAccount, password, setPassword, passwordRefused, refusePassword, finish, drop }),
    [account, password, setPassword, passwordRefused, refusePassword, finish, drop],
  );

  // Back at `/setup`'s entry once view G has finished setup, which F's and G's entries always follow: it becomes
  // Today's before any page here renders, view E's prompt among them, so that the tab keeps one entry for all of
  // setup, and Back from Today leaves the app (sign-in.md *Interactions* 12). Leaving the layout drops its memory.
  if (finished && atSetup) {
    return <Navigate to="/" replace />;
  }

  return (
    <SetupContext value={setup}>
      <Outlet />
    </SetupContext>
  );
}
