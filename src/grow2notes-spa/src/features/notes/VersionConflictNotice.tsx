import { useQuery } from '@tanstack/react-query';
import { useId, type Ref } from 'react';
import { useMe } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { parseDateOnly, stamp } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { readNote } from './noteQueries.ts';
import { FlagLine, ReadSections } from './ReadView.tsx';
import styles from './VersionConflictNotice.module.css';

type VersionConflictNoticeProps = {
  /** The notice's section, which the form focuses when a press of Save changes meets the conflict, or again. */
  ref: Ref<HTMLElement>;
  participantId: string;
  noteDate: string;
  /** The conflicts met so far, so that a second conflict reads the note afresh and opens the notice anew. */
  conflicts: number;
  /** Start again has been pressed: the newer version stays, closed, for reference, and the button has gone. */
  restarted: boolean;
  /** Start again from the latest version, given its number. */
  onStartAgain: (versionNumber: number) => void;
};

/**
 * The version-conflict notice of edit mode (note-form.md component 6, autosave-status.md *Behaviour* 10), in page flow
 * between Past notes and the form, when another editor has saved a newer version than the one being edited: who saved
 * which version and when, that the person's changes are still below, the newer version read-only in a `<details>`
 * that starts open, and Start again from the latest version. It reads the note afresh, once, from `GET {base}`, which
 * any signed-in user may do, and builds all it shows from that one answer, so that its parts describe the same version,
 * rather than from the 409's own details. A failed read says so, with Try again, in the notice.
 */
export function VersionConflictNotice({
  ref,
  participantId,
  noteDate,
  conflicts,
  restarted,
  onStartAgain,
}: VersionConflictNoticeProps) {
  const me = useMe();
  const headingId = useId();
  // Under the note's prefix, so that leaving the form drops it with the note, and read once for each conflict.
  const newer = useQuery({
    queryKey: ['notes', participantId, noteDate, 'newer', conflicts],
    queryFn: ({ signal }) => readNote(participantId, noteDate, signal),
    staleTime: Infinity,
    gcTime: 0,
    refetchOnReconnect: false,
  });

  return (
    <section ref={ref} tabIndex={-1} aria-labelledby={headingId} className={styles.notice}>
      <LoadRegion queries={[newer]} loading={strings.note.loading} what={strings.note.theNote}>
        {() => {
          const found = newer.data;
          // The note is submitted and has more than one version, as the conflict says; anything else shows nothing.
          if (found?.status !== 'Submitted' || !found.isEdited) {
            return null;
          }

          const { current, lastEdit } = found;
          return (
            <>
              <h2 id={headingId} className={styles.heading}>
                {msg.savedVersionWhileEditing(
                  lastEdit.by,
                  current.versionNumber,
                  stamp(lastEdit.atUtc, parseDateOnly(me.today)),
                )}
              </h2>
              <p className={styles.line}>{strings.note.yourChangesAreStillBelow}</p>
              <details open={!restarted}>
                <summary className={styles.summary}>{msg.versionBy(current.versionNumber, lastEdit.by)}</summary>
                <FlagLine current={current} />
                <ReadSections
                  goals={current.goals}
                  commonItemGroups={current.commonItemGroups}
                  narrative={current.narrative}
                />
              </details>
              {!restarted && (
                <Button
                  className={styles.start}
                  onClick={() => {
                    onStartAgain(current.versionNumber);
                  }}
                >
                  {strings.note.startAgain}
                </Button>
              )}
            </>
          );
        }}
      </LoadRegion>
    </section>
  );
}
