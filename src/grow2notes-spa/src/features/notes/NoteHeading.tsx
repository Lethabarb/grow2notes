import { PageHeading } from '../../components/PageHeading.tsx';
import { dateLong, parseDateOnly } from '../../copy/format.ts';
import styles from './NotePage.module.css';

/**
 * The note's identity `<h1>` (note-identity-header.md), on the form and the read view alike, which takes the focus
 * left pending for it on arrival: the name as stored, never translated, then the note date, so that a screen reader
 * reads the person and the day together, as "Jane Citizen Thursday 1 October 2026". A `caption`, which a screen
 * gives when it needs one, sits on its own line before the name and is read first, as in "Editing submitted note
 * (version 2) Sam Taylor Thursday 1 October 2026".
 */
export function NoteHeading({ name, noteDate, caption }: { name: string; noteDate: string; caption?: string }) {
  return (
    <PageHeading>
      {caption !== undefined && (
        <>
          <span className={styles.caption}>{caption}</span>{' '}
        </>
      )}
      <span className={styles.name} translate="no">
        {name}
      </span>{' '}
      <time className={styles.date} dateTime={noteDate}>
        {dateLong(parseDateOnly(noteDate))}
      </time>
    </PageHeading>
  );
}
