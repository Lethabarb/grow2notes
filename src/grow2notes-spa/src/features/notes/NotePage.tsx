import { useQuery } from '@tanstack/react-query';
import { useRef, useState, type Ref } from 'react';
import { useParams } from 'react-router';
import type { StartedBy } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { ButtonGroup } from '../../components/ButtonGroup.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import textBox from '../../components/TextBox.module.css';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { dateLong, parseDateOnly } from '../../copy/format.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { useBackTo } from '../../routes/routeChange.ts';
import { fullName } from '../participants/participantsQuery.ts';
import {
  draftQuery,
  noteQuery,
  participantQuery,
  type DraftAnswer,
  type NoteAnswer,
  type ParticipantHeader,
} from './noteQueries.ts';
import { NoteForm, type NoteFormHandle } from './NoteForm.tsx';
import styles from './NotePage.module.css';
import { ReadView } from './ReadView.tsx';
import { SaveIndicatorText, type SaveIndicator } from './SaveIndicator.tsx';
import { SomeoneElsesDraft } from './SomeoneElsesDraft.tsx';
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
 * The note's page at the shared note address (note-form.md *Routes*), in the form's 40rem column, which decides what it
 * shows from fresh data on every visit. Its top bar shows at once, with the back link to Past notes or to Flagged when
 * that list opened it, else to Today, static, as nothing on the read view is sticky (participant-notes.md *Conflicts
 * resolved* 1). Under it, one region loads the participant and the note, `GET {base}`, together, then, for a note that
 * is not submitted, the form's own read. It shows nothing until all it needs have loaded, as an empty box or unticked
 * rows could be tapped, or read as nothing ticked, before the name above them is known, and "Loading note…" after 1 s;
 * or, when a read failed, the fallback `<h1>` "Note" with the failure sentence and Try again; or Page not found in
 * place of the whole screen for a `404`, such as another organisation's participant or a date after today. Then it
 * shows the read view for a submitted note, and otherwise what the form's read gives. Nothing is sent but those reads
 * until the form's first change: opening, scrolling, focusing or tapping into the form saves nothing (design.md §3.3).
 *
 * The form, once open, makes the top bar sticky, with the save status at its end, and stays open whatever the note is
 * fetched as later, as after signing in again in place, so that nothing on screen is replaced (note-form.md
 * *Autosave*). A dead end, or a takeover by another page's save, shows its banner in the top bar's second row, and says
 * its sentence once through the form's assertive region (note-form.md component 16), there and empty from the first
 * render, as a screen reader may not announce words written into one added with them; its polite twin is the page's
 * `PageStatus`.
 */
function Note({ participantId, noteDate }: NoteProps) {
  const participant = useQuery(participantQuery(participantId));
  const note = useQuery(noteQuery(participantId, noteDate));
  const form = useRef<NoteFormHandle>(null);
  // None until the form mounts and gives the words it opens with.
  const [indicator, setIndicator] = useState<SaveIndicator>();
  // The sentence of the dead end that stopped autosave, if one has.
  const [deadEnd, setDeadEnd] = useState<string>();
  const formOpen = indicator !== undefined;
  const takenOver = indicator?.kind === 'takenOver';
  const said = takenOver ? apiErrorCopy['draft.taken_over'] : deadEnd;
  const submitted = !formOpen && note.data?.status === 'Submitted' ? note.data : undefined;
  const formNeeded = note.data !== undefined && submitted === undefined;
  const draft = useQuery({ ...draftQuery(participantId, noteDate), enabled: formNeeded });
  const back = useBackTo({ pastNotes: pageName.pastNotes, flagged: strings.navItem.flagged });

  return (
    <PageStatus>
      <PageBar sticky={formOpen}>
        <BackLink to={back.to}>{back.label}</BackLink>
        {/* A new element as the form mounts, holding its first words, such as "Draft · saved 9:42 am", which a screen
            reader takes as the page's own rather than announcing a change (autosave-status.md *Behaviour* 8). */}
        {formOpen && <SaveIndicatorRegion indicator={indicator} />}
        {deadEnd !== undefined && <DeadEndBanner sentence={deadEnd} />}
        {takenOver && (
          <TakeoverBanner
            onKeep={() => {
              form.current?.keep();
            }}
            onLoad={async () => {
              await form.current?.loadOther();
            }}
          />
        )}
      </PageBar>
      <div className={textBox.column}>
        <LoadRegion
          queries={formNeeded ? [participant, note, draft] : [participant, note]}
          loading={strings.note.loading}
          what={strings.note.theNote}
          fallbackHeading={pageName.note}
        >
          {/* Called only once every query given has data. */}
          {() =>
            submitted === undefined ? (
              <Loaded
                formRef={form}
                participant={participant.data!}
                note={note.data ?? null}
                answer={draft.data!}
                noteDate={noteDate}
                indicator={indicator}
                deadEnd={deadEnd}
                onIndicator={setIndicator}
                onDeadEnd={setDeadEnd}
              />
            ) : (
              <ReadView
                heading={<NoteHeading name={fullName(participant.data!)} noteDate={noteDate} />}
                participantId={participantId}
                note={submitted}
                noteDate={noteDate}
              />
            )
          }
        </LoadRegion>
      </div>
      <div role="alert" className="visually-hidden">
        {/* Keyed by the state, so that the version-conflict dead end that Load can meet is said, though its words are
            the takeover's, which React would otherwise leave in place, unchanged and unannounced. */}
        {said !== undefined && <span key={takenOver ? 'taken-over' : 'dead-end'}>{said}</span>}
      </div>
    </PageStatus>
  );
}

