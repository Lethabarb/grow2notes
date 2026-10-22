import type { ReactNode } from 'react';
import { Tag } from '../../components/Tag.tsx';
import { TickListRead, type TickedItem } from '../../components/TickListRead.tsx';
import { parseDateOnly, stamp } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { Pause } from '../../copy/Pause.tsx';
import { Sep } from '../../copy/Sep.tsx';
import { strings } from '../../copy/strings.ts';
import type { ReadGroup, SubmittedNote } from './noteQueries.ts';
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
 * Melbourne, else with its date (microcopy.md §3, *Reference day*); then, only when the note has more than one
 * version, the Edited line, the Edited tag and, after a pause, who saved the current version and when; then, only when
 * that version is flagged, the Flagged tag and the reason after a pause; then the actions, the Past notes link; then
 * its sections. T14 adds Edit, T16 a manager's review lines and panel, and T18 the past-day line and Version history.
 */
export function ReadView({ heading, participantId, note, noteDate }: ReadViewProps) {
  const { author, firstSubmittedAtUtc, current } = note;
  const day = parseDateOnly(noteDate);
  return (
    <>
      {heading}
      <div className={styles.lines}>
        <p className={styles.line}>
          {msg.writtenBy(author.displayName)}
          <Sep />
          {strings.readView.submitted}{' '}
          <time dateTime={firstSubmittedAtUtc}>{stamp(firstSubmittedAtUtc, day)}</time>
        </p>
        {note.isEdited && (
          <p className={styles.line}>
            <Tag kind="edited" />
            <Pause /> {msg.lastChangeBy(note.lastEdit.by)}{' '}
            <time dateTime={note.lastEdit.atUtc}>{stamp(note.lastEdit.atUtc, day)}</time>
          </p>
        )}
        <FlagLine current={current} />
      </div>
      <PastNotesLink participantId={participantId} />
      <ReadSections
        goals={current.goals}
        commonItemGroups={current.commonItemGroups}
        narrative={current.narrative}
      />
    </>
  );
}

/**
 * The flag line of a version that is flagged (participant-notes.md component 4): the Flagged tag and, after a pause,
 * the reason as written; nothing for one that is not. The read view shows it for the note's current version, and the
 * version-conflict notice for the newer version it lays out.
 */
export function FlagLine({ current }: { current: SubmittedNote['current'] }) {
  if (!current.isFlagged) {
    return null;
  }

  return (
    <p className={styles.line}>
      <Tag kind="flagged" />
      <Pause />{' '}
      <span className={styles.written}>{msg.flagReason(current.flagReason)}</span>
    </p>
  );
}

type ReadSectionsProps = {
  /** Every goal of the snapshot, in its order. */
  goals: readonly TickedItem[];
  /** The groups the note shows, Every note and the picked ones, each with every item, in the order given. */
  commonItemGroups: readonly ReadGroup[];
  narrative: string;
};

/**
 * A note's sections as the read view shows them (participant-notes.md components 6 and 7), on a submitted note and on
 * the draft that a manager reads alike, each after a rule: "1. Goals", every goal of the snapshot ticked or not ticked;
 * "2. Common items", a list under an `<h3>` of the group's name for each group the API gives, which has already left
 * out the groups that were not picked (design.md D47), so that it shows what it is given and never puts the writer's
 * question; and "3. Guided notes", the text as React's escaped text, never HTML, or "Nothing written yet." for a draft
 * with nothing but spaces in it, as Submit's checks judge blank, which a submitted note never is.
 */
export function ReadSections({ goals, commonItemGroups, narrative }: ReadSectionsProps) {
  return (
    <>
      <div className={styles.section}>
        <TickListRead heading={strings.note.goals} emptyText={strings.readView.noGoals} items={goals} />
      </div>
      <div className={styles.section}>
        <h2>{strings.note.commonItems}</h2>
        {commonItemGroups.length === 0 ? (
          <p className={styles.empty}>{strings.readView.noCommonItems}</p>
        ) : (
          <div className={styles.groups}>
            {commonItemGroups.map((each, index) => (
              <TickListRead key={index} heading={each.name} headingLevel={3} items={each.items} />
            ))}
          </div>
        )}
      </div>
      <div className={styles.section}>
        <h2>{strings.note.guidedNotes}</h2>
        {narrative.trim() === '' ? (
          <p className={styles.empty}>{strings.readView.nothingWrittenYet}</p>
        ) : (
          <div className={styles.written}>{narrative}</div>
        )}
      </div>
    </>
  );
}
