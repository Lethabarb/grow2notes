import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { refreshMe, useMe } from '../../api/me.ts';
import { FindParticipant } from '../../components/FindParticipant.tsx';
import { LinkRow } from '../../components/LinkRow.tsx';
import { LinkRowList } from '../../components/LinkRowList.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { matchesName } from '../../components/matchesName.ts';
import { Notice } from '../../components/Notice.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { dateLong, dateToday, parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { Sep } from '../../copy/Sep.tsx';
import { strings } from '../../copy/strings.ts';
import { messageStore } from '../../routes/messageStore.ts';
import { useReturnFocusReady } from '../../routes/routeChange.ts';
import { fullName } from '../participants/participantsQuery.ts';
import { NoteStatus } from './NoteStatus.tsx';
import { myDraftsQuery, refetchToday, todayQuery, type MyDraft, type TodayList } from './todayQuery.ts';
import styles from './TodayPage.module.css';

/**
 * Today at `/` (today.md), everyone's home screen: after Submit, the success banner, "Note for Jane Citizen
 * submitted", which the shell focuses as Today arrives, in place of the `<h1>` or a row (component 2); its `<h1>` at
 * once, "Today · Thursday 1 October", from the server's date in Melbourne, never the device's clock (A33); then the
 * one region that loads the day's list and the caller's unfinished drafts, which show together or not at all, so that
 * the drafts never push the rows down under a finger nor go unsaid (component 4). Each row is a return point, which
 * the shell focuses on a pop back to Today once both lists, or the failure to load them, are in the page.
 */
export function TodayPage() {
  const me = useMe();
  const queryClient = useQueryClient();
  const today = useQuery(todayQuery(me.today));
  const drafts = useQuery(myDraftsQuery);
  useReturnFocusReady(!today.isPending && !drafts.isPending);
  // Submit's sentence, read from the store once, as Today mounts, and kept while Today shows, through every refresh of
  // its list; the store is emptied at once, so that leaving Today and coming back, or a reload, shows it no more.
  const [submitted] = useState(() => messageStore.peek());
  useEffect(() => {
    messageStore.clear();
  }, []);
  // What is typed in Find a participant, here in memory alone, never in the address, the title, a query key or storage
  // (today.md component 7): it stays through a refresh of the list, a new day's included, and goes as the person
  // leaves Today.
  const [typed, setTyped] = useState('');

  // A list for another day than the header's: Melbourne's date has changed since `/me` last answered, so `/me` is
  // asked again, once, and its new `today` puts the header right and loads that day's list (today.md *Conflicts
  // resolved* 14). A failure leaves both as they are, as any refresh's does.
  const listDate = today.data?.date;
  useEffect(() => {
    if (listDate !== undefined && listDate !== me.today) {
      refreshMe(queryClient).catch(() => {});
    }
  }, [listDate, me.today, queryClient]);

  // TanStack Query 5 refetches on `visibilitychange` alone, which a click back into a window left visible beside
  // another never fires, so the window's `focus` fetches both lists again too (today.md *Refresh*).
  useEffect(() => {
    const onFocus = () => {
      void refetchToday(queryClient);
    };

    window.addEventListener('focus', onFocus);
    return () => {
      window.removeEventListener('focus', onFocus);
    };
  }, [queryClient]);

  return (
    <PageStatus>
      {submitted !== undefined && <Notice tone="success">{submitted}</Notice>}
      <PageHeading>
        {pageName.today}
        <Sep />
        <time dateTime={me.today}>{dateToday(parseDateOnly(me.today))}</time>
      </PageHeading>
      <LoadRegion
        queries={[today, drafts]}
        loading={strings.participants.listLoading}
        what={strings.participants.theList}
      >
        {/* Called only once both queries have data. */}
        {() => (
          <>
            <UnfinishedDrafts list={today.data!} drafts={drafts.data!} />
            <Participants list={today.data!} isManager={me.role === 'Manager'} typed={typed} onType={setTyped} />
          </>
        )}
      </LoadRegion>
    </PageStatus>
  );
}

