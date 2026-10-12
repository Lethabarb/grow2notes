import { createContext, useContext } from 'react';

/** The account a setup link names, as `POST /api/auth/setup/start` answers it (design.md §6.2). */
export type SetupAccount = { email: string; displayName: string };

/** What setup keeps between its views (sign-in.md *Interactions* 7 and 10). */
export type Setup = {
  /** The account the link check found, or `undefined` before it and once setup drops it. */
  account: SetupAccount | undefined;
  setAccount: (account: SetupAccount) => void;
  /** The password that view F took, as typed, never trimmed, for view G to send with the code. */
  password: string | undefined;
  setPassword: (password: string) => void;
  /**
   * Drops everything setup holds, as a `401` from a setup call does (sign-in.md *Setup session timed out*). The same
   * function on every render.
   */
  drop: () => void;
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
