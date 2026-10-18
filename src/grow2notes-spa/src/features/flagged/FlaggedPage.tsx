import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { ChronoList } from '../../components/ChronoList.tsx';
import { LinkRow } from '../../components/LinkRow.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { MetaLine } from '../../components/MetaLine.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { dateLong, parseDateOnly, stamp } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { Pause } from '../../copy/Pause.tsx';
import { strings } from '../../copy/strings.ts';
import { useReturnFocusReady } from '../../routes/routeChange.ts';
import { fullName } from '../participants/participantsQuery.ts';
import styles from './FlaggedPage.module.css';
import { refetchToReview, toReviewQuery, type ToReviewNote } from './toReviewQuery.ts';

/**
 * Flagged notes at `/flagged` (flagged.md), for managers alone, inside `ManagerOnly`: its static `<h1>` at once, which
 * the shell focuses on arrival, then the region that loads the To review list, with "Loading flagged notes…" after
 * 1 s, "Flagged notes did not load: …" with Try again, or Page not found in place of the whole screen for a `403` or a
 * `404`, as after a change of role. The list is asked for again on every visit, as the page becomes visible and as the
 * window regains focus, the rows already shown staying meanwhile. Once the region is ready, its rows or the failure to
 * load them in the page, the shell may focus the row opened from it on a pop back. The view switch and Reviewed come
 * with T19, so any `?view=` shows To review.
 */
export function FlaggedPage() {
  const queryClient = useQueryClient();
  const toReview = useQuery(toReviewQuery);
  useReturnFocusReady(!toReview.isPending);

  // TanStack Query 5 refetches on `visibilitychange` alone, which a click back into a window left visible beside
  // another never fires, so the window's `focus` fetches the list again too, as the shell refreshes `/me` on it.
  useEffect(() => {
    const onFocus = () => {
      void refetchToReview(queryClient);
    };

    window.addEventListener('focus', onFocus);
    return () => {
      window.removeEventListener('focus', onFocus);
    };
  }, [queryClient]);

  return (
    <PageStatus>
      <PageHeading>{pageName.flaggedNotes}</PageHeading>
      <div className={styles.list}>
        <LoadRegion queries={[toReview]} loading={strings.flagged.loading} what={pageName.flaggedNotes}>
          {/* Called only once the query has data. */}
          {() => (
            <ChronoList
              items={toReview.data!}
              getKey={keyOf}
              emptyText={strings.flagged.noneToReview}
              renderRow={(note) => <ToReviewRow note={note} />}
            />
          )}
        </LoadRegion>
      </div>
    </PageStatus>
  );
}

/** A row's stable key and return key: its participant and note date, as there is one note a day for each (D6). */
function keyOf(note: ToReviewNote): string {
  return `${note.participantId}/${note.noteDate}`;
}

/** The text before the first line break, whole: design.md §4.6's "first line of the flag reason". */
function firstLine(text: string): string {
  return text.split(/\r\n?|\n/, 1)[0];
}

/**
 * A To review row (flagged.md item 4): one link, to the shared note address, with no router state, named by the
 * participant's full name and, after a pause, the note date on the next line; described by its meta line, which has
 * no status word, as the list names the state: the author's display name as stored, then "flagged" and when, the time
 * alone when the flag fell on the note date in Melbourne, else the date and time; then, as plain lines read after it,
 * the reason up to its first line break, never cut short, and "Flag removed in a later edit" when the current version
 * is not flagged. No tag and no button.
 */
function ToReviewRow({ note }: { note: ToReviewNote }) {
  const flagged = stamp(note.flaggedAtUtc, parseDateOnly(note.noteDate));
  const reason = msg.flagReason(firstLine(note.flagReason));

  return (
    <LinkRow
      look="date"
      to={`/participants/${note.participantId}/notes/${note.noteDate}`}
      name={
        <>
          {fullName(note)}
          <Pause />{' '}
          <time className={styles.noteDate} dateTime={note.noteDate}>
            {dateLong(parseDateOnly(note.noteDate))}
          </time>
        </>
      }
      returnKey={keyOf(note)}
      detail={[
        <MetaLine
          statusFirst={false}
          parts={[
            note.authorDisplayName,
            <>
              {strings.flagged.flagged} <time dateTime={note.flaggedAtUtc}>{flagged}</time>
            </>,
          ]}
        />,
      ]}
      extra={note.flagRemovedLater ? [reason, strings.flagged.flagRemovedLater] : [reason]}
    />
  );
}
