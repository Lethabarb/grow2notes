import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import type { DraftContent } from './useAutosave.ts';

/**
 * The message of each field that Submit's checks refuse, by the field's name in the API's JSON: Guided notes' text
 * and the flag's reason.
 */
export type NoteErrors = { narrative?: string; flagReason?: string };

/**
 * Submit's checks (note-form.md *Validation*, *Pressing Submit note* item 1), on the content as Submit would send it:
 * Guided notes not blank, spaces alone counting as blank, and at most `limits.guidedNotes` characters; and, while the
 * flag is ticked, a reason not blank and at most `limits.flagReason`. Blank is judged on the trimmed text, and length
 * on the whole of it, as the count under the field counts it, so that a text these checks pass is sent whole and the
 * server counts the same characters (T09's Notes). The blank text's message adds "The grey text is only a guide." only
 * while guide prompts are set, which the grey text is.
 */
export function checkNote({ narrative, isFlagged, flagReason }: DraftContent, hasPrompts: boolean): NoteErrors {
  const errors: NoteErrors = {};
  if (narrative.trim() === '') {
    errors.narrative = hasPrompts ? strings.note.writeYourNotesNotTheGuide : strings.note.writeYourNotes;
  } else if (narrative.length > limits.guidedNotes) {
    errors.narrative = msg.tooLong(strings.note.guidedNotesName, limits.guidedNotes);
  }

  if (isFlagged) {
    if (flagReason.trim() === '') {
      errors.flagReason = strings.note.enterAReason;
    } else if (flagReason.length > limits.flagReason) {
      errors.flagReason = msg.tooLong(strings.note.reason, limits.flagReason);
    }
  }

  return errors;
}
