import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router';
import { BackLink } from '../../components/BackLink.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import textBox from '../../components/TextBox.module.css';
import { dateTime } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { PageNotFound } from '../../routes/PageNotFound.tsx';
import { fullName } from '../participants/participantsQuery.ts';
import { noteQuery, participantQuery, type SubmittedNote } from './noteQueries.ts';
import { NoteHeading } from './NoteHeading.tsx';
import { PastDayLine } from './PastDayLine.tsx';
import readView from './ReadView.module.css';
import { FlagLine, ReadSections } from './ReadView.tsx';
import styles from './VersionPage.module.css';
import { saverOf, versionQuery, versionsQuery, type NoteVersion } from './versionQueries.ts';

// A whole number from 1, with no sign, no leading zero and no fraction: the only address of a version.
const versionNumberPattern = /^[1-9][0-9]*$/;

/**
 * One version of a submitted note, page B at `/participants/{participantId}/notes/{noteDate}/versions/{versionNumber}`
 * (version-history.md), for a manager alone. An address whose number is not a whole number from 1 is Page not found,
 * with no request sent; the version itself is the inner page's, so that no hook runs after that check.
 */
export function VersionPage() {
  const { versionNumber = '' } = useParams();
  return versionNumberPattern.test(versionNumber) ? (
    <VersionView versionNumber={Number(versionNumber)} />
  ) : (
    <PageNotFound />
  );
}

/**
 * Version n in full and read-only: the back link, "Version history", in a static top bar; then one region that loads
 * the participant, the note, the version list and the version together, showing nothing until all have, "Loading
 * version 2…" after 1 s, and the fallback `<h1>` "Version history" with the failure sentence and Try again, or Page not
 * found in place of the whole screen for a number the note does not have, which the API answers `404`. Once loaded, the
 * `<h1>` with "Version 2" as its caption; the line in bold that says whether this is the current version or an earlier
 * one, and then links to the current; who saved it and when; the past-day line, for a past-day note; the flag, only if
 * this version is flagged, with its own reason; and the sections as the read view has them, of this version's own
 * groups, with no control and none of the review lines, as they describe the note, not the version. A number as high as
 * the list's, or higher, is the current version: the list fetched afresh may not yet have the version just saved, and
 * the API's `404` alone says that a number is missing.
 */
function VersionView({ versionNumber }: { versionNumber: number }) {
  const { participantId = '', noteDate = '' } = useParams();
  const participant = useQuery(participantQuery(participantId));
  const note = useQuery(noteQuery(participantId, noteDate));
  const versions = useQuery(versionsQuery(participantId, noteDate));
  const version = useQuery(versionQuery(participantId, noteDate, versionNumber));

  return (
    <PageStatus>
      <PageBar>
        <BackLink to={`/participants/${participantId}/notes/${noteDate}/versions`}>{pageName.versionHistory}</BackLink>
      </PageBar>
      <div className={textBox.column}>
        <LoadRegion
          queries={[participant, note, versions, version]}
          loading={msg.loadingVersion(versionNumber)}
          what={msg.version(versionNumber)}
          fallbackHeading={pageName.versionHistory}
        >
          {/* Called only once every query has data. The version list answers `404` for a note that is not submitted. */}
          {() =>
            note.data?.status === 'Submitted' && (
              <Loaded
                name={fullName(participant.data!)}
                noteDate={noteDate}
                note={note.data}
                currentNumber={versions.data![0].versionNumber}
                versionNumber={versionNumber}
                version={version.data!}
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
  currentNumber: number;
  versionNumber: number;
  version: NoteVersion;
};

/** The version once loaded (version-history.md page B, components 3 to 10). */
function Loaded({ name, noteDate, note, currentNumber, versionNumber, version }: LoadedProps) {
  const saver = saverOf(version.createdBy, note.author);
  return (
    <>
      <NoteHeading name={name} noteDate={noteDate} caption={msg.version(versionNumber)} />
      <p className={styles.currency}>
        {versionNumber >= currentNumber ? (
          strings.versionHistory.thisIsCurrent
        ) : (
          <>
            {strings.versionHistory.earlier}{' '}
            <Link to={`../${currentNumber}`} relative="path" className={styles.link}>
              {msg.versionLink(currentNumber)}
            </Link>
            {strings.versionHistory.fullStop}
          </>
        )}
      </p>
      <div className={readView.lines}>
        <p className={readView.line}>
          {versionNumber === 1 ? msg.submittedBy(saver) : msg.savedBy(saver)}{' '}
          <time dateTime={version.createdAtUtc}>{dateTime(version.createdAtUtc)}</time>
        </p>
        {note.isPastDayNote && <PastDayLine author={note.author} startedAtUtc={note.startedAtUtc} />}
        <FlagLine current={version} />
      </div>
      <ReadSections
        goals={version.goals}
        commonItemGroups={version.commonItemGroups}
        narrative={version.narrative}
      />
    </>
  );
}
