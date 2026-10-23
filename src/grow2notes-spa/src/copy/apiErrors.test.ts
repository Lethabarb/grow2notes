import { describe, expect, it } from 'vitest';
import { apiErrorCopy } from './apiErrors.ts';

const nbsp = ' ';

describe("note.in_progress_by_other's words (note-form.md *States*)", () => {
  const startedBy = { authorDisplayName: 'Alex P.', participantName: 'Jane Citizen' };

  it("name today's note, the author in full both times, and the time it was started", () => {
    // 9:14 am in Melbourne on 1 October 2026.
    const words = apiErrorCopy['note.in_progress_by_other']({
      ...startedBy,
      startedAtUtc: '2026-09-30T23:14:00Z',
      noteDate: { y: 2026, m: 10, d: 1 },
      onToday: true,
    });

    expect(words).toBe(`Alex P. started today’s note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`);
  });

  it('name "the note" for an earlier day', () => {
    // 9:14 am in Melbourne on 30 September 2026.
    const words = apiErrorCopy['note.in_progress_by_other']({
      ...startedBy,
      startedAtUtc: '2026-09-29T23:14:00Z',
      noteDate: { y: 2026, m: 9, d: 30 },
      onToday: false,
    });

    expect(words).toBe(`Alex P. started the note for Jane Citizen at 9:14${nbsp}am. Only Alex P. can finish it.`);
  });

  // A manager's past-day note, started on a later day than its note date (design.md §3.8).
  it("give the start's date too when it was another day than the note's", () => {
    const words = apiErrorCopy['note.in_progress_by_other']({
      ...startedBy,
      startedAtUtc: '2026-09-30T23:14:00Z',
      noteDate: { y: 2026, m: 9, d: 28 },
      onToday: false,
    });

    expect(words).toBe(
      `Alex P. started the note for Jane Citizen at Thu 1${nbsp}Oct 2026, 9:14${nbsp}am. Only Alex P. can finish it.`,
    );
  });
});

describe("report.no_notes's words (daily-report.md *States*)", () => {
  it('name the day in words', () => {
    expect(apiErrorCopy['report.no_notes']({ y: 2026, m: 10, d: 1 })).toBe(
      `No submitted notes for Thursday 1${nbsp}October 2026.`,
    );
  });
});
