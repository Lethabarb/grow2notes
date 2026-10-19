import { describe, expect, it } from 'vitest';
import { msg } from './messages.ts';
import { strings } from './strings.ts';

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

// note-form.md *Confirmation dialog* and design.md §4.3, word for word.
describe("Submit's words", () => {
  it("name the participant in full on the confirming button and in Today's banner", () => {
    expect(msg.submitNoteFor('Jane Citizen')).toBe('Submit note for Jane Citizen');
    expect(msg.noteSubmitted('Jane Citizen')).toBe('Note for Jane Citizen submitted');
  });

  it('say that it was not submitted, and why, and that the draft is saved', () => {
    expect(msg.notSubmitted('offline')).toBe('Not submitted: no connection. Your draft is saved. Try again.');
    expect(msg.notSubmitted('server')).toBe('Not submitted: something went wrong. Your draft is saved. Try again.');
  });

  it("give Guided notes' too-long message, its limit grouped", () => {
    expect(msg.tooLong('Guided notes', 20_000)).toBe('Guided notes must be 20,000 characters or less');
  });
});

// note-form.md component 15 and *States* › *Discard dialog*, and confirm-dialog.md's dialog 2, word for word.
describe("Discard's words", () => {
  it('name the participant in full in the title and on the confirming button', () => {
    expect(msg.discardTitle('Jane Citizen')).toBe('Discard the draft note for Jane Citizen?');
    expect(msg.discardDraftFor('Jane Citizen')).toBe('Discard draft for Jane Citizen');
  });

  it("say an earlier day's date, and who started someone else's draft and when", () => {
    expect(msg.draftIsFor('Wednesday 30 September 2026')).toBe('This draft is for Wednesday 30 September 2026.');
    expect(msg.startedItAt('Alex P.', '9:14 am')).toBe('Alex P. started it at 9:14 am.');
  });

  it('say that it was not discarded, and why', () => {
    expect(msg.notDiscarded('offline')).toBe('Not discarded: no connection. Try again.');
    expect(msg.notDiscarded('server')).toBe('Not discarded: something went wrong. Try again.');
  });
});

// participant-notes.md *Read view* › Status lines, word for word.
describe("the read view's lines", () => {
  it("name the author as stored in the byline, and give the flag's reason after the form's label", () => {
    expect(msg.writtenBy('Priya Nair')).toBe('Written by Priya Nair');
    expect(msg.flagReason('Mentioned pain in his left knee after the walk.')).toBe(
      'Reason: Mentioned pain in his left knee after the walk.',
    );
  });
});

// participant-notes.md *History list* states, word for word.
describe('Past notes', () => {
  it('say that there are no notes yet for the participant, named in full', () => {
    expect(msg.noNotesYet('Jane Citizen')).toBe('No notes yet for Jane Citizen.');
  });

  it('say that the list or Show older did not load, and why, with "still" after a second failure', () => {
    expect(msg.loadFailed('Past notes', 'offline', false)).toBe('Past notes did not load: no connection. Try again.');
    expect(msg.loadFailed('Past notes', 'server', true)).toBe(
      'Past notes still did not load: something went wrong. Try again.',
    );
    expect(msg.loadFailed(strings.showOlder.olderNotes, 'offline', false)).toBe(
      'Older notes did not load: no connection. Try again.',
    );
    expect(msg.loadFailed(strings.showOlder.olderNotes, 'offline', true)).toBe(
      'Older notes still did not load: no connection. Try again.',
    );
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

describe("a list row's move (list-editor.md *Behaviour* and *Request errors*)", () => {
  it('says where the row went, counted from 1, with no full stop', () => {
    expect(msg.moved('up', 2, 5)).toBe('Moved up to number 2 of 5');
    expect(msg.moved('down', 4, 5)).toBe('Moved down to number 4 of 5');
  });

  it('says it was not moved, and why', () => {
    expect(msg.notMoved('offline')).toBe('Not moved: no connection. Try again.');
    expect(msg.notMoved('server')).toBe('Not moved: something went wrong. Try again.');
  });
});

describe("a common item's Save that moved it (common-items.md *Item actions*)", () => {
  it('names the group it went to the end of, with no full stop', () => {
    expect(msg.itemSavedAndMoved('Personal care')).toBe('Item saved and moved to the end of Personal care');
  });
});

describe("a list row's Save refused as stale (list-editor.md *Request errors*)", () => {
  const words =
    'Someone else changed this goal while you were editing. It now says: Phones her sister. Save to replace it with '
    + 'your wording, or Cancel to keep it.';

  it('names the row by its kind, says its wording now, and says what Save and Cancel each do', () => {
    expect(msg.rowChangedWhileEditing('goal', 'Phones her sister')).toBe(words);
  });

  // common-items.md *States*: an item's wording in typographic quotes, where a goal's has none.
  it("says a common item's wording now in typographic quotes", () => {
    expect(msg.rowChangedWhileEditing('item', msg.quoted('Meal prepared'))).toBe(
      'Someone else changed this item while you were editing. It now says: ‘Meal prepared’. Save to replace it with '
        + 'your wording, or Cancel to keep it.',
    );
  });
});

describe('the note form with no goals (design.md §4.3)', () => {
  it("says so with the participant's given name, as design.md writes it", () => {
    expect(msg.noGoalsSetUp('Jane')).toBe('No goals set up for Jane yet. A manager can add them.');
  });
});
