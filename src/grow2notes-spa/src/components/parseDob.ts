import { msg } from '../copy/messages.ts';
import { strings } from '../copy/strings.ts';

/** The date of birth's three fields as typed. */
export type DobParts = { day: string; month: string; year: string };

export type DobPart = keyof DobParts;

/**
 * A date of birth read from its fields: the date, written `1987-03-07`, as the API takes it; or the first check that
 * failed, its message and the parts it marks invalid, in the fields' order.
 */
export type DobResult = { ok: true; iso: string } | { ok: false; message: string; parts: readonly DobPart[] };

/** The parts in the fields' order, which is the order in which a message names them. */
export const dobParts: readonly DobPart[] = ['day', 'month', 'year'];

const monthNames = [
  'january', 'february', 'march', 'april', 'may', 'june',
  'july', 'august', 'september', 'october', 'november', 'december',
] as const;

/**
 * The month's number, from its digits, leading zeros and all, or its English name, whole or in its first three
 * letters, or "sept", in any case, with an optional full stop at the end (GOV.UK's finding that people type month
 * names); `undefined` for anything else.
 */
function monthNumber(typed: string): number | undefined {
  if (/^\d{1,2}$/.test(typed)) {
    return Number(typed);
  }

  const name = typed.toLowerCase().replace(/\.$/, '');
  if (name === 'sept') {
    return 9;
  }

  const index = monthNames.findIndex((month) => month === name || month.slice(0, 3) === name);
  return index === -1 ? undefined : index + 1;
}

/** The days in the month of a year in the Gregorian calendar, worked out here, as `Date` takes 0 to 99 as 1900s. */
function daysIn(month: number, year: number): number {
  if (month === 2) {
    const leap = (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
    return leap ? 29 : 28;
  }

  return [4, 6, 9, 11].includes(month) ? 30 : 31;
}

/**
 * Reads a date of birth from its three fields by text-and-date-inputs.md's checks, in their order, and gives the
 * first that fails: none typed; a part left empty; a year of digits but not 4 of them; a date that is not real, on the
 * part that is wrong; and one that is not before `today`. Each part is trimmed, and a two-digit year is never guessed
 * into a century. The rules are the server's, which stays the gate; this words them.
 *
 * @param today The date in Melbourne from the server, `me.today`, never the device's clock.
 */
export function parseDob(parts: DobParts, today: string): DobResult {
  const typed: DobParts = { day: parts.day.trim(), month: parts.month.trim(), year: parts.year.trim() };
  const missing = dobParts.filter((part) => typed[part] === '');
  if (missing.length === dobParts.length) {
    return { ok: false, message: strings.dateOfBirth.enter, parts: dobParts };
  }

  if (missing.length > 0) {
    const names = missing.map((part) => strings.dateOfBirth.partNames[part]);
    return { ok: false, message: msg.dateOfBirthMustInclude(names), parts: missing };
  }

  // There is no year 0, and the API's date holds none before year 1.
  if (!/^\d{4}$/.test(typed.year) || Number(typed.year) === 0) {
    const message = /^\d+$/.test(typed.year) && typed.year.length !== 4
      ? strings.dateOfBirth.yearFourNumbers
      : strings.dateOfBirth.notReal;
    return { ok: false, message, parts: ['year'] };
  }

  const year = Number(typed.year);
  const month = monthNumber(typed.month);
  if (month === undefined || month < 1 || month > 12) {
    return { ok: false, message: strings.dateOfBirth.notReal, parts: ['month'] };
  }

  const day = /^\d{1,2}$/.test(typed.day) ? Number(typed.day) : 0;
  if (day < 1 || day > daysIn(month, year)) {
    return { ok: false, message: strings.dateOfBirth.notReal, parts: ['day'] };
  }

  const iso = `${typed.year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  if (iso >= today) {
    return { ok: false, message: strings.dateOfBirth.inThePast, parts: dobParts };
  }

  return { ok: true, iso };
}
