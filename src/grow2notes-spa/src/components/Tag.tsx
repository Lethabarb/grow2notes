import { strings } from '../copy/strings.ts';
import styles from './Tag.module.css';

/**
 * Each kind of tag, its words and its tone (status-tags.md *The mapping*), here alone, so that no screen gives a tag
 * words or a colour of its own: Flagged, in the attention tone, on a submitted note whose current version is flagged.
 * T14 adds Edited, T16 To review and Reviewed, and T18 Past-day note.
 */
const tags = {
  flagged: { words: strings.tag.flagged, tone: 'attention' },
} as const;

export type TagKind = keyof typeof tags;

/**
 * A tag (status-tags.md *Anatomy* 2): a state that a note carries beside its status, in words on a pale fill with a
 * border, which forced colours draw in the text colour. Plain text in the reading order, with no role, never a link,
 * a button or a tab stop, so that the row it sits in says it as part of its description.
 */
export function Tag({ kind }: { kind: TagKind }) {
  const { words, tone } = tags[kind];
  return <span className={`${styles.tag} ${styles[tone]}`}>{words}</span>;
}
