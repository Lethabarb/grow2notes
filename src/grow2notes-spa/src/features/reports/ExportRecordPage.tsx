import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useParams } from 'react-router';
import { ApiError, classify } from '../../api/api.ts';
import { refreshMe, useMe } from '../../api/me.ts';
import { useDownload, type FileFormat } from '../../api/useDownload.ts';
import { Button } from '../../components/Button.tsx';
import choiceRow from '../../components/ChoiceRow.module.css';
import { ChoiceRow } from '../../components/ChoiceRow.tsx';
import { ErrorSummary, type FormError } from '../../components/ErrorSummary.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import textInput from '../../components/textInput.module.css';
import tickList from '../../components/TickList.module.css';
import { useDelayed } from '../../components/useDelayed.ts';
import { isIsoDate, parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { participantQuery, type ParticipantHeader } from '../notes/noteQueries.ts';
import { fullName } from '../participants/participantsQuery.ts';
import styles from './ExportRecordPage.module.css';

type Field = 'from' | 'to' | 'format';

type Problems = Partial<Record<Field, string>>;

/** The fields in page order, with the id each error links to: for Format, the first radio. */
const fields: readonly { field: Field; id: string }[] = [
  { field: 'from', id: 'export-from' },
  { field: 'to', id: 'export-to' },
  { field: 'format', id: 'export-format' },
];

/**
 * Export record at `/manage/participants/:participantId/export` (record-export.md), for managers alone, inside
 * `ManagerOnly`, keyed by the participant so that nothing of one participant's form is ever kept for another's.
 */
export function ExportRecordPage() {
  const { participantId = '' } = useParams();
  return <ExportRecord key={participantId} participantId={participantId} />;
}

/** The region that loads the participant, and, once it has, the form. */
function ExportRecord({ participantId }: { participantId: string }) {
  const { today } = useMe();
  const participant = useQuery(participantQuery(participantId));

  return (
    <PageStatus>
      <div className={styles.column}>
        <LoadRegion
          queries={[participant]}
          loading={strings.participants.participantLoading}
          what={strings.exportRecord.theForm}
          fallbackHeading={pageName.exportRecord}
        >
          {/* Called only once the participant has loaded. */}
          {() => <ExportForm participant={participant.data!} today={today} />}
        </LoadRegion>
      </div>
    </PageStatus>
  );
}

// What is wrong with the form, in the words under each field, checked only when Export is pressed. A To date after
// today is no fault: the server takes it as sent (design.md A60).
function exportProblems(from: string, to: string, format: FileFormat | undefined): Problems {
  const found: Problems = {};
  const fromOk = isIsoDate(from);
  if (!fromOk) {
    found.from = strings.exportRecord.enterFromDate;
  }

  if (!isIsoDate(to)) {
    found.to = strings.exportRecord.enterToDate;
  } else if (fromOk && to < from) {
    found.to = msg.toNotBeforeFrom(parseDateOnly(from));
  }

  if (format === undefined) {
    found.format = strings.exportRecord.choosePdfOrWord;
  }

  return found;
}

// The fields of a 422, in the screen's own words, or undefined when it names one that the form does not hold, which
// is no fault of the person's.
function serverProblems(names: readonly string[]): Problems | undefined {
  const found: Problems = {};
  for (const name of names) {
    if (name === 'from') {
      found.from = strings.exportRecord.enterFromDate;
    } else if (name === 'to') {
      found.to = strings.exportRecord.enterToDate;
    } else if (name === 'format') {
      found.format = strings.exportRecord.choosePdfOrWord;
    } else {
      return undefined;
    }
  }

  return names.length === 0 ? undefined : found;
}

/**
 * The form once the participant has loaded (record-export.md components 2 to 11): the error summary, the `<h1>`, the
 * participant, then From, To, Format, the earlier versions box and Export, with the alert and the status line above
 * Export. The dates start at the participant's first submitted note and today (the server's, never the device's), and
 * nothing is kept for a later visit. Nothing is checked before Export is pressed, and a request is sent only when
 * the form passes. A date changed while no export runs empties the old result, which named the old dates.
 */
function ExportForm({ participant, today }: { participant: ParticipantHeader; today: string }) {
  const queryClient = useQueryClient();
  const [from, setFrom] = useState(participant.firstNoteDate ?? today);
  const [to, setTo] = useState(today);
  const [format, setFormat] = useState<FileFormat>();
  const [includeHistory, setIncludeHistory] = useState(false);
  const [problems, setProblems] = useState<Problems>({});
  const [attempt, setAttempt] = useState(0);
  // The dates sent when the range held no notes, and whether the last result is still the form's to show.
  const [noNotes, setNoNotes] = useState<{ from: string; to: string }>();
  const [showResult, setShowResult] = useState(false);
  const download = useDownload(participant.id);
  const slow = useDelayed(download.pending !== undefined, 400);
  const name = fullName(participant);

  function changeDate(which: 'from' | 'to', value: string) {
    const nextFrom = which === 'from' ? value : from;
    const nextTo = which === 'to' ? value : to;
    if (which === 'from') {
      setFrom(value);
    } else {
      setTo(value);
    }

    if (download.pending === undefined) {
      setShowResult(false);
    }

    // A date that shows an error is checked again, so that a fix clears it at once; no other field gets one now.
    const again = exportProblems(nextFrom, nextTo, format);
    setProblems((shown) => ({ ...shown, from: shown.from && again.from, to: shown.to && again.to }));
  }

  function failed(cause: unknown, sentFrom: string, sentTo: string): string | undefined {
    if (cause instanceof ApiError) {
      if (cause.code === 'report.no_notes') {
        setNoNotes({ from: sentFrom, to: sentTo });
        return undefined;
      }

      const named =
        cause.status === 422 && cause.errors !== undefined ? serverProblems(Object.keys(cause.errors)) : undefined;
      if (named !== undefined) {
        setProblems(named);
        setAttempt((n) => n + 1);
        return undefined;
      }

      if (cause.status === 429) {
        return strings.exportRecord.tooManyExports;
      }

      if (cause.status === 403) {
        // No longer a manager: the shell's role guard shows Page not found once `/me` says so.
        refreshMe(queryClient).catch(() => {});
        return undefined;
      }
    }

    // A 401 has the session flow, which `api()` started, and shows nothing here; nothing is sent again after it.
    const failure = classify(cause);
    return failure === 'signedOut' ? undefined : msg.notDone('exported', failure);
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (download.pending !== undefined) {
      return;
    }

    const found = exportProblems(from, to, format);
    setProblems(found);
    setAttempt((n) => n + 1);
    if (format === undefined || Object.keys(found).length > 0) {
      return;
    }

    // The values at the press: the request outlasts any edit the person makes while it runs.
    const sentFrom = from;
    const sentTo = to;
    setNoNotes(undefined);
    setShowResult(true);
    const query = new URLSearchParams({ from: sentFrom, to: sentTo, format, includeHistory: String(includeHistory) });
    download.start({
      scope: participant.id,
      format,
      path: `/api/reports/participants/${participant.id}/export?${query}`,
      fallbackName: `participant-record_${sentFrom}_to_${sentTo}.${format}`,
      failed: (cause) => failed(cause, sentFrom, sentTo),
    });
  }

  function statusLine(): string | undefined {
    if (slow && download.pending !== undefined) {
      return strings.report.preparing[download.pending];
    }

    if (!showResult) {
      return undefined;
    }

    if (download.savedName !== undefined) {
      return msg.fileReady(download.savedName);
    }

    return noNotes === undefined
      ? undefined
      : msg.noNotesInRange(name, parseDateOnly(noNotes.from), parseDateOnly(noNotes.to));
  }

  const summary: FormError[] = fields.flatMap(({ field, id }) => {
    const message = problems[field];
    return message === undefined ? [] : [{ message, fieldId: id }];
  });

  return (
    <>
      <ErrorSummary errors={summary} attempt={attempt} />
      <PageHeading>{pageName.exportRecord}</PageHeading>
      <dl className={styles.participant}>
        <dt>{strings.exportRecord.participant}</dt>
        <dd translate="no">{name}</dd>
      </dl>
      <form noValidate className={styles.form} onSubmit={submit}>
        <FieldGroup fieldId="export-from" label={strings.exportRecord.fromDate} error={problems.from}>
          <input
            id="export-from"
            name="from"
            type="date"
            className={`${textInput.input} ${styles.date}`}
            max={today}
            value={from}
            autoComplete="off"
            aria-invalid={problems.from === undefined ? undefined : true}
            aria-describedby={problems.from === undefined ? undefined : 'export-from-error'}
            onChange={(event) => {
              changeDate('from', event.currentTarget.value);
            }}
          />
        </FieldGroup>
        <FieldGroup fieldId="export-to" label={strings.exportRecord.toDate} error={problems.to}>
          <input
            id="export-to"
            name="to"
            type="date"
            className={`${textInput.input} ${styles.date}`}
            min={isIsoDate(from) ? from : undefined}
            max={today}
            value={to}
            autoComplete="off"
            aria-invalid={problems.to === undefined ? undefined : true}
            aria-describedby={problems.to === undefined ? undefined : 'export-to-error'}
            onChange={(event) => {
              changeDate('to', event.currentTarget.value);
            }}
          />
        </FieldGroup>
        <FieldGroup group fieldId="export-format" label={strings.exportRecord.format} error={problems.format}>
          <ChoiceRow
            id="export-format"
            name="format"
            label={strings.exportRecord.pdf}
            hint={strings.exportRecord.pdfHint}
            checked={format === 'pdf'}
            onChange={() => {
              setFormat('pdf');
              setProblems((shown) => ({ ...shown, format: undefined }));
            }}
          />
          <ChoiceRow
            id="export-format-docx"
            name="format"
            label={strings.exportRecord.word}
            hint={strings.exportRecord.wordHint}
            checked={format === 'docx'}
            onChange={() => {
              setFormat('docx');
              setProblems((shown) => ({ ...shown, format: undefined }));
            }}
          />
        </FieldGroup>
        <div>
          <label htmlFor="export-includeHistory" className={tickList.row}>
            <input
              id="export-includeHistory"
              type="checkbox"
              className={tickList.box}
              checked={includeHistory}
              aria-describedby="export-includeHistory-hint"
              onChange={(event) => {
                setIncludeHistory(event.currentTarget.checked);
              }}
            />
            <span className={tickList.text}>{strings.exportRecord.includeHistory}</span>
          </label>
          <p id="export-includeHistory-hint" className={choiceRow.hint}>
            {strings.exportRecord.includeHistoryHint}
          </p>
        </div>
        <div className={styles.actions}>
          <RequestAlert id="export-alert">{download.failure}</RequestAlert>
          <p role="status" id="export-status" className={styles.status} data-preparing={slow || undefined}>
            {statusLine()}
          </p>
          <Button
            type="submit"
            variant="primary"
            busy={download.pending !== undefined}
            busyLabel={strings.exportRecord.exporting}
            announceBusy={false}
          >
            {strings.exportRecord.export}
          </Button>
        </div>
      </form>
    </>
  );
}
