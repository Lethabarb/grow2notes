import { describe, expect, it } from 'vitest';
import { dateLong, datePlain, dateTime, dateToday, parseDateOnly, stamp, time } from './format.ts';

const firstOctober = { y: 2026, m: 10, d: 1 };

describe('the microcopy.md §8 snapshots', () => {
  it('have dateLong of 1 October 2026, with a non-breaking space between the day and the month', () => {
    expect(dateLong(firstOctober)).toBe('Thursday 1\u00A0October 2026');
  });

  it('have midday at 02:00Z on 1 October 2026', () => {
    expect(time('2026-10-01T02:00:00Z')).toBe('midday');
  });

  it('have the time either side of daylight saving starting, at 2:00 am on 4 October 2026', () => {
    expect(time('2026-10-03T15:30:00Z')).toBe('1:30\u00A0am');
    expect(time('2026-10-03T16:30:00Z')).toBe('3:30\u00A0am');
  });
});

// A device in a zone behind UTC that read these dates in its own time would give the day before, so CI runs them in
// America/Los_Angeles, in WebKit (vitest.webkit.config.ts), as well as in UTC, in jsdom.
describe('the dates', () => {
  it('read a calendar date from the API', () => {
    expect(parseDateOnly('2026-10-01')).toEqual(firstOctober);
  });

  it('give each token in its own form', () => {
    expect(dateLong(parseDateOnly('2026-09-30'))).toBe('Wednesday 30\u00A0September 2026');
    expect(dateToday(firstOctober)).toBe('Thursday 1\u00A0October');
    expect(datePlain(firstOctober)).toBe('1\u00A0October 2026');
  });
});

describe('time', () => {
  it('gives the time in Melbourne, with a non-breaking space before am or pm', () => {
    expect(time('2026-10-01T06:12:00Z')).toBe('4:12\u00A0pm');
  });

  it('gives 2:30 am twice as daylight saving ends, when 3:00 am on 4 April 2027 goes back to 2:00 am', () => {
    expect(time('2027-04-03T15:30:00Z')).toBe('2:30\u00A0am');
    expect(time('2027-04-03T16:30:00Z')).toBe('2:30\u00A0am');
  });

  it('gives midnight at exactly 12:00 am, and the hour 12 for the minutes after midday and midnight', () => {
    expect(time('2026-09-30T14:00:00Z')).toBe('midnight');
    expect(time('2026-10-01T02:05:00Z')).toBe('12:05\u00A0pm');
    expect(time('2026-09-30T14:05:00Z')).toBe('12:05\u00A0am');
  });
});

describe('dateTime', () => {
  it('gives the short day and month, then the time, with non-breaking spaces where they belong', () => {
    expect(dateTime('2026-10-01T06:12:00Z')).toBe('Thu 1\u00A0Oct 2026, 4:12\u00A0pm');
  });

  it('gives Sep, never Sept', () => {
    const september = dateTime('2026-09-30T06:12:00Z');

    expect(september).toBe('Wed 30\u00A0Sep 2026, 4:12\u00A0pm');
    expect(september).not.toContain('Sept');
  });

  it('gives the date in Melbourne, a day after the UTC date', () => {
    expect(dateTime('2026-09-30T14:30:00Z')).toBe('Thu 1\u00A0Oct 2026, 12:30\u00A0am');
  });

  it('ends with midday or midnight at exactly 12:00', () => {
    expect(dateTime('2026-10-01T02:00:00Z')).toBe('Thu 1\u00A0Oct 2026, midday');
    expect(dateTime('2026-09-30T14:00:00Z')).toBe('Thu 1\u00A0Oct 2026, midnight');
  });
});

describe('stamp', () => {
  it('gives the time alone on the reference day in Melbourne, from its first minute', () => {
    expect(stamp('2026-09-30T14:00:00Z', firstOctober)).toBe('midnight');
    expect(stamp('2026-10-01T06:12:00Z', firstOctober)).toBe('4:12\u00A0pm');
  });

  it('gives the date and time a minute before the reference day begins, and once it has ended', () => {
    expect(stamp('2026-09-30T13:59:00Z', firstOctober)).toBe('Wed 30\u00A0Sep 2026, 11:59\u00A0pm');
    expect(stamp('2026-10-01T14:00:00Z', firstOctober)).toBe('Fri 2\u00A0Oct 2026, midnight');
  });
});
