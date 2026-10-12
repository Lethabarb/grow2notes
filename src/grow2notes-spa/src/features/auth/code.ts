import { strings } from '../../copy/strings.ts';

// Whitespace, the hyphen-minus, the hyphens and dashes from U+2010 to U+2015, and the minus sign, which people type to
// group the digits as their app shows them: the separators that the server's OneTimeCode takes out too.
const separators = /[\s\-‐-―−]/g;

const sixDigits = /^[0-9]{6}$/;

/**
 * An authenticator app's code as the server reads it (sign-in.md *Interactions* 4; totp-setup.md): NFKC first, so that
 * full-width digits, from a phone keyboard set to another script, become ASCII ones, then without separators.
 */
export function normaliseCode(typed: string): string {
  return typed.normalize('NFKC').replace(separators, '');
}

/**
 * What is wrong with a code as it was typed, or nothing once it reads as exactly six ASCII digits, which is the one
 * code check of sign-in's code step and setup's view G. A code that fails it is never sent, so that a typo never uses
 * up a sign-in attempt.
 */
export function codeFormatError(typed: string): string | undefined {
  const code = normaliseCode(typed);
  if (code === '') {
    return strings.oneTimeCode.label;
  }

  return sixDigits.test(code) ? undefined : strings.oneTimeCode.wrongFormat;
}
