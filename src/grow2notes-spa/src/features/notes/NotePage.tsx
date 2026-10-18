import { useQuery } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { useParams } from 'react-router';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { Notice } from '../../components/Notice.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import textBox from '../../components/TextBox.module.css';
import { TickListField } from '../../components/TickListField.tsx';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { dateLong, parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { fullName } from '../participants/participantsQuery.ts';
import { GuidedNotes } from './GuidedNotes.tsx';
import { draftQuery, participantQuery, type Draft, type DraftAnswer, type ParticipantHeader } from './noteQueries.ts';
import styles from './NotePage.module.css';
import { SaveIndicatorText, type SaveIndicator } from './SaveIndicator.tsx';
import { useAutosave } from './useAutosave.ts';

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
 * first change: opening, scrolling, focusing or tapping into the form saves nothing (design.md §3.3).
 */
function Note({ participantId, noteDate }: NoteProps) {
  const participant = useQuery(participantQuery(participantId));
  const draft = useQuery(draftQuery(participantId, noteDate));
  // None until the form mounts and gives the words it opens with.
  const [indicator, setIndicator] = useState<SaveIndicator>();

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
              onIndicator={setIndicator}
            />
          )}
        </LoadRegion>
      </div>
    </PageStatus>
  );
}

type LoadedProps = {
  participant: ParticipantHeader;
  answer: DraftAnswer;
  noteDate: string;
  onIndicator: (indicator: SaveIndicator) => void;
};

/**
 * The note once loaded: the identity `<h1>` (note-identity-header.md), which takes the focus left pending for it on
 * arrival, the name as stored, never translated, then the note date, so that a screen reader reads the person and the
 * day together; then the form, or, for another user's draft, who started it and when, in a notice, and no form
 * (note-form.md *Loading and load errors*), worded for today's note, or for an earlier day's. It keeps the note as it
 * first loaded, so that nothing fetched later, as after signing in again in place, replaces what is on screen
 * (note-form.md *Autosave*).
 */
function Loaded({ participant, answer, noteDate, onIndicator }: LoadedProps) {
  const me = useMe();
  const [opened] = useState(answer);
  const name = fullName(participant);
  const date = parseDateOnly(noteDate);

  return (
    <>
      <PageHeading>
        <span className={styles.name} translate="no">
          {name}
        </span>{' '}
        <time className={styles.date} dateTime={noteDate}>
          {dateLong(date)}
        </time>
      </PageHeading>
      {'startedBy' in opened ? (
        <Notice>
          {apiErrorCopy['note.in_progress_by_other']({
            ...opened.startedBy,
            participantName: name,
            noteDate: date,
            onToday: noteDate >= me.today,
          })}
        </Notice>
      ) : (
        <NoteForm
          participantId={participant.id}
          noteDate={noteDate}
          givenName={participant.givenName}
          draft={opened.draft}
          onIndicator={onIndicator}
        />
      )}
    </>
  );
}

type NoteFormProps = {
  participantId: string;
  noteDate: string;
  givenName: string;
  draft: Draft;
  onIndicator: (indicator: SaveIndicator) => void;
};

/**
 * The note form (note-form.md components 7, 8 and 10): the lists-changed notice's live region, there and empty from
 * the first render, as a screen reader may not announce words written into one added with them; then Goals, its
 * ticks in this page's state, from the draft's; then Guided notes. Until T13 adds "2. Common items", the sections go
 * from 1 to 3, keeping design.md's numbers. Each tick and each input is a change that autosave saves, and a field
 * that loses focus sends one at once (design.md §3.4).
 */
function NoteForm({ participantId, noteDate, givenName, draft, onIndicator }: NoteFormProps) {
  const box = useRef<HTMLTextAreaElement>(null);
  const [ticked, setTicked] = useState(
    () => new Set(draft.goals.filter((goal) => goal.isTicked).map((goal) => goal.id)),
  );
  const autosave = useAutosave({
    participantId,
    noteDate,
    opened: draft,
    read: () => ({ narrative: box.current!.value, tickedGoalIds: [...ticked] }),
    onIndicator,
  });

  function toggle(goalId: string, isTicked: boolean) {
    setTicked((current) => {
      const next = new Set(current);
      if (isTicked) {
        next.add(goalId);
      } else {
        next.delete(goalId);
      }

      return next;
    });
    autosave.changed();
  }

  return (
    // React's onBlur bubbles, as focusout does, so it hears every field of the form.
    <div onBlur={autosave.flush}>
      <div role="status" />
      <TickListField
        idPrefix="goal"
        className={styles.section}
        heading={strings.note.goals}
        hint={strings.note.goalsHint}
        emptyText={msg.noGoalsSetUp(givenName)}
        items={draft.goals}
        ticked={ticked}
        onToggle={toggle}
      />
      <GuidedNotes
        boxRef={box}
        initialText={draft.narrative}
        prompts={draft.guidePrompts}
        onChange={autosave.changed}
      />
    </div>
  );
}
