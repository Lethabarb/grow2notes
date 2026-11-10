import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { useMe } from '../../api/me.ts';
import { buttonClass } from '../../components/buttonClass.ts';
import { Tag } from '../../components/Tag.tsx';
import { TickListRead } from '../../components/TickListRead.tsx';
import { dateTime, parseDateOnly, stamp } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { Pause } from '../../copy/Pause.tsx';
import { Sep } from '../../copy/Sep.tsx';
import { strings } from '../../copy/strings.ts';
import { goalsByTerm } from './goalTerms.ts';
import type { ReadContent, SubmittedNote } from './noteQueries.ts';
import { PastDayLine } from './PastDayLine.tsx';
import pastNotesLinkStyles from './PastNotesLink.module.css';
import { PastNotesLink } from './PastNotesLink.tsx';
import styles from './ReadView.module.css';
import { ReviewPanel } from './ReviewPanel.tsx';

type ReadViewProps = {
  /** The note's identity `<h1>`, the participant's name and the note date, as the form shows it. */
  heading: ReactNode;
  participantId: string;
  note: SubmittedNote;
  noteDate: string;
  /** For a manager's note that is To review though its current version is not flagged, why it was flagged. */
  removedFlagReason?: string;
};

/**
 * The read view of a submitted note (participant-notes.md *Read view*, note-read-view.md), its current version as
 * written, in the daily report's order and words (design.md §11.3), with no control on it: the `<h1>`; the byline,
 * "Written by Priya Nair · Submitted 4:42 pm", the time alone when the note was submitted on its own date in
 * Melbourne, else with its date (microcopy.md §3, *Reference day*); then, only when the note has more than one
 * version, the Edited line, the Edited tag and, after a pause, who saved the current version and when; then, only when
 * that version is flagged, the Flagged tag and the reason after a pause; then, for a manager alone, the review lines:
 * To review, or Reviewed with who and when and the comment; then the actions, Edit first, only for the note's author
 * and for a manager, never shown disabled, which opens the edit form in this entry's place, and the Past notes link;
 * then its sections; then, for a manager, the Review panel, only when the note was To review as the page opened, so
 * that it stays for the visit, through the refetch that shows the note Reviewed, and a note that was already Reviewed
 * has none (participant-notes.md component 8). On a past-day note, which a manager wrote on a later day than its date,
 * the byline is the past-day line and then "Submitted 4:42 pm" on a line of its own. A manager's actions also hold the
 * Version history link, between Edit and Past notes, on every submitted note, even one with a single version.
 */
