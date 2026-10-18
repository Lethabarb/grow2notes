import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { Link } from 'react-router';
import { refreshMe, useMe } from '../../api/me.ts';
import { LinkRow } from '../../components/LinkRow.tsx';
import { LinkRowList } from '../../components/LinkRowList.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { dateToday, parseDateOnly } from '../../copy/format.ts';
import { pageName } from '../../copy/pageNames.ts';
import { Sep } from '../../copy/Sep.tsx';
import { strings } from '../../copy/strings.ts';
import { useReturnFocusReady } from '../../routes/routeChange.ts';
import { fullName } from '../participants/participantsQuery.ts';
import { refetchToday, todayQuery, type TodayList } from './todayQuery.ts';
import styles from './TodayPage.module.css';

/**
 * Today at `/` (today.md), everyone's home screen: its `<h1>` at once, "Today · Thursday 1 October", from the server's
 * date in Melbourne, never the device's clock (A33); then the region that loads the day's list. Each row is a return
 * point, which the shell focuses on a pop back to Today once the list, or the failure to load it, is in the page.
 */
export function TodayPage() {
  const me = useMe();
  const queryClient = useQueryClient();
  const today = useQuery(todayQuery(me.today));
  useReturnFocusReady(!today.isPending);

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
  // another never fires, so the window's `focus` fetches the list again too (today.md *Refresh*).
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
      <PageHeading>
        {pageName.today}
        <Sep />
        <time dateTime={me.today}>{dateToday(parseDateOnly(me.today))}</time>
      </PageHeading>
      <LoadRegion queries={[today]} loading={strings.participants.listLoading} what={strings.participants.theList}>
        {/* Called only once the query has data. */}
        {() => <Participants list={today.data!} isManager={me.role === 'Manager'} />}
      </LoadRegion>
    </PageStatus>
  );
}

/**
 * The day's participants (today.md components 6, 8 and 9), under the visually hidden `<h2>`: a row for each, in the
 * API's order, never sorted again, whose link names them alone and opens their note for the list's date, with no
 * router state; or, with none, "No participants yet.", and, for a manager alone, Add participant. T09 gives each row
 * its status line.
 */
function Participants({ list, isManager }: { list: TodayList; isManager: boolean }) {
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
        <LinkRowList>
          {list.participants.map((participant) => (
            <LinkRow
              key={participant.id}
              to={`/participants/${participant.id}/notes/${list.date}`}
              name={fullName(participant)}
              returnKey={participant.id}
            />
          ))}
        </LinkRowList>
      )}
    </>
  );
}
