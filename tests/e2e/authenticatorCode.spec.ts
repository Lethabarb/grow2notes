import { authenticatorCode } from './authenticatorCode.ts';
import { expect, test } from './fixtures.ts';

// The tests' codes are RFC 6238's: its appendix B's SHA-1 test values, cut to six digits, for its key,
// "12345678901234567890", in base32, as Grow2Notes.Tests' AuthenticatorCodesTests checks the .NET tests' codes.
const key = 'GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ';

for (const [unixSeconds, code] of [
  [59, '287082'],
  [1_111_111_109, '081804'],
  [1_111_111_111, '050471'],
  [1_234_567_890, '005924'],
  [2_000_000_000, '279037'],
  [20_000_000_000, '353130'],
] as const) {
  test(`the code for ${unixSeconds} s after 1970 is RFC 6238's, ${code}`, () => {
    expect(authenticatorCode(key, new Date(unixSeconds * 1000))).toBe(code);
  });
}
