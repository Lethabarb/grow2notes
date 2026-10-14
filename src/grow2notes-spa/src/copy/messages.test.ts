import { describe, expect, it } from 'vitest';
import { msg } from './messages.ts';

describe('the character count words (microcopy.md §4)', () => {
  it('give the limit hint, with the limit grouped', () => {
    expect(msg.characterLimitHint(1_000)).toBe('You can enter up to 1,000 characters');
    expect(msg.characterLimitHint(200)).toBe('You can enter up to 200 characters');
  });

  it.each([
    [900, 'You have 100 characters remaining'],
    [999, 'You have 1 character remaining'],
    [1_000, 'You have 0 characters remaining'],
    [1_001, 'You have 1 character too many'],
    [1_002, 'You have 2 characters too many'],
    [2_212, 'You have 1,212 characters too many'],
  ])('give, at %i of 1,000, "%s"', (length, words) => {
    expect(msg.characterCount(length, 1_000)).toBe(words);
  });
});
