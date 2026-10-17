import { describe, expect, it } from 'vitest';
import { matchesName } from './matchesName.ts';

// Made-up people (A35).
const jane = { givenName: 'Jane', familyName: 'Citizen' };
const zoe = { givenName: 'Zoë', familyName: 'Hill' };
const sean = { givenName: 'Seán', familyName: 'O’Brien' };
const pat = { givenName: 'Pat', familyName: "O'Brien" };
const ana = { givenName: 'Ana', familyName: 'Smith-Jones' };
const lan = { givenName: 'Lan', familyName: 'Nguyễn' };

describe('matchesName', () => {
  it.each([
    ['jan', jane],
    ['JANE', jane],
    ['cit', jane],
    ['jane cit', jane],
    ['cit jane', jane],
    ['citizen, jane', jane],
    ['Jane.', jane],
    ['  jane   citizen  ', jane],
    ['zoe', zoe],
    ['ZOË hill', zoe],
    ['obr', sean],
    ["o'b", sean],
    ['o’brien', pat],
    ['sean', sean],
    ['smith-j', ana],
    ['smith jones', ana],
    ['jones', ana],
    ['nguyen', lan],
  ])('finds the person for "%s"', (typed, person) => {
    expect(matchesName(person, typed)).toBe(true);
  });

  it.each([
    ['xyz', jane],
    ['itizen', jane],
    ['jane x', jane],
    ['janet', jane],
    ['brien', sean],
    ['smithjones', ana],
  ])('does not find the person for "%s", as a typed word starts no word of the name', (typed, person) => {
    expect(matchesName(person, typed)).toBe(false);
  });

  it.each(['', '   ', '.', ' . ', '’', '-'])('finds everyone for "%s", which is nothing once normalised', (typed) => {
    for (const person of [jane, zoe, sean, pat, ana, lan]) {
      expect(matchesName(person, typed)).toBe(true);
    }
  });
});
