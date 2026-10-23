import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router';
import { BackLink } from '../../components/BackLink.tsx';
import { LinkRow } from '../../components/LinkRow.tsx';
import { LinkRowList } from '../../components/LinkRowList.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { MetaLine } from '../../components/MetaLine.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import textBox from '../../components/TextBox.module.css';
import { dateTime } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { useReturnFocusReady } from '../../routes/routeChange.ts';
import { fullName } from '../participants/participantsQuery.ts';
import { noteQuery, participantQuery, type SubmittedNote } from './noteQueries.ts';
import { NoteHeading } from './NoteHeading.tsx';
import { PastDayLine } from './PastDayLine.tsx';
import readView from './ReadView.module.css';
import { saverOf, versionsQuery, type VersionSummary } from './versionQueries.ts';

/**
 * A submitted note's version history, page A at `/participants/{participantId}/notes/{noteDate}/versions` (version-
 * history.md), for a manager alone: the back link, "Note", in a static top bar; then one region that loads the
 * participant, the note and the version list together, showing nothing until all have, "Loading version history…" after
 * 1 s, and the fallback `<h1>` "Version history" with the failure sentence and Try again, or Page not found in place of
 * the whole screen for a note that has no versions to show, such as a draft, which the API answers `404`. Once loaded,
 * the `<h1>`, the sentence that names the current version, which is the highest number of the list; the past-day line,
 * for a past-day note; "Not edited since submit", for a note with one version; and the versions, newest first, each a
 * row whose link is "Version 3" and whose meta line says who saved it and when, "Submitted" first on version 1. Once the
 * region is ready, its rows or the failure to load them in the page, the shell may focus: on a pop back from a version,
 * the row opened (app-shell.md *The route-change rule (one owner)*, 3b).
 */
export function VersionHistoryPage() {
  const { participantId = '', noteDate = '' } = useParams();
  const participant = useQuery(participantQuery(participantId));
  const note = useQuery(noteQuery(participantId, noteDate));
  const versions = useQuery(versionsQuery(participantId, noteDate));
  useReturnFocusReady(!participant.isPending && !note.isPending && !versions.isPending);

  return (
    <PageStatus>
      <PageBar>
        <BackLink to={`/participants/${participantId}/notes/${noteDate}`}>{pageName.note}</BackLink>
      </PageBar>
      <div className={textBox.column}>
        <LoadRegion
          queries={[participant, note, versions]}
          loading={strings.versionHistory.loading}
          what={pageName.versionHistory}
          fallbackHeading={pageName.versionHistory}
        >
          {/* Called only once every query has data. The version list answers `404` for a note that is not submitted. */}
          {() =>
            note.data?.status === 'Submitted' && (
              <Loaded
                name={fullName(participant.data!)}
                noteDate={noteDate}
                note={note.data}
                versions={versions.data!}
              />
            )
          }
        </LoadRegion>
      </div>
    </PageStatus>
  );
}

type LoadedProps = {
  name: string;
  noteDate: string;
  note: SubmittedNote;
  /** Newest first, and never empty. */
  versions: readonly VersionSummary[];
};

/** The version history once loaded (version-history.md components 3 to 9). */
function Loaded({ name, noteDate, note, versions }: LoadedProps) {
  return (
    <>
      <NoteHeading name={name} noteDate={noteDate} caption={pageName.versionHistory} />
      <p>{msg.currentVersionIs(versions[0].versionNumber)}</p>
      {note.isPastDayNote && (
        <div className={readView.lines}>
          <PastDayLine author={note.author} startedAtUtc={note.startedAtUtc} />
        </div>
      )}
      {versions.length === 1 && <p>{strings.versionHistory.notEdited}</p>}
      <LinkRowList ordered reversed>
        {versions.map(({ versionNumber, createdBy, createdAtUtc }) => (
          <LinkRow
            key={versionNumber}
            look="date"
            to={String(versionNumber)}
            returnKey={String(versionNumber)}
            name={msg.version(versionNumber)}
            detail={[
              <MetaLine
                statusFirst={versionNumber === 1}
                parts={[
                  ...(versionNumber === 1 ? [strings.readView.submitted] : []),
                  saverOf(createdBy, note.author),
                  <time dateTime={createdAtUtc}>{dateTime(createdAtUtc)}</time>,
                ]}
              />,
            ]}
          />
        ))}
      </LinkRowList>
    </>
  );
}
