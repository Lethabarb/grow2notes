import { createContext, useContext } from 'react';

/** The account a setup link names, as `POST /api/auth/setup/start` answers it (design.md §6.2). */
export type SetupAccount = { email: string; displayName: string };

/** What setup keeps between its views (sign-in.md *Interactions* 7). */
export type Setup = {
  /** The account the link check found, or `undefined` before it and once setup drops it. */
  account: SetupAccount | undefined;
  setAccount: (account: SetupAccount | undefined) => void;
};

export const SetupContext = createContext<Setup | undefined>(undefined);

/** The setup layout's memory, for a page under it. */
export function useSetup(): Setup {
  const setup = useContext(SetupContext);
  if (setup === undefined) {
    throw new Error('useSetup is for the pages under the setup layout.');
  }

  return setup;
}
