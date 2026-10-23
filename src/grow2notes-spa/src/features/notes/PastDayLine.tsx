import { Tag } from '../../components/Tag.tsx';
import { dateTime } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { Pause } from '../../copy/Pause.tsx';
import type { NoteAuthor } from './noteQueries.ts';
import styles from './ReadView.module.css';

/**
 * The past-day line of a note that a manager wrote on a later day than its date (status-tags.md, version-history.md):
 * the Past-day note tag and, after a pause, who wrote it and when they started it, so that a screen reader hears
 * "Past-day note, written by Jo Smith (manager) on Thu 1 Oct 2026, 10:14 am". The read view and both pages of the
 * version history show it, as one line of the byline's kind.
 */
export function PastDayLine({ author, startedAtUtc }: { author: NoteAuthor; startedAtUtc: string }) {
  return (
    <p className={styles.line}>
      <Tag kind="pastDay" />
      <Pause /> {msg.pastDayWrittenBy(author.displayName)} <time dateTime={startedAtUtc}>{dateTime(startedAtUtc)}</time>
    </p>
  );
}