export function ReadView({ heading, participantId, note, noteDate, removedFlagReason }: ReadViewProps) {
  const { author, firstSubmittedAtUtc, current } = note;
  const me = useMe();
  const [reviewing] = useState(() => note.flagStatus === 'ToReview');
  const review = note.flagStatus === 'Reviewed' ? note.reviews?.[0] : undefined;
  const day = parseDateOnly(noteDate);
  const canEdit = me.role === 'Manager' || author.id === me.userId;
  return (
    <>
      {heading}
      <div className={styles.lines}>
        {note.isPastDayNote ? (
          <>
            <PastDayLine author={author} startedAtUtc={note.startedAtUtc} />
            <p className={styles.line}>
              {strings.readView.submitted}{' '}
              <time dateTime={firstSubmittedAtUtc}>{stamp(firstSubmittedAtUtc, day)}</time>
            </p>
          </>
        ) : (
          <p className={styles.line}>
            {msg.writtenBy(author.displayName)}
            <Sep />
            {strings.readView.submitted}{' '}
            <time dateTime={firstSubmittedAtUtc}>{stamp(firstSubmittedAtUtc, day)}</time>
          </p>
        )}
        {note.isEdited && (
          <p className={styles.line}>
            <Tag kind="edited" />
            <Pause /> {msg.lastChangeBy(note.lastEdit.by)}{' '}
            <time dateTime={note.lastEdit.atUtc}>{stamp(note.lastEdit.atUtc, day)}</time>
          </p>
        )}
        <FlagLine current={current} />
        {note.flagStatus === 'ToReview' && (
          <p className={styles.line}>
            <Tag kind="toReview" />
            {!current.isFlagged && (
              <>
                <Pause /> {strings.flagged.flagRemovedLater}
              </>
            )}
          </p>
        )}
        {note.flagStatus === 'ToReview' && !current.isFlagged && removedFlagReason !== undefined && (
          <p className={`${styles.line} ${styles.written}`}>{msg.flagReason(removedFlagReason)}</p>
        )}
        {review !== undefined && (
          <>
            <p className={styles.line}>
              <Tag kind="reviewed" /> {msg.reviewedBy(review.reviewedBy.displayName)}{' '}
              <time dateTime={review.reviewedAtUtc}>{dateTime(review.reviewedAtUtc)}</time>
            </p>
            {review.comment !== null && (
              <p className={`${styles.line} ${styles.written}`}>{msg.reviewComment(review.comment)}</p>
            )}
          </>
        )}
      </div>
      <div className={styles.actions}>
        {canEdit && (
          <Link to={`/participants/${participantId}/notes/${noteDate}/edit`} replace className={buttonClass()}>
            {strings.readView.edit}
          </Link>
        )}
        {me.role === 'Manager' && (
          <p className={pastNotesLinkStyles.line}>
            <Link className={pastNotesLinkStyles.link} to={`/participants/${participantId}/notes/${noteDate}/versions`}>
              {pageName.versionHistory}
            </Link>
          </p>
        )}
        <PastNotesLink participantId={participantId} />
      </div>
      <ReadSections content={current} />
      {reviewing && (
        <div className={styles.section}>
          <ReviewPanel participantId={participantId} noteDate={noteDate} versionNumber={current.versionNumber} />
        </div>
      )}
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

/**
 * A note's sections as the read view shows them (participant-notes.md components 6 and 7), on a submitted note, an
 * earlier version and the draft that a manager reads alike, in the daily report's order and words (D104), each after a
 * rule and under an `<h2>`: "Attendance", the three ticks; the goals under "Short term goals" and "Medium or Long term
 * goal", a heading left out when its term has none, or "No goals set" alone; "Medication", only when the note has the
 * box, its text with every line break kept; a list for each group the API gives, under its name, which has already left
 * out the groups that were not picked (design.md D47), so that it shows what it is given and never puts the writer's
 * question, with "Details: …" under a ticked item that has them, or "No common items set" alone; and "Daily Support
 * Notes", the text as React's escaped text, never HTML, or "Nothing written yet." for a draft with nothing but spaces
 * in it, as Submit's checks judge blank, which a submitted note never is.
 */
export function ReadSections({ content }: { content: ReadContent }) {
  const { isNotIn, isLateNotice, isSick, medication, goals, commonItemGroups, narrative } = content;
  const { shortTerm, mediumOrLongTerm } = goalsByTerm(goals);
  const { noteWords } = strings;
  return (
    <>
      <div className={styles.section}>
        <TickListRead
          heading={strings.note.attendance}
          items={[
            { text: noteWords.notIn, isTicked: isNotIn },
            { text: noteWords.lateNotice, isTicked: isLateNotice },
            { text: noteWords.sick, isTicked: isSick },
          ]}
        />
      </div>
      <div className={styles.section}>
        {goals.length === 0 ? (
          <p className={styles.empty}>{noteWords.noGoals}</p>
        ) : (
          <div className={styles.groups}>
            {shortTerm.length > 0 && <TickListRead heading={noteWords.shortTermGoals} items={shortTerm} />}
            {mediumOrLongTerm.length > 0 && (
              <TickListRead heading={noteWords.mediumOrLongTermGoal} items={mediumOrLongTerm} />
            )}
          </div>
        )}
      </div>
      {medication !== null && (
        <div className={styles.section}>
          <h2>{noteWords.medication}</h2>
          {medication.trim() !== '' && <div className={styles.written}>{medication}</div>}
        </div>
      )}
      <div className={styles.section}>
        {commonItemGroups.length === 0 ? (
          <p className={styles.empty}>{noteWords.noCommonItems}</p>
        ) : (
          <div className={styles.groups}>
            {commonItemGroups.map((each, index) => (
              <TickListRead key={index} heading={each.name} items={each.items} />
            ))}
          </div>
        )}
      </div>
      <div className={styles.section}>
        <h2>{noteWords.dailySupportNotes}</h2>
        {narrative.trim() === '' ? (
          <p className={styles.empty}>{strings.readView.nothingWrittenYet}</p>
        ) : (
          <div className={styles.written}>{narrative}</div>
        )}
      </div>
    </>
  );
}
