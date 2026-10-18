import type { ReactNode } from 'react';
import { Tag } from '../../components/Tag.tsx';
import { TickListRead } from '../../components/TickListRead.tsx';
import { parseDateOnly, stamp } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { Pause } from '../../copy/Pause.tsx';
import { Sep } from '../../copy/Sep.tsx';
import { strings } from '../../copy/strings.ts';
import type { SubmittedNote } from './noteQueries.ts';
import { PastNotesLink } from './PastNotesLink.tsx';
import styles from './ReadView.module.css';

type ReadViewProps = {
  /** The note's identity `<h1>`, the participant's name and the note date, as the form shows it. */
  heading: ReactNode;
  participantId: string;
  note: SubmittedNote;
  noteDate: string;
};

/**
 * The read view of a submitted note (participant-notes.md *Read view*, note-read-view.md), its current version as
 * written, in the daily report's order and words (design.md §11.3), with no control on it: the `<h1>`; the byline,
 * "Written by Priya Nair · Submitted 4:42 pm", the time alone when the note was submitted on its own date in
 * Melbourne, else with its date (microcopy.md §3, *Reference day*); then, only when the version is flagged, the Flagged
 * tag and the reason after a pause; then the actions, the Past notes link; then each section after a rule: "1. Goals",
 * every goal of the snapshot ticked or not ticked, and "3. Guided notes", the text as React's escaped text, never HTML.
 * T13 adds "2. Common items" between them, as the form does; T14 Edit and the Edited line, T16 a manager's review lines
 * and panel, and T18 the past-day line and Version history.
 */
export function ReadView({ heading, participantId, note, noteDate }: ReadViewProps) {
  const { author, firstSubmittedAtUtc, current } = note;
  return (
    <>
      {heading}
      <div className={styles.lines}>
        <p className={styles.line}>
          {msg.writtenBy(author.displayName)}
          <Sep />
          {strings.readView.submitted}{' '}
          <time dateTime={firstSubmittedAtUtc}>{stamp(firstSubmittedAtUtc, parseDateOnly(noteDate))}</time>
        </p>
        {current.isFlagged && (
          <p className={styles.line}>
            <Tag kind="flagged" />
            <Pause />{' '}
            <span className={styles.written}>{msg.flagReason(current.flagReason)}</span>
          </p>
        )}
      </div>
      <PastNotesLink participantId={participantId} />
      <div className={styles.section}>
        <TickListRead heading={strings.note.goals} emptyText={strings.readView.noGoals} items={current.goals} />
      </div>
      <div className={styles.section}>
        <h2>{strings.note.guidedNotes}</h2>
        <div className={styles.written}>{current.narrative}</div>
      </div>
    </>
  );
}
