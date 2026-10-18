import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useParams } from 'react-router';
import type { StartedBy } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { Notice } from '../../components/Notice.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import textBox from '../../components/TextBox.module.css';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { dateLong, parseDateOnly } from '../../copy/format.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { fullName } from '../participants/participantsQuery.ts';
import { draftQuery, participantQuery, type DraftAnswer, type ParticipantHeader } from './noteQueries.ts';
import { NoteForm } from './NoteForm.tsx';
import styles from './NotePage.module.css';
import { SaveIndicatorText, type SaveIndicator } from './SaveIndicator.tsx';
import type { DeadEnd } from './useAutosave.ts';

/**
 * A participant's note for a day at `/participants/{participantId}/notes/{noteDate}` (note-form.md *Routes*), keyed by
 * both, so that nothing of one participant's note is ever kept on the page for another's (identity item 1).
 */
export function NotePage() {
  const { participantId = '', noteDate = '' } = useParams();
  return <Note key={`${participantId}/${noteDate}`} participantId={participantId} noteDate={noteDate} />;
}

type NoteProps = { participantId: string; noteDate: string };

/**
 * The note screen (note-form.md), in the form's 40rem column: its top bar at once, with the back link to Today and
 * the save status, there and empty from the first render; then the region that loads the participant and the note
 * together, which shows nothing of either until both have loaded, as an empty box or unticked rows could be tapped
 * before the name above them is known, then "Loading note…" after 1 s; or, when either did not load, the fallback
 * `<h1>` "Note" with the failure sentence and Try again; or Page not found in place of the whole screen for a `404`,
 * such as another organisation's participant or a date after today. Nothing is sent but those two requests until the
 * first change: opening, scrolling, focusing or tapping into the form saves nothing (design.md §3.3). A dead end shows
 * its sentence in the top bar's second row, and says it once through the form's assertive region (note-form.md
 * component 16), there and empty from the first render, as a screen reader may not announce words written into one
 * added with them; its polite twin is the page's `PageStatus`.
 */
function Note({ participantId, noteDate }: NoteProps) {
  const participant = useQuery(participantQuery(participantId));
  const draft = useQuery(draftQuery(participantId, noteDate));
  // None until the form mounts and gives the words it opens with.
  const [indicator, setIndicator] = useState<SaveIndicator>();
  // The sentence of the dead end that stopped autosave, if one has.
  const [deadEnd, setDeadEnd] = useState<string>();

  return (
    <PageStatus>
      <PageBar sticky>
        <BackLink to="/">{pageName.today}</BackLink>
        {/* A new element as the form mounts, holding its first words, such as "Draft · saved 9:42 am", which a screen
            reader takes as the page's own rather than announcing a change (autosave-status.md *Behaviour* 8). */}
        <p
          key={indicator === undefined ? 'page' : 'form'}
          id="save-status"
          role="status"
          aria-live="polite"
          aria-atomic="true"
          className={styles.saveStatus}
        >
          {indicator !== undefined && <SaveIndicatorText indicator={indicator} />}
        </p>
        {deadEnd !== undefined && <DeadEndBanner sentence={deadEnd} />}
      </PageBar>
      <div className={textBox.column}>
        <LoadRegion
          queries={[participant, draft]}
          loading={strings.note.loading}
          what={strings.note.theNote}
          fallbackHeading={pageName.note}
        >
          {/* Called only once both queries have data. */}
          {() => (
            <Loaded
              participant={participant.data!}
              answer={draft.data!}
              noteDate={noteDate}
              indicator={indicator}
              deadEnd={deadEnd}
              onIndicator={setIndicator}
              onDeadEnd={setDeadEnd}
            />
          )}
        </LoadRegion>
      </div>
      <div role="alert" className="visually-hidden">
        {deadEnd}
      </div>
    </PageStatus>
  );
}

/**
 * A dead end's banner, the top bar's second row (note-form.md component 1), full width under the back link and the
 * save status, in the attention colours: a region named by its sentence, which says why the note cannot be saved
 * here, and is not itself a live region. Its border is a real one, so that forced colours keep it.
 */
function DeadEndBanner({ sentence }: { sentence: string }) {
  return (
    <section className={styles.deadEnd} aria-labelledby="dead-end">
      <svg className={styles.deadEndIcon} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
        <circle cx="10" cy="10" r="8.75" fill="none" stroke="currentColor" strokeWidth="1.5" />
        <path d="M10 5v6.5" fill="none" stroke="currentColor" strokeWidth="2" />
        <circle cx="10" cy="14.5" r="1.25" fill="currentColor" />
      </svg>
      <p id="dead-end" className={styles.deadEndText}>
        {sentence}
      </p>
    </section>
  );
}

type LoadedProps = {
  participant: ParticipantHeader;
  answer: DraftAnswer;
  noteDate: string;
  /** What the save indicator says, none until the form first says it. */
  indicator: SaveIndicator | undefined;
  /** The sentence of the dead end that stopped autosave, if one has. */
  deadEnd: string | undefined;
  onIndicator: (indicator: SaveIndicator) => void;
  /** The sentence of a dead end that stopped autosave. */
  onDeadEnd: (sentence: string) => void;
};

/**
 * The note once loaded: the identity `<h1>` (note-identity-header.md), which takes the focus left pending for it on
 * arrival, the name as stored, never translated, then the note date, so that a screen reader reads the person and the
 * day together; then the form, which puts its error summary above the `<h1>`, or, for another user's draft, who started
 * it and when, in a notice, and no form (note-form.md *Loading and load errors*), worded for today's note, or for an
 * earlier day's, as the first save's dead end words it too. It keeps the note as it first loaded, so that nothing
 * fetched later, as after signing in again in place, replaces what is on screen (note-form.md *Autosave*).
 */
function Loaded({ participant, answer, noteDate, indicator, deadEnd, onIndicator, onDeadEnd }: LoadedProps) {
  const me = useMe();
  const [opened] = useState(answer);
  const name = fullName(participant);
  const date = parseDateOnly(noteDate);

  function startedBySentence(startedBy: StartedBy): string {
    return apiErrorCopy['note.in_progress_by_other']({
      ...startedBy,
      participantName: name,
      noteDate: date,
      onToday: noteDate >= me.today,
    });
  }

  function deadEndSentence(stopped: DeadEnd): string {
    switch (stopped.code) {
      case 'note.past_date_manager_only':
        return apiErrorCopy['note.past_date_manager_only'];
      case 'note.participant_archived':
        return apiErrorCopy['note.participant_archived'](name);
      case 'note.version_conflict':
        return apiErrorCopy['note.version_conflict'];
      case 'note.in_progress_by_other':
        return startedBySentence(stopped.startedBy);
    }
  }

  const heading = (
    <PageHeading>
      <span className={styles.name} translate="no">
        {name}
      </span>{' '}
      <time className={styles.date} dateTime={noteDate}>
        {dateLong(date)}
      </time>
    </PageHeading>
  );

  return 'startedBy' in opened ? (
    <>
      {heading}
      <Notice>{startedBySentence(opened.startedBy)}</Notice>
    </>
  ) : (
    <NoteForm
      participantId={participant.id}
      noteDate={noteDate}
      name={name}
      givenName={participant.givenName}
      draft={opened.draft}
      heading={heading}
      indicator={indicator}
      deadEnd={deadEnd}
      onIndicator={onIndicator}
      onDeadEnd={(stopped) => {
        onDeadEnd(deadEndSentence(stopped));
      }}
    />
  );
}
