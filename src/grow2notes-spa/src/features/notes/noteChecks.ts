import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import type { DraftContent } from './useAutosave.ts';

/**
 * The message of each field that Submit's checks refuse, by the field's name in the API's JSON: Daily Support Notes'
 * text, the Medication text, the flag's reason, and, by the item's ID, each Details box, in the form's order (A151).
 */
export type NoteErrors = {
  narrative?: string;
  medication?: string;
  flagReason?: string;
  details?: Record<string, string>;
};

/** Whether any field of `errors` has a message. */
export function hasErrors({ narrative, medication, flagReason, details }: NoteErrors): boolean {
  return (
    narrative !== undefined ||
    medication !== undefined ||
    flagReason !== undefined ||
    Object.keys(details ?? {}).length > 0
  );
}

/**
 * Submit's checks (note-form.md *Validation*, *Pressing Submit note* item 1), on the content as Submit would send it:
 * Daily Support Notes not blank, spaces alone counting as blank, and at most `limits.guidedNotes` characters; the
 * Medication text, when the note has the box, at most `limits.medication`; each Details box of a ticked item that asks
 * for details, which `content.details` holds, not blank and at most `limits.itemDetail`, the message naming the item
 * by its wording, which `wording` gives from its ID; and, while the flag is ticked, a reason not blank and at most
 * `limits.flagReason`. Blank is judged on the trimmed text, and length on the whole of it, as the count under the field
 * counts it, so that a text these checks pass is sent whole and the server counts the same characters (T09's Notes).
 * The blank text's message adds "The grey text is only a guide." only while guide prompts are set, which the grey text
 * is.
 */
export function checkNote(
  { narrative, medication, details, isFlagged, flagReason }: DraftContent,
  hasPrompts: boolean,
  wording: (itemId: string) => string,
): NoteErrors {
  const errors: NoteErrors = {};
  if (narrative.trim() === '') {
    errors.narrative = hasPrompts ? strings.note.writeYourNotesNotTheGuide : strings.note.writeYourNotes;
  } else if (narrative.length > limits.guidedNotes) {
    errors.narrative = msg.tooLong(strings.noteWords.dailySupportNotes, limits.guidedNotes);
  }

  if (medication !== null && medication.length > limits.medication) {
    errors.medication = msg.tooLong(strings.noteWords.medication, limits.medication);
  }

  for (const [itemId, written] of Object.entries(details)) {
    if (written.trim() === '') {
      (errors.details ??= {})[itemId] = msg.enterDetails(wording(itemId));
    } else if (written.length > limits.itemDetail) {
      (errors.details ??= {})[itemId] = msg.tooLong(strings.noteWords.details, limits.itemDetail);
    }
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

/**
 * The messages still shown after an input (form-validation.md *Fixing*): a field with a message is checked again, and
 * its message goes with the input that fixes it, or says what the field now fails; a field with none gets none while
 * typing, and a Details box that has gone with its item's tick takes its message with it.
 */
export function recheckedErrors(shown: NoteErrors, now: NoteErrors): NoteErrors {
  const details = Object.fromEntries(
    Object.keys(shown.details ?? {}).flatMap((itemId) => {
      const message = now.details?.[itemId];
      return message === undefined ? [] : [[itemId, message]];
    }),
  );
  return {
    narrative: shown.narrative === undefined ? undefined : now.narrative,
    medication: shown.medication === undefined ? undefined : now.medication,
    flagReason: shown.flagReason === undefined ? undefined : now.flagReason,
    details: Object.keys(details).length === 0 ? undefined : details,
  };
}
