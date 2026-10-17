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

  it("give a field's too-long message, naming it and its limit, grouped", () => {
    expect(msg.tooLong('Guide prompts', 1_000)).toBe('Guide prompts must be 1,000 characters or less');
    expect(msg.tooLong('Reason', 200)).toBe('Reason must be 200 characters or less');
  });
});

describe("a Save's failure (microcopy.md §4's Action failed)", () => {
  it('says it was not saved, and why', () => {
    expect(msg.notSaved('offline')).toBe('Not saved: no connection. Try again.');
    expect(msg.notSaved('server')).toBe('Not saved: something went wrong. Try again.');
  });
});

// users.md *States* › User detail and *Dialogs*, word for word (AC40), with the typographic apostrophe of src/copy.
describe("a user's actions and their dialogs", () => {
  it('say that Reset sign-in, Deactivate or Reactivate failed, and why', () => {
    expect(msg.notReset('offline')).toBe('Not reset: no connection. Try again.');
    expect(msg.notReset('server')).toBe('Not reset: something went wrong. Try again.');
    expect(msg.notDeactivated('offline')).toBe('Not deactivated: no connection. Try again.');
    expect(msg.notDeactivated('server')).toBe('Not deactivated: something went wrong. Try again.');
    expect(msg.notReactivated('offline')).toBe('Not reactivated: no connection. Try again.');
    expect(msg.notReactivated('server')).toBe('Not reactivated: something went wrong. Try again.');
  });

  it("give the Reset sign-in dialog's words for someone else, naming them and their address", () => {
    expect(msg.resetTitle('Alex Park')).toBe('Reset sign-in for Alex Park?');
    expect(msg.resetCheckWhoIsAsking('Alex Park')).toBe(
      'Only do this after you have checked, by phone or in person, that Alex Park is the one asking.',
    );
    expect(msg.resetWhatHappens('alex.park@example.org')).toBe(
      'Their current way of signing in will be removed, and they will be signed out everywhere. ' +
        'A new setup link will be emailed to alex.park@example.org.',
    );
    expect(msg.resetConfirm('Alex Park')).toBe('Reset sign-in for Alex Park');
    expect(msg.resetOwnWhatHappens('sam.lee@example.org')).toBe(
      'You will be signed out everywhere now. A new setup link will be emailed to sam.lee@example.org.',
    );
  });

  it("give the Deactivate dialog's words, design.md's body kept with its contraction", () => {
    expect(msg.deactivateTitle('Alex Park')).toBe('Deactivate Alex Park?');
    expect(msg.deactivateBody('Alex Park')).toBe(
      'Alex Park will be signed out everywhere now and can’t sign in. Their notes stay.',
    );
    expect(msg.deactivateInvitedBody('Alex Park')).toBe(
      'Alex Park has not set up their account yet. Their setup link will stop working.',
    );
    expect(msg.deactivateConfirm('Alex Park')).toBe('Deactivate Alex Park');
  });

  it('give the sentences for a user someone else changed first, on the page and in a dialog', () => {
    expect(msg.userChanged('Alex Park')).toBe(
      'Someone else has just changed Alex Park’s account. The page now shows the latest details.',
    );
    expect(msg.userChangedGoBack('Alex Park')).toBe(
      'Someone else has just changed Alex Park’s account. Go back to see the latest details.',
    );
  });
});

describe("an Add's failure (list-editor.md *Request errors*)", () => {
  it('says it was not added, and why', () => {
    expect(msg.notAdded('offline')).toBe('Not added: no connection. Try again.');
    expect(msg.notAdded('server')).toBe('Not added: something went wrong. Try again.');
  });
});
