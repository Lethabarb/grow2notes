import type { ReactNode } from 'react';
import { MetaLine } from '../../components/MetaLine.tsx';
import { time } from '../../copy/format.ts';
import { strings } from '../../copy/strings.ts';
import type { TodayNote } from './todayQuery.ts';

/**
 * A row's status line on Today (today.md component 8), which describes the row's link: the status word; who has the
 * note, "You" for the signed-in user's own, else the author's display name as stored; and when, the author's last save
 * of their own draft, the start of anyone else's, or a submitted note's first submitted time. Then, on a submitted
 * note whose current version is flagged, the Flagged tag, never on a draft, whose flag nobody else sees (design.md
 * §3.6). Each time is Melbourne's, alone, as Today's notes are today's (microcopy.md §3, *Reference day*).
 */
export function NoteStatus({ note }: { note: TodayNote }) {
  return (
    <MetaLine
      parts={[
        strings.noteStatus[note.status],
        note.isMine ? strings.statusLine.you : note.authorDisplayName,
        when(note),
      ]}
      tags={note.status === 'Submitted' && note.isFlagged ? ['flagged'] : []}
    />
  );
}

function when(note: TodayNote): ReactNode {
  if (note.status === 'Submitted') {
    return <At utc={note.firstSubmittedAtUtc} />;
  }

  return note.isMine ? (
    <>
      {strings.statusLine.saved} <At utc={note.savedAtUtc} />
    </>
  ) : (
    <>
      {strings.statusLine.started} <At utc={note.startedAtUtc} />
    </>
  );
}

function At({ utc }: { utc: string }) {
  return <time dateTime={utc}>{time(utc)}</time>;
}