/**
 * The save indicator (note-form.md component 1), at the end of the top bar's first row: its words in one live region,
 * read whole as they change. A failed save that is being retried makes it the bar's second row, full width under the
 * back link, in the attention colours with the warning icon, so that its sentence shows, and is said, once.
 */
function SaveIndicatorRegion({ indicator }: { indicator: SaveIndicator }) {
  const failed = indicator.kind === 'failed';
  return (
    <p
      id="save-status"
      role="status"
      aria-live="polite"
      aria-atomic="true"
      className={failed ? `${styles.saveStatus} ${styles.attentionRow}` : styles.saveStatus}
    >
      {failed ? (
        <>
          <WarningIcon />
          <span className={styles.attentionText}>
            <SaveIndicatorText indicator={indicator} />
          </span>
        </>
      ) : (
        <SaveIndicatorText indicator={indicator} />
      )}
    </p>
  );
}

/**
 * A dead end's banner, the top bar's second row (note-form.md component 1), full width under the back link and the
 * save status, in the attention colours: a region named by its sentence, which says why the note cannot be saved
 * here, and is not itself a live region.
 */
function DeadEndBanner({ sentence }: { sentence: string }) {
  return (
    <section className={styles.attentionRow} aria-labelledby="dead-end">
      <WarningIcon />
      <p id="dead-end" className={styles.attentionText}>
        {sentence}
      </p>
    </section>
  );
}

type TakeoverBannerProps = {
  onKeep: () => void;
  /** Settles once Load's read is answered. */
  onLoad: () => Promise<void>;
};

/**
 * The takeover banner, the top bar's second row in place of a failed save's (note-form.md component 1), while another
 * page's save has the note: as a dead end's banner, a region named by its sentence, then its two choices, Keep the text
 * on this screen first, as the one that loses nothing on this screen, and Load the other version (autosave-status.md
 * *Behaviour* 9), stacked full width on a phone and side by side from 40rem, wrapping. Both wait while Load's read is
 * out, ready again if it fails.
 */
function TakeoverBanner({ onKeep, onLoad }: TakeoverBannerProps) {
  const [loading, setLoading] = useState(false);
  return (
    <section className={`${styles.attentionRow} ${styles.takeover}`} aria-labelledby="taken-over">
      <WarningIcon />
      <p id="taken-over" className={styles.attentionText}>
        {apiErrorCopy['draft.taken_over']}
      </p>
      <div className={styles.choices}>
        <ButtonGroup>
          <Button variant="primary" busy={loading} onClick={onKeep}>
            {strings.note.keepTheText}
          </Button>
          <Button
            busy={loading}
            onClick={() => {
              setLoading(true);
              void onLoad().finally(() => {
                setLoading(false);
              });
            }}
          >
            {strings.note.loadTheOtherVersion}
          </Button>
        </ButtonGroup>
      </div>
    </section>
  );
}

/** The warning icon of the top bar's second row, beside the first line of its words, which carry the meaning. */
function WarningIcon() {
  return (
    <svg className={styles.attentionIcon} viewBox="0 0 20 20" aria-hidden="true" focusable="false">
      <circle cx="10" cy="10" r="8.75" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <path d="M10 5v6.5" fill="none" stroke="currentColor" strokeWidth="2" />
      <circle cx="10" cy="14.5" r="1.25" fill="currentColor" />
    </svg>
  );
}

/**
 * The note's identity `<h1>` (note-identity-header.md), on the form and the read view alike, which takes the focus
 * left pending for it on arrival: the name as stored, never translated, then the note date, so that a screen reader
 * reads the person and the day together, as "Jane Citizen Thursday 1 October 2026".
 */
function NoteHeading({ name, noteDate }: { name: string; noteDate: string }) {
  return (
    <PageHeading>
      <span className={styles.name} translate="no">
        {name}
      </span>{' '}
      <time className={styles.date} dateTime={noteDate}>
        {dateLong(parseDateOnly(noteDate))}
      </time>
    </PageHeading>
  );
}

type LoadedProps = {
  /** The form's, for the takeover banner in the top bar. */
  formRef: Ref<NoteFormHandle>;
  participant: ParticipantHeader;
  /** What `GET {base}` found, which holds another user's draft's working copy for a manager. */
  note: NoteAnswer;
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
 * A note that is not submitted, once loaded: the form, which puts its error summary above the `<h1>`, or, for another
 * user's draft, which the form's read refuses, the `<h1>` and who started it and when, worded for today's note, or
 * for an earlier day's, as the first save's dead end words it too, and no form (note-form.md *Loading and load
 * errors*), with the draft read-only for a manager, from `GET {base}`'s working copy. A note started between the two
 * reads has none, and shows as it does to a worker. It keeps the note as it first loaded, so that nothing fetched
 * later, as after signing in again in place, replaces what is on screen (note-form.md *Autosave*).
 */
function Loaded({
  formRef,
  participant,
  note,
  answer,
  noteDate,
  indicator,
  deadEnd,
  onIndicator,
  onDeadEnd,
}: LoadedProps) {
  const me = useMe();
  const [found] = useState(note);
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

  const heading = <NoteHeading name={name} noteDate={noteDate} />;

  return 'startedBy' in opened ? (
    <SomeoneElsesDraft
      heading={heading}
      participantId={participant.id}
      noteDate={noteDate}
      name={name}
      startedBy={opened.startedBy}
      sentence={startedBySentence(opened.startedBy)}
      content={found?.status === 'Draft' ? found.current : undefined}
    />
  ) : (
    <NoteForm
      ref={formRef}
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