// By family name, then given name, as everywhere (A5), then by note date, oldest first (today.md component 5).
const collator = new Intl.Collator('en-AU');

type UnfinishedDraftsProps = {
  list: TodayList;
  drafts: readonly MyDraft[];
};

/**
 * Your unfinished drafts (today.md component 5), only when the caller has a draft or pending edit that the day's list
 * does not show, as one from an earlier day, or for a participant archived since, which nothing marks, as the draft can
 * still be submitted (design.md §3.7): its `<h2>`, then a row for each, whose link names the participant alone and
 * opens the caller's working copy, the note's address for a draft and its edit form for a pending edit, described by
 * the note date. The rows are a list of their own, which Find a participant never filters, each a return point by its
 * participant and date, so that a pop back to Today focuses the row opened, never the main list's row of the same
 * participant (T12's Notes). With nothing left to show, neither the heading nor the list is in the page.
 */
function UnfinishedDrafts({ list, drafts }: UnfinishedDraftsProps) {
  const onTheList = new Set(list.participants.map((participant) => participant.id));
  const shown = drafts
    .filter((draft) => draft.noteDate !== list.date || !onTheList.has(draft.participantId))
    .toSorted(
      (a, b) =>
        collator.compare(a.familyName, b.familyName) ||
        collator.compare(a.givenName, b.givenName) ||
        collator.compare(a.noteDate, b.noteDate),
    );
  if (shown.length === 0) {
    return null;
  }

  return (
    <div className={styles.drafts}>
      <h2>{strings.today.unfinishedDraftsHeading}</h2>
      <LinkRowList>
        {shown.map((draft) => {
          const key = `${draft.participantId}/${draft.noteDate}`;
          const note = `/participants/${draft.participantId}/notes/${draft.noteDate}`;
          return (
            <LinkRow
              key={key}
              to={draft.kind === 'draft' ? note : `${note}/edit`}
              name={fullName(draft)}
              returnKey={key}
              detail={[<time dateTime={draft.noteDate}>{dateLong(parseDateOnly(draft.noteDate))}</time>]}
            />
          );
        })}
      </LinkRowList>
    </div>
  );
}

type ParticipantsProps = {
  list: TodayList;
  isManager: boolean;
  /** What is typed in Find a participant, which Today holds. */
  typed: string;
  onType: (typed: string) => void;
};

/**
 * The day's participants (today.md components 6 to 9), under the visually hidden `<h2>`: Find a participant, then a
 * row for each person whose name matches what is typed, in the API's order, never sorted again, whose link names them
 * alone and opens their note for the list's date, with no router state, whatever its status line says, as the note's
 * page decides what to show from what the server says then; the line, which describes the link, only where the person
 * has a note; or, when nobody matches, the no-match sentence in place of the rows; or, with no participants at all, no
 * search box, "No participants yet." and, for a manager alone, Add participant.
 */
function Participants({ list, isManager, typed, onType }: ParticipantsProps) {
  const shown = list.participants.filter((participant) => matchesName(participant, typed));
  const noMatch = list.participants.length > 0 && shown.length === 0;

  return (
    <>
      <h2 className="visually-hidden">{strings.today.participantsHeading}</h2>
      {list.participants.length === 0 ? (
        <>
          <p>{strings.participants.noneYet}</p>
          {isManager && (
            <p>
              <Link to="/manage/participants/new" className={styles.add}>
                {pageName.addParticipant}
              </Link>
            </p>
          )}
        </>
      ) : (
        <>
          <FindParticipant className={styles.find} value={typed} onChange={onType} noMatch={noMatch} />
          {noMatch ? (
            <p className={styles.noMatch}>{msg.noParticipantMatches(typed.trim())}</p>
          ) : (
            <LinkRowList>
              {shown.map((participant) => (
                <LinkRow
                  key={participant.id}
                  to={`/participants/${participant.id}/notes/${list.date}`}
                  name={fullName(participant)}
                  returnKey={participant.id}
                  detail={participant.note === null ? undefined : [<NoteStatus note={participant.note} />]}
                />
              ))}
            </LinkRowList>
          )}
        </>
      )}
    </>
  );
}
