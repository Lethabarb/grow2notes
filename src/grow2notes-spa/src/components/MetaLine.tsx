import { Fragment, type ReactNode } from 'react';
import { Pause } from '../copy/Pause.tsx';
import { Sep } from '../copy/Sep.tsx';
import styles from './MetaLine.module.css';
import { Tag, type TagKind } from './Tag.tsx';

type MetaLineProps = {
  /** The line's parts, in order, from the note's status word where it has one (status-tags.md *Anatomy* 1). */
  parts: readonly ReactNode[];
  /**
   * Whether the first part is the status word, bold and in the text colour, as on Today's rows and Past notes'; false
   * for a line with none, such as a To review row's "{author} · flagged {time}", every part of which is secondary.
   */
  statusFirst?: boolean;
  /** The tags that the note carries, after the line, in status-tags.md's fixed order; none by default. */
  tags?: readonly TagKind[];
};

/**
 * A note's meta line (status-tags.md), which describes its row's link, as on Today's rows and Past notes': the status
 * word, bold, then each other part, each kept on one line where it fits and joined to the next by `Sep`, so that a
 * screen reader reads "Draft, You, saved 9:42 am"; then each tag, after a pause, so that it reads ", Flagged". A line
 * with no status word sets no part apart.
 */
export function MetaLine({ parts, statusFirst = true, tags = [] }: MetaLineProps) {
  return (
    <>
      {parts.map((part, index) => (
        <Fragment key={index}>
          {index > 0 && ' '}
          <span className={styles.part}>
            {index === 0 && statusFirst ? <span className={styles.status}>{part}</span> : part}
            {index < parts.length - 1 ? <Sep /> : tags.length > 0 && <Pause />}
          </span>
        </Fragment>
      ))}
      {tags.map((kind, index) => (
        <Fragment key={kind}>
          {' '}
          <Tag kind={kind} />
          {index < tags.length - 1 && <Pause />}
        </Fragment>
      ))}
    </>
  );
}
