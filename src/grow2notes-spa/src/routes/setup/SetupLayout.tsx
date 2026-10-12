import { useMemo, useState } from 'react';
import { Outlet } from 'react-router';
import { SetupContext, type SetupAccount } from './setupContext.ts';

/**
 * The layout over `/setup`, `/setup/password` and `/setup/app`, whose state is setup's memory: in React only, never in
 * storage, the URL or router state (sign-in.md *Interactions* 7). It lives while the person moves between those
 * addresses, so that leaving them for any other drops it (*Interactions* 13).
 */
export function SetupLayout() {
  const [account, setAccount] = useState<SetupAccount>();
  const setup = useMemo(() => ({ account, setAccount }), [account]);

  return (
    <SetupContext value={setup}>
      <Outlet />
    </SetupContext>
  );
}
