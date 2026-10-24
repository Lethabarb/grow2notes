import { useInfiniteQuery, useQuery, useQueryClient, type UseInfiniteQueryResult } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, NavigationType, useNavigationType, useParams } from 'react-router';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { buttonClass } from '../../components/buttonClass.ts';
import { ButtonGroup } from '../../components/ButtonGroup.tsx';
import { ChronoList } from '../../components/ChronoList.tsx';
import { LinkRow } from '../../components/LinkRow.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { MetaLine } from '../../components/MetaLine.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import type { TagKind } from '../../components/Tag.tsx';
import { dateLong, parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { useBackTo, useReturnFocusReady } from '../../routes/routeChange.ts';
import { fullName } from '../participants/participantsQuery.ts';
import type { ParticipantHeader } from './noteQueries.ts';
import noteStyles from './NotePage.module.css';
import styles from './PastNotesPage.module.css';
import { dropPastNotes, noteDateOf, pastNotesHeaderQuery, pastNotesQuery, type PastNote } from './pastNotesQuery.ts';

/**
 * A participant's Past notes at `/participants/{participantId}/notes` (participant-notes.md *History list*), keyed by
 * the participant, so that nothing of one participant's list is ever kept on the page for another's.
 */
export function PastNotesPage() {
  const { participantId = '' } = useParams();
  return <PastNotes key={participantId} participantId={participantId} />;
}

/**
 * Past notes (design.md §4.4 items 1 and 2): the back link in a static top bar, to the screen that opened it, "Note"
 * or "Participant", else "Today"; then one region that loads the participant and the first page of their notes
 * together, showing nothing until both have, "Loading notes…" after 1 s, and the fallback `<h1>` "Past notes" with the
 * failure sentence and Try again, or Page not found in place of the whole screen for a participant not in the
 * organisation (empty-loading-error.md). A push is a new visit, which starts from the newest 30 at the top, so what an
 * earlier visit loaded is dropped before the queries read the cache; a pop, Back from a note opened here, keeps every
 * page loaded, which shows at once, and the shell then focuses the row opened, where the list was (participant-notes.md
 * items 13 and 14). Once the region is ready, its rows or the failure to load them in the page, the shell may focus.
 */
function PastNotes({ participantId }: { participantId: string }) {
  const me = useMe();
  const queryClient = useQueryClient();
  const action = useNavigationType();
  // Once, as the page mounts, before either query below reads the cache.
  useState(() => {
    if (action !== NavigationType.Pop) {
      dropPastNotes(queryClient, participantId);
    }
  });
  const participant = useQuery(pastNotesHeaderQuery(participantId));
  const notes = useInfiniteQuery(pastNotesQuery(participantId));
  useReturnFocusReady(!participant.isPending && !notes.isPending);
  const back = useBackTo({ note: pageName.note, participant: pageName.participant });

  return (
    <PageStatus>
      <PageBar>
        <BackLink to={back.to}>{back.label}</BackLink>
      </PageBar>
      <div className={styles.column}>
        <LoadRegion
          queries={[participant, notes]}
          loading={strings.pastNotes.loading}
          what={pageName.pastNotes}
          fallbackHeading={pageName.pastNotes}
        >
          {/* Called only once both queries have data. */}
          {() => (
            <Loaded participant={participant.data!} notes={notes} isManager={me.role === 'Manager'} />
          )}
        </LoadRegion>
      </div>
    </PageStatus>
  );
}

type LoadedProps = {
  participant: ParticipantHeader;
  notes: UseInfiniteQueryResult<PastNote[]>;
  isManager: boolean;
};

/**
 * A row's tags, in the order status-tags.md gives them: Flagged, then To review or Reviewed, which only a manager's
 * rows have as only they hold `flagStatus`, then Edited. A draft has none.
 */
function tagsOf(note: PastNote): TagKind[] {
  const tags: TagKind[] = [];
  if (note.status === 'Submitted' && note.isFlagged) {
    tags.push('flagged');
  }

  if (note.status === 'Submitted' && note.flagStatus === 'ToReview') {
    tags.push('toReview');
  } else if (note.status === 'Submitted' && note.flagStatus === 'Reviewed') {
    tags.push('reviewed');
  }

  if (note.status === 'Submitted' && note.isEdited) {
    tags.push('edited');
  }

  return tags;
}

/**
 * The list once loaded (participant-notes.md components 3 to 7): the `<h1>`, the caption "Past notes" and the name as
 * stored, never translated; for a manager alone, Write past-day note, unless the participant is archived, Export
 * record and Edit participant, there even with no notes; then the notes, newest first, 30 at a time with Show older, or "No notes yet
 * for Jane Citizen.". Each row's link names the note's date alone and opens the note, which shows what this person may
 * see of it, as the list cannot tell; its status and author describe it, and Flagged too on a submitted note whose
 * current version is flagged, then, on a manager's list, To review or Reviewed, then Edited when it has more than one
 * version, never on a draft.
 */
function Loaded({ participant, notes, isManager }: LoadedProps) {
  const name = fullName(participant);
  return (
    <>
      <PageHeading>
        <span className={noteStyles.caption}>{pageName.pastNotes}</span>{' '}
        <span className={noteStyles.name} translate="no">
          {name}
        </span>
      </PageHeading>
      {isManager && (
        <div className={styles.actions}>
          <ButtonGroup>
            {participant.status !== 'Archived' && (
              <Link to={`/manage/participants/${participant.id}/past-day-note`} className={buttonClass()}>
                {pageName.writePastDayNote}
              </Link>
            )}
            <Link to={`/manage/participants/${participant.id}/export`} className={buttonClass()}>
              {pageName.exportRecord}
            </Link>
            <Link to={`/manage/participants/${participant.id}`} className={buttonClass()}>
              {strings.pastNotes.editParticipant}
            </Link>
          </ButtonGroup>
        </div>
      )}
      <div className={styles.notes}>
        <ChronoList
          items={notes.data!}
          getKey={noteDateOf}
          emptyText={msg.noNotesYet(name)}
          older={notes}
          renderRow={(note) => (
            <LinkRow
              look="date"
              to={`/participants/${participant.id}/notes/${note.noteDate}`}
              name={<time dateTime={note.noteDate}>{dateLong(parseDateOnly(note.noteDate))}</time>}
              returnKey={note.noteDate}
              detail={[
                <MetaLine
                  parts={[strings.noteStatus[note.status], note.authorDisplayName]}
                  tags={tagsOf(note)}
                />,
              ]}
            />
          )}
        />
      </div>
    </>
  );
}
