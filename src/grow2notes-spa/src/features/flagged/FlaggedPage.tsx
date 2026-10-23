import {
  useInfiniteQuery,
  useQuery,
  useQueryClient,
  type UseInfiniteQueryResult,
  type UseQueryResult,
} from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { NavigationType, useNavigationType } from 'react-router';
import { ChronoList } from '../../components/ChronoList.tsx';
import { LinkRow } from '../../components/LinkRow.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { MetaLine } from '../../components/MetaLine.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { useView } from '../../components/useView.ts';
import { ViewLinks } from '../../components/ViewLinks.tsx';
import { dateLong, dateTime, parseDateOnly, stamp } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { Pause } from '../../copy/Pause.tsx';
import { strings } from '../../copy/strings.ts';
import { useReturnFocusReady } from '../../routes/routeChange.ts';
import { fullName } from '../participants/participantsQuery.ts';
import styles from './FlaggedPage.module.css';
import { dropReviewed, reviewedQuery, type ReviewedNote } from './reviewedQuery.ts';
import { refetchReviewLists, toReviewQuery, type ToReviewNote } from './toReviewQuery.ts';
import { useFocusKeptInList } from './useFocusKeptInList.ts';

const views = ['reviewed'] as const;

/**
 * Flagged notes at `/flagged` (flagged.md), for managers alone, inside `ManagerOnly`: its static `<h1>` at once, which
 * the shell focuses on arrival; the To review and Reviewed links, the first with the number to review once that list
 * has loaded; then the view's list in a region that loads it, with "Loading flagged notes…" or "Loading reviewed
 * notes…" after 1 s, "Flagged notes did not load: …" or "Reviewed notes did not load: …" with Try again, or Page
 * not found in place of the whole screen for a `403` or a `404`, as after a change of role. To review is asked for
 * on both views, for its number. The lists shown are asked for again on every visit, as the page becomes visible and
 * as the window regains focus, the rows already shown staying meanwhile. Once the shown view's region is ready, its
 * rows or the failure to load them in the page, the shell may focus the row opened from it on a pop back.
 *
 * A push to the page is a new visit, which starts Reviewed from the newest 30, so what an earlier visit loaded is
 * dropped before the queries read the cache; a switch of view or a pop keeps every page loaded.
 */
export function FlaggedPage() {
  const queryClient = useQueryClient();
  const action = useNavigationType();
  // Once, as the page mounts, before either query below reads the cache.
  useState(() => {
    if (action !== NavigationType.Pop) {
      dropReviewed(queryClient);
    }
  });
  const view = useView(views);
  const toReview = useQuery(toReviewQuery);
  const reviewed = useInfiniteQuery({ ...reviewedQuery, enabled: view === 'reviewed' });
  useReturnFocusReady(view === 'reviewed' ? !reviewed.isPending : !toReview.isPending);

  // TanStack Query 5 refetches on `visibilitychange` alone, which a click back into a window left visible beside
  // another never fires, so the window's `focus` fetches the lists again too, as the shell refreshes `/me` on it.
  useEffect(() => {
    const onFocus = () => {
      void refetchReviewLists(queryClient);
    };

    window.addEventListener('focus', onFocus);
    return () => {
      window.removeEventListener('focus', onFocus);
    };
  }, [queryClient]);

  return (
    <PageStatus>
      <PageHeading>{pageName.flaggedNotes}</PageHeading>
      <ViewLinks
        options={[
          {
            value: null,
            label: toReview.data === undefined ? strings.tag.toReview : msg.toReviewView(toReview.data.length),
          },
          { value: 'reviewed', label: strings.tag.reviewed },
        ]}
        current={view}
      />
      {view === 'reviewed' ? <ReviewedView reviewed={reviewed} /> : <ToReviewView toReview={toReview} />}
    </PageStatus>
  );
}

/** A To review row's stable key and return key: its participant and note date, as there is one note a day for each. */
function toReviewKeyOf(note: ToReviewNote): string {
  return `${note.participantId}/${note.noteDate}`;
}

