import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { buttonClass } from '../../components/buttonClass.ts';
import { FindParticipant } from '../../components/FindParticipant.tsx';
import { LinkRow } from '../../components/LinkRow.tsx';
import { LinkRowList } from '../../components/LinkRowList.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { matchesName } from '../../components/matchesName.ts';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { useView } from '../../components/useView.ts';
import { ViewLinks } from '../../components/ViewLinks.tsx';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { useReturnFocusReady } from '../../routes/routeChange.ts';
import { fullName, participantsQuery, type AdminParticipant } from './participantsQuery.ts';
import styles from './ParticipantsPage.module.css';

// The Archived view's keyword; the Active view, the default, has none.
const views = ['archived'] as const;

/**
 * Participants at `/manage/participants` (participants.md component A): its `<h1>` and Add participant at once, in the
 * same place in every state, then the region that loads every participant, active and archived, once. Add
 * participant is a link, as it changes the address, styled as the primary button. Each row is a return point, which
 * the shell focuses on a pop back to the list once its rows, or the failure to load them, are in the page.
 */
export function ParticipantsPage() {
  const participants = useQuery(participantsQuery);
  useReturnFocusReady(!participants.isPending);

  return (
    <PageStatus>
      <PageHeading>{pageName.participants}</PageHeading>
      <div className={styles.add}>
        <Link to="/manage/participants/new" className={buttonClass('primary')}>
          {pageName.addParticipant}
        </Link>
      </div>
      <LoadRegion
        queries={[participants]}
        loading={strings.participants.listLoading}
        what={strings.participants.theList}
      >
        {/* Called only once the query has data. */}
        {() => <ParticipantViews participants={participants.data!} />}
      </LoadRegion>
    </PageStatus>
  );
}

/**
 * Find a participant, the Active / Archived links and the view's rows, which show together once the list has loaded,
 * so nothing shifts under a finger (A3 to A6). What is typed is kept here, in memory only, so it stays through a
 * switch of view, which keeps this mounted, and goes when the manager leaves the screen.
 */
function ParticipantViews({ participants }: { participants: readonly AdminParticipant[] }) {
  const view = useView(views);
  const [typed, setTyped] = useState('');
  const inView = participants.filter(({ status }) => (status === 'Archived') === (view === 'archived'));
  // Filtered, never sorted again: the API's order, by family name then given name (A5), is the list's.
  const shown = inView.filter((participant) => matchesName(participant, typed));
  const noMatch = inView.length > 0 && shown.length === 0;

  return (
    <>
      {/* Not with no participants at all, where there is nothing to find; a view empty only by the switch keeps it. */}
      {participants.length > 0 && <FindParticipant value={typed} onChange={setTyped} noMatch={noMatch} />}
      <ViewLinks
        options={[
          { value: null, label: strings.participants.active },
          { value: 'archived', label: strings.participants.archived },
        ]}
        current={view}
      />
      {participants.length === 0 && <p>{strings.participants.noneYet}</p>}
      {participants.length > 0 && inView.length === 0 && (
        <p>{view === 'archived' ? strings.participants.noneArchived : strings.participants.noneActive}</p>
      )}
      {noMatch && <p className={styles.noMatch}>{msg.noParticipantMatches(typed.trim())}</p>}
      {shown.length > 0 && (
        <LinkRowList>
          {shown.map((participant) => (
            <LinkRow
              key={participant.id}
              to={`/manage/participants/${participant.id}`}
              name={fullName(participant)}
              returnKey={participant.id}
            />
          ))}
        </LinkRowList>
      )}
    </>
  );
}
