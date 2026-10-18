import { describe, expect, it } from 'vitest';
import { capped, sentContent } from './useAutosave.ts';

describe('capped', () => {
  it('leaves a text of the limit or shorter as it is', () => {
    expect(capped('Jane made toast.', 16)).toBe('Jane made toast.');
    expect(capped('', 16)).toBe('');
  });

  it('cuts a longer text to the limit, counted as UTF-16 code units, as .NET counts a string', () => {
    // "é" written as e and a combining accent is two code units, as the server counts it too.
    expect(capped('Zoé went out', 4)).toBe('Zoé');
    expect(capped('x'.repeat(20_050), 20_000)).toHaveLength(20_000);
  });

  it('stops short of a surrogate pair that the limit would split, rather than send half a character', () => {
    // "😀" is a surrogate pair, two code units, the 4th and 5th here.
    expect(capped('abc😀d', 4)).toBe('abc');
    expect(capped('abc😀d', 5)).toBe('abc😀');
  });
});

describe('sentContent', () => {
  const content = { narrative: 'Jane made toast.', tickedGoalIds: ['g1'], isFlagged: true, flagReason: 'Unwell.' };

  it('sends the text, the ticks, the flag and the reason, as autosave and Submit alike send them', () => {
    expect(sentContent(content)).toEqual(content);
  });

  it('sends no reason while the box is unticked, or while the reason is blank', () => {
    expect(sentContent({ ...content, isFlagged: false })).toEqual({ ...content, isFlagged: false, flagReason: null });
    expect(sentContent({ ...content, flagReason: '   ' })).toEqual({ ...content, flagReason: null });
  });

  it('cuts the text and the reason to their limits', () => {
    const sent = sentContent({ ...content, narrative: 'x'.repeat(20_050), flagReason: 'y'.repeat(210) });

    expect(sent.narrative).toBe('x'.repeat(20_000));
    expect(sent.flagReason).toBe('y'.repeat(200));
  });
});
