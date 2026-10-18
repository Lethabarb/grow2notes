import { useMe } from '../../api/me.ts';
import { parseDateOnly, stamp } from '../../copy/format.ts';
import { Sep } from '../../copy/Sep.tsx';
import { strings } from '../../copy/strings.ts';

/**
 * What the note form's save indicator says (note-form.md *Saving*, autosave-status.md *States*): nothing, on a new
 * note with no change yet; "Draft · saved 9:42 am", on a draft reopened, until its first change; "Saving…", from a
 * change until a save that carried the newest change is answered; then "Saved 9:42 am", at the time of that answer;
 * or "Not saved" from a dead end on, which the top bar's second row explains (note-form.md *Conflicts and refusals*).
 */
export type SaveIndicator =
  | { kind: 'none' }
  | { kind: 'draft'; savedAtUtc: string }
  | { kind: 'saving' }
  | { kind: 'saved'; savedAtUtc: string }
  | { kind: 'notSaved' };

/**
 * The indicator's words, each time Melbourne's, from the server's `savedAtUtc`, with its date when that was not
 * Melbourne's today (microcopy.md §3, *Reference day*).
 */
export function SaveIndicatorText({ indicator }: { indicator: SaveIndicator }) {
  switch (indicator.kind) {
    case 'none':
      return null;
    case 'saving':
      return strings.saving;
    case 'notSaved':
      return strings.note.notSaved;
    case 'saved':
      return (
        <>
          {strings.saved} <SavedAt atUtc={indicator.savedAtUtc} />
        </>
      );
    case 'draft':
      return (
        <>
          {strings.noteStatus.Draft}
          <Sep />
          {strings.statusLine.saved} <SavedAt atUtc={indicator.savedAtUtc} />
        </>
      );
  }
}

function SavedAt({ atUtc }: { atUtc: string }) {
  const me = useMe();
  return <time dateTime={atUtc}>{stamp(atUtc, parseDateOnly(me.today))}</time>;
}