/** A Reviewed row's stable key and return key: its review, as a note reviewed twice has two rows (D4). */
function reviewedKeyOf(note: ReviewedNote): string {
  return note.reviewId;
}

/** The To review list, kept in place when a refresh takes away the row that has focus. */
function ToReviewView({ toReview }: { toReview: UseQueryResult<ToReviewNote[]> }) {
  const keys = useMemo(() => (toReview.data ?? []).map(toReviewKeyOf), [toReview.data]);
  const kept = useFocusKeptInList(keys);

  return (
    <div className={styles.list} {...kept}>
      <LoadRegion queries={[toReview]} loading={strings.flagged.loading} what={pageName.flaggedNotes}>
        {/* Called only once the query has data. */}
        {() => (
          <ChronoList
            items={toReview.data!}
            getKey={toReviewKeyOf}
            emptyText={strings.flagged.noneToReview}
            renderRow={(note) => <ToReviewRow note={note} />}
          />
        )}
      </LoadRegion>
    </div>
  );
}

/** The Reviewed list, 30 at a time with Show older, kept in place as To review is. */
function ReviewedView({ reviewed }: { reviewed: UseInfiniteQueryResult<ReviewedNote[]> }) {
  const keys = useMemo(() => (reviewed.data ?? []).map(reviewedKeyOf), [reviewed.data]);
  const kept = useFocusKeptInList(keys);

  return (
    <div className={styles.list} {...kept}>
      <LoadRegion
        queries={[reviewed]}
        loading={strings.flagged.loadingReviewed}
        what={strings.flagged.reviewedNotes}
      >
        {/* Called only once the query has data. */}
        {() => (
          <ChronoList
            items={reviewed.data!}
            getKey={reviewedKeyOf}
            emptyText={strings.flagged.noneReviewed}
            older={reviewed}
            renderRow={(note) => <ReviewedRow note={note} />}
          />
        )}
      </LoadRegion>
    </div>
  );
}

/** The text before the first line break, whole: design.md §4.6's "first line of the flag reason". */
function firstLine(text: string): string {
  return text.split(/\r\n?|\n/, 1)[0];
}

/** A row link's text: the participant's full name and, after a pause, the note date on the next line. */
function NoteRowName({ note }: { note: Pick<ToReviewNote, 'givenName' | 'familyName' | 'noteDate'> }) {
  return (
    <>
      {fullName(note)}
      <Pause />{' '}
      <time className={styles.noteDate} dateTime={note.noteDate}>
        {dateLong(parseDateOnly(note.noteDate))}
      </time>
    </>
  );
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
      name={<NoteRowName note={note} />}
      returnKey={toReviewKeyOf(note)}
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

/**
 * A Reviewed row (flagged.md item 4, as the To review row): the same link and a meta line with no status word, the
 * author's display name as stored, then "reviewed by Jo Smith on Tue 29 Sep 2026, 9:30 am", as the panel's record
 * says it; then, as plain lines, the reason of the flag that was cleared, up to its first line break, and the comment,
 * as written, with its line breaks, when there is one.
 */
function ReviewedRow({ note }: { note: ReviewedNote }) {
  const reason = msg.flagReason(firstLine(note.flagReason));

  return (
    <LinkRow
      look="date"
      to={`/participants/${note.participantId}/notes/${note.noteDate}`}
      name={<NoteRowName note={note} />}
      returnKey={reviewedKeyOf(note)}
      detail={[
        <MetaLine
          statusFirst={false}
          parts={[
            note.authorDisplayName,
            <>
              {strings.flagged.reviewed} {msg.reviewedBy(note.reviewedBy)}{' '}
              <time dateTime={note.reviewedAtUtc}>{dateTime(note.reviewedAtUtc)}</time>
            </>,
          ]}
        />,
      ]}
      extra={
        note.comment === null
          ? [reason]
          : [reason, <span className={styles.comment}>{msg.reviewComment(note.comment)}</span>]
      }
    />
  );
}
