import { createContext, useContext } from 'react';

/** The account a setup link names, as `POST /api/auth/setup/start` answers it (design.md §6.2). */
export type SetupAccount = { email: string; displayName: string };

/**
 * Which password choice view E's link recorded (D97): a password and an authenticator app, a password only, or a
 * password and a passkey. View F reads it to say what its button does.
 */
export type PasswordChoice = 'authenticatorApp' | 'passwordOnly' | 'passwordAndPasskey';

/** What setup keeps between its views (sign-in.md *Interactions* 7, 10 and 12). */
export type Setup = {
  /** The account the link check found, or `undefined` before it and once setup drops it. */
  account: SetupAccount | undefined;
  setAccount: (account: SetupAccount) => void;
  /** The password that view F took, as typed, never trimmed, for view G to send with the code. */
  password: string | undefined;
  /** Holds the password that view F took, which ends the server's refusal of the one before. */
  setPassword: (password: string) => void;
  /**
   * The server refused the password that view G sent (sign-in.md *Password rejected by server*), so view F shows its
   * message on it until it takes a password again.
   */
  passwordRefused: boolean;
  refusePassword: () => void;
  /** The password choice that view E's link recorded, or `'authenticatorApp'` before one, as it was until D97. */
  passwordChoice: PasswordChoice;
  /** Records the password choice, as pressing its link on view E does. The same function on every render. */
  choosePassword: (choice: PasswordChoice) => void;
  /**
   * Marks setup finished, as view F or G does on its `200 Me` before it goes back to `/setup`'s entry, which the
   * setup layout then replaces with Today's (sign-in.md *Interactions* 12 and 16). The same function on every render.
   */
  finish: () => void;
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
