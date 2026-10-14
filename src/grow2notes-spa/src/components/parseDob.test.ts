import { describe, expect, it } from 'vitest';
import { parseDob, type DobParts } from './parseDob.ts';

// Melbourne's date from the server, as `me.today` gives it.
const today = '2026-10-01';

/** The three fields, typed as "day / month / year". */
function typed(date: string): DobParts {
  const [day, month, year] = date.split('/');
  return { day, month, year };
}

describe('parseDob', () => {
  it.each([
    ['7/3/1987', '1987-03-07'],
    ['07/03/1987', '1987-03-07'],
    [' 7 / 3 / 1987 ', '1987-03-07'],
    ['7/mar/1987', '1987-03-07'],
    ['7/Mar./1987', '1987-03-07'],
    ['7/MARCH/1987', '1987-03-07'],
    ['7/Sept./1987', '1987-09-07'],
    ['7/sep/1987', '1987-09-07'],
    ['7/september/1987', '1987-09-07'],
    ['31/12/1999', '1999-12-31'],
    ['29/2/2024', '2024-02-29'],
    ['29/2/2000', '2000-02-29'],
    ['30/9/2026', '2026-09-30'],
  ])('reads "%s" as %s', (date, iso) => {
    expect(parseDob(typed(date), today)).toEqual({ ok: true, iso });
  });

  it.each(['//', '  /  /  '])('gives "Enter the date of birth" on every part for "%s"', (date) => {
    expect(parseDob(typed(date), today)).toEqual({
      ok: false,
      message: 'Enter the date of birth',
      parts: ['day', 'month', 'year'],
    });
  });

  it.each([
    ['/3/1987', 'a day', ['day']],
    ['7//1987', 'a month', ['month']],
    ['7/3/', 'a year', ['year']],
    ['//1987', 'a day and month', ['day', 'month']],
    ['/3/', 'a day and year', ['day', 'year']],
    ['7/ /', 'a month and year', ['month', 'year']],
  ])('gives "Date of birth must include %s" for "%s" on the parts left empty', (date, what, parts) => {
    expect(parseDob(typed(date), today)).toEqual({
      ok: false,
      message: `Date of birth must include ${what}`,
      parts,
    });
  });

  it.each(['7/3/87', '7/3/987', '7/3/19870', '31/13/87'])(
    'gives "Year must include 4 numbers" on the year for "%s", before any other check, never guessing a century',
    (date) => {
      expect(parseDob(typed(date), today)).toEqual({
        ok: false,
        message: 'Year must include 4 numbers',
        parts: ['year'],
      });
    },
  );

  it.each([
    ['7/3/19a7', 'year'],
    ['7/3/1 87', 'year'],
    ['1/1/0000', 'year'],
    ['7/13/1987', 'month'],
    ['7/0/1987', 'month'],
    ['7/0 3/1987', 'month'],
    ['7/marc/1987', 'month'],
    ['32/13/1987', 'month'],
    ['31/4/1987', 'day'],
    ['29/2/2023', 'day'],
    ['29/2/1900', 'day'],
    ['0/3/1987', 'day'],
    ['007/3/1987', 'day'],
    ['0 7/3/1987', 'day'],
    ['seven/3/1987', 'day'],
  ])('gives "Date of birth must be a real date" for "%s" on the %s', (date, part) => {
    expect(parseDob(typed(date), today)).toEqual({
      ok: false,
      message: 'Date of birth must be a real date',
      parts: [part],
    });
  });

  it.each(['1/10/2026', '2/10/2026', '1/1/2030'])(
    'gives "Date of birth must be in the past" on every part for "%s", today or later',
    (date) => {
      expect(parseDob(typed(date), today)).toEqual({
        ok: false,
        message: 'Date of birth must be in the past',
        parts: ['day', 'month', 'year'],
      });
    },
  );
});
