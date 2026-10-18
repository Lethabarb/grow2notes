import type { ReactNode } from 'react';
import { Tag } from '../../components/Tag.tsx';
import { time } from '../../copy/format.ts';
import { Pause } from '../../copy/Pause.tsx';
import { Sep } from '../../copy/Sep.tsx';
import { strings } from '../../copy/strings.ts';
import styles from './NoteStatus.module.css';
import type { TodayNote } from './todayQuery.ts';

/**
 * A row's status line on Today (today.md component 8), which describes the row's link: the status word, bold; who has
 * the note, "You" for the signed-in user's own, else the author's display name as stored; and when, the author's last
 * save of their own draft, the start of anyone else's, or a submitted note's first submitted time; each part kept on
 * one line where it fits and joined to the next by `Sep`, so that a screen reader reads "Draft, You, saved 9:42 am".
 * Then, on a submitted note whose current version is flagged, the Flagged tag, after a pause, never on a draft, whose
 * flag nobody else sees (design.md §3.6). Each time is Melbourne's, alone, as Today's notes are today's (microcopy.md
 * §3, *Reference day*).
 */
export function NoteStatus({ note }: { note: TodayNote }) {
  const flagged = note.status === 'Submitted' && note.isFlagged;
  return (
    <>
      <span className={styles.part}>
        <span className={styles.status}>{strings.noteStatus[note.status]}</span>
        <Sep />
      </span>{' '}
      <span className={styles.part}>
        {note.isMine ? strings.statusLine.you : note.authorDisplayName}
        <Sep />
      </span>{' '}
      <span className={styles.part}>
        {when(note)}
        {flagged && <Pause />}
      </span>
      {flagged && (
        <>
          {' '}
          <Tag kind="flagged" />
        </>
      )}
    </>
  );
}

function when(note: TodayNote): ReactNode {
  if (note.status === 'Submitted') {
    return <At utc={note.firstSubmittedAtUtc} />;
  }

  return note.isMine ? (
    <>
      {strings.statusLine.saved} <At utc={note.savedAtUtc} />
    </>
  ) : (
    <>
      {strings.statusLine.started} <At utc={note.startedAtUtc} />
    </>
  );
}

function At({ utc }: { utc: string }) {
  return <time dateTime={utc}>{time(utc)}</time>;
}
