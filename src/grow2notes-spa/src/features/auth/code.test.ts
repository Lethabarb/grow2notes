import { describe, expect, it } from 'vitest';
import { codeFormatError, normaliseCode } from './code.ts';

const enterCode = 'Enter the 6-digit code from your authenticator app';
const sixDigits = 'The code must be 6 digits';

describe('normaliseCode', () => {
  it.each([
    { typed: '123456', kind: 'six digits' },
    { typed: '123 456', kind: 'a space' },
    { typed: ' 12 34 56\t', kind: 'spaces and a tab round and between the digits' },
    { typed: '123 456', kind: 'a no-break space' },
    { typed: '123　456', kind: 'an ideographic space' },
    { typed: '123-456', kind: 'a hyphen-minus' },
    { typed: '123‐456', kind: 'a hyphen' },
    { typed: '123‑456', kind: 'a no-break hyphen' },
    { typed: '123–456', kind: 'an en dash' },
    { typed: '123—456', kind: 'an em dash' },
    { typed: '123―456', kind: 'a horizontal bar' },
    { typed: '123−456', kind: 'a minus sign' },
    { typed: '123－456', kind: 'a full-width hyphen-minus' },
    { typed: '１２３４５６', kind: 'full-width digits' },
    { typed: '１２３　４５６', kind: 'full-width digits and space' },
  ])('reads a code with $kind as its six ASCII digits', ({ typed }) => {
    expect(normaliseCode(typed)).toBe('123456');
  });

  it('keeps what is not a separator, for the check to refuse', () => {
    expect(normaliseCode('12_3.45a6')).toBe('12_3.45a6');
  });
});

describe('codeFormatError', () => {
  it.each([
    { typed: '', kind: 'nothing' },
    { typed: '   ', kind: 'only spaces' },
    { typed: ' - – ', kind: 'only spaces and dashes' },
  ])('asks for the code, as its label does, for $kind', ({ typed }) => {
    expect(codeFormatError(typed)).toBe(enterCode);
  });

  it.each([
    { typed: '12345', kind: 'five digits' },
    { typed: '1234567', kind: 'seven digits' },
    { typed: 'abcdef', kind: 'letters' },
    { typed: '12345a', kind: 'five digits and a letter' },
    { typed: '123_456', kind: 'an underscore between the digits' },
    { typed: '123.456', kind: 'a full stop between the digits' },
    // Digits of another script, which NFKC leaves as they are, as does the server's check.
    { typed: '١٢٣٤٥٦', kind: 'Arabic-Indic digits' },
  ])('says the code must be 6 digits for $kind', ({ typed }) => {
    expect(codeFormatError(typed)).toBe(sixDigits);
  });

  it.each(['123456', '000000', '123 456', '123-456', '１２３４５６'])(
    'finds nothing wrong with "%s"',
    (typed) => {
      expect(codeFormatError(typed)).toBeUndefined();
    },
  );
});
