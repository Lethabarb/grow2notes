// The date and time formats (microcopy.md §3). Each is put together from numbers and the fixed English names below,
// so that no engine's own wording ("Sept", "AM", another comma or space) can reach the screen.

// Between a day and its month and between a time and am or pm, so that neither pair splits across two lines.
const nbsp = '\u00A0';
const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;
const months = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
] as const;

/** A calendar date with no time zone, such as a note date. */
export type Ymd = { y: number; m: number; d: number };

/** A calendar date from the API's `DateOnly`, such as "2026-10-01". */
export function parseDateOnly(dateOnly: string): Ymd {
  const [y, m, d] = dateOnly.split('-').map(Number);
  return { y, m, d };
}

// Read in UTC, which has no offset to move the date, so the day is the same whatever zone the device is in.
const dayName = ({ y, m, d }: Ymd) => days[new Date(Date.UTC(y, m - 1, d)).getUTCDay()];
const monthName = ({ m }: Ymd) => months[m - 1];

/** "Thursday 1 October 2026", for a date that names something. */
export function dateLong(date: Ymd): string {
  return `${dayName(date)} ${date.d}${nbsp}${monthName(date)} ${date.y}`;
}

/** "Thursday 1 October", for the Today header alone. */
export function dateToday(date: Ymd): string {
  return `${dayName(date)} ${date.d}${nbsp}${monthName(date)}`;
}

/** "1 October 2026", for a date of birth and the ends of a range. */
export function datePlain(date: Ymd): string {
  return `${date.d}${nbsp}${monthName(date)} ${date.y}`;
}

type Moment = Ymd & { h: number; min: number };

// Every time the app shows is Melbourne's (D37, A33), wherever the device is. 'h23' gives the hour as a number
// from 0 to 23, with no am or pm of the engine's own, and midnight as 0, never 24.
const melbourne = new Intl.DateTimeFormat('en-AU', {
  timeZone: 'Australia/Melbourne',
  year: 'numeric',
  month: 'numeric',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
  hourCycle: 'h23',
});

function inMelbourne(utcIso: string): Moment {
  const parts = melbourne.formatToParts(new Date(utcIso));
  const part = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find((p) => p.type === type)?.value);
  return { y: part('year'), m: part('month'), d: part('day'), h: part('hour'), min: part('minute') };
}

function timeOf({ h, min }: Moment): string {
  if (min === 0 && h === 12) return 'midday';
  if (min === 0 && h === 0) return 'midnight';
  return `${h % 12 || 12}:${String(min).padStart(2, '0')}${nbsp}${h < 12 ? 'am' : 'pm'}`;
}

function dateTimeOf(moment: Moment): string {
  const day = dayName(moment).slice(0, 3);
  const month = monthName(moment).slice(0, 3);
  return `${day} ${moment.d}${nbsp}${month} ${moment.y}, ${timeOf(moment)}`;
}

/** "4:12 pm" in Melbourne, with "midday" and "midnight" at exactly 12:00. */
export function time(utcIso: string): string {
  return timeOf(inMelbourne(utcIso));
}

/** "Thu 1 Oct 2026, 4:12 pm" in Melbourne, for a time on a day other than the reference day. */
export function dateTime(utcIso: string): string {
  return dateTimeOf(inMelbourne(utcIso));
}

/**
 * The time alone when it falls on the reference day in Melbourne (Melbourne's today from the server, or the note
 * date), and the date and time otherwise (microcopy.md §3, Reference day).
 */
export function stamp(utcIso: string, referenceDay: Ymd): string {
  const moment = inMelbourne(utcIso);
  const onReferenceDay = moment.y === referenceDay.y && moment.m === referenceDay.m && moment.d === referenceDay.d;
  return onReferenceDay ? timeOf(moment) : dateTimeOf(moment);
}
