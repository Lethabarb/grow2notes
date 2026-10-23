import { useQuery } from '@tanstack/react-query';
import { useRef, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useMe } from '../../api/me.ts';
import { BackLink } from '../../components/BackLink.tsx';
import { Button } from '../../components/Button.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { Notice } from '../../components/Notice.tsx';
import { PageBar } from '../../components/PageBar.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import textInput from '../../components/textInput.module.css';
import { dayBefore } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { Pause } from '../../copy/Pause.tsx';
import { strings } from '../../copy/strings.ts';
import { useBackTo } from '../../routes/routeChange.ts';
import { participantQuery, type ParticipantHeader } from '../notes/noteQueries.ts';
import noteStyles from '../notes/NotePage.module.css';
import { fullName } from './participantsQuery.ts';
import styles from './WritePastDayNotePage.module.css';

const fieldId = 'past-day-date';

/**
 * Write past-day note at `/manage/participants/{participantId}/past-day-note` (participants.md C), keyed by the
 * participant, so that nothing of one participant's page is ever kept for another's.
 */
export function WritePastDayNotePage() {
  const { participantId = '' } = useParams();
  return <WritePastDayNote key={participantId} participantId={participantId} />;
}

/**
 * The back link in a static top bar, to the screen that opened it, "Participant" or "Past notes", else "Participant";
 * then one region that loads the participant (the `<h1>`'s caption is their name), and, once loaded, a one-field form,
 * or, for an archived participant, only the notice that no new notes can be written for them.
 */
function WritePastDayNote({ participantId }: { participantId: string }) {
  const participant = useQuery(participantQuery(participantId));
  const back = useBackTo(
    { participant: pageName.participant, pastNotes: pageName.pastNotes },
    { to: `/manage/participants/${participantId}`, label: pageName.participant },
  );

  return (
    <PageStatus>
      <PageBar>
        <BackLink to={back.to}>{back.label}</BackLink>
      </PageBar>
      <div className={styles.column}>
        <LoadRegion
          queries={[participant]}
          loading={strings.participants.participantLoading}
          what={strings.participants.thisParticipant}
          fallbackHeading={pageName.writePastDayNote}
        >
          {/* Called only once the participant has loaded. */}
          {() => <Loaded participant={participant.data!} />}
        </LoadRegion>
      </div>
    </PageStatus>
  );
}

/**
 * The page once loaded: the `<h1>` holding the participant's name as stored, never translated, as its caption, then
 * the form. Continue only goes to the note: it checks the date in code, as `max` is only a hint to the picker, and the
 * server stays the gate, and it replaces this page's entry, so that the note's back link, and Back, go to the screen
 * that opened this page rather than to an empty date field (participants.md C). Nothing is created until the note's
 * first change (design.md §3.3).
 */
function Loaded({ participant }: { participant: ParticipantHeader }) {
  const me = useMe();
  const navigate = useNavigate();
  const field = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string>();
  const name = fullName(participant);

  // A year of five or more digits, which a browser lets a person type, sorts before 2026 as text but is long after it.
  function problemWith(value: string): string | undefined {
    if (value === '') {
      return strings.pastDayNote.enterTheNoteDate;
    }

    return value >= me.today || value.length > me.today.length ? strings.pastDayNote.mustBeInThePast : undefined;
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = field.current!.value;
    const found = problemWith(value);
    setError(found);
    if (found !== undefined) {
      field.current!.focus();
      return;
    }

    void navigate(`/participants/${participant.id}/notes/${value}`, { replace: true });
  }

  return (
    <>
      <PageHeading>
        <span className={noteStyles.caption} translate="no">
          {name}
        </span>
        <Pause />{' '}
        {pageName.writePastDayNote}
      </PageHeading>
      <div className={styles.body}>
        {participant.status === 'Archived' ? (
          <Notice>{msg.noNewNotesArchived(name)}</Notice>
        ) : (
          <form noValidate onSubmit={submit}>
            <FieldGroup
              fieldId={fieldId}
              label={strings.pastDayNote.noteDate}
              hint={strings.pastDayNote.hint}
              error={error}
            >
              <input
                ref={field}
                id={fieldId}
                type="date"
                className={`${textInput.input} ${styles.input}`}
                max={dayBefore(me.today)}
                autoComplete="off"
                aria-describedby={error === undefined ? `${fieldId}-hint` : `${fieldId}-error ${fieldId}-hint`}
                aria-invalid={error === undefined ? undefined : true}
                onChange={(event) => {
                  if (error !== undefined) {
                    setError(problemWith(event.currentTarget.value));
                  }
                }}
              />
            </FieldGroup>
            <div className={styles.submit}>
              <Button variant="primary" type="submit">
                {strings.pastDayNote.continue}
              </Button>
            </div>
          </form>
        )}
      </div>
    </>
  );
}
