import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useEffectEvent, useRef, useState, type ReactNode } from 'react';
import { Navigate, useHref, useLinkClickHandler, useNavigate, useParams } from 'react-router';
import { ApiError, classify, isNotFound } from '../../api/api.ts';
import { refreshMe, useMe } from '../../api/me.ts';
import { useDownload, type FileFormat } from '../../api/useDownload.ts';
import { Button } from '../../components/Button.tsx';
import { buttonClass } from '../../components/buttonClass.ts';
import { ButtonGroup } from '../../components/ButtonGroup.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import textInput from '../../components/textInput.module.css';
import { useDelayed } from '../../components/useDelayed.ts';
import { useOnWindowFocus } from '../../components/useOnWindowFocus.ts';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { dateLong, dayAfter, dayBefore, isIsoDate, parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { PageNotFound } from '../../routes/PageNotFound.tsx';
import styles from './DailyReportPage.module.css';
import { hasNotesQuery, type HasNotes } from './hasNotesQuery.ts';

// A report has no business lower bound (design.md A83). The floor keeps a half-typed year, such as 0202, from
// committing as a date, and is the same rule for the field and the address.
const earliestDate = '1900-01-01';

const pauseMs = 750;

const formats: readonly { format: FileFormat; label: string }[] = [
  { format: 'docx', label: strings.report.downloadWord },
  { format: 'pdf', label: strings.report.downloadPdf },
];

const isReportDate = (value: string) => isIsoDate(value) && value >= earliestDate;

/**
 * The Report screen at `/reports/daily/:date?` (daily-report.md), for managers alone, inside `ManagerOnly`. Its arrival
 * rules run in order: no date, or a date after today, is replaced by today; a date that is not a real one from 1900 is
 * Page not found; the rest is the screen. The screen is one for focus and scroll whatever its date (`screenKey`), so
 * a step, a typed date and Back keep the place.
 */
export function DailyReportPage() {
  const { date } = useParams();
  const { today } = useMe();

  if (date === undefined) {
    return <Navigate replace to={`/reports/daily/${today}`} />;
  }

  if (!isReportDate(date)) {
    return <PageNotFound />;
  }

  if (date > today) {
    return <Navigate replace to={`/reports/daily/${today}`} />;
  }

  return <DailyReport date={date} today={today} />;
}

// A step is a link, which is announced as one, and opens in a new tab. On today, Next day is the same element with no
// address: it stays focusable, and keeps focus when a step arrives on today.
function StepLink({ to, available, children }: { to: string; available: boolean; children: ReactNode }) {
  const href = useHref(to);
  const click = useLinkClickHandler<HTMLAnchorElement>(to);
  const className = `${buttonClass('secondary')} ${styles.step}`;

  if (available) {
    return (
      <a className={className} href={href} onClick={click}>
        {children}
      </a>
    );
  }

  return (
    <a className={className} role="link" aria-disabled="true" tabIndex={0} data-unavailable>
      {children}
    </a>
  );
}

function DailyReport({ date, today }: { date: string; today: string }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const input = useRef<HTMLInputElement>(null);
  // Whether this visit to the field has pushed an entry yet: one visit adds one, and the rest replace it.
  const pushedThisVisit = useRef(false);
  const [shownFor, setShownFor] = useState(date);
  const [draft, setDraft] = useState(date);
  const [error, setError] = useState<string>();

  // A new date in the address, from a step, Back or a commit, puts the field right.
  if (shownFor !== date) {
    setShownFor(date);
    setDraft(date);
    setError(undefined);
  }

  const check = useQuery(hasNotesQuery(date));
  useOnWindowFocus(() => {
    void check.refetch({ cancelRefetch: false });
  });
  const download = useDownload(date);
  const slow = useDelayed(download.pending !== undefined, 400);

  // Commits a pause in typing, which the next change, a new date in the address and leaving all clear.
  const onPause = useEffectEvent((value: string) => {
    commit(value, false);
  });
  useEffect(() => {
    if (draft === date) {
      return;
    }

    const timer = window.setTimeout(() => {
      onPause(draft);
    }, pauseMs);
    return () => window.clearTimeout(timer);
  }, [draft, date]);

  // The date the value is, or undefined with the field's error, which only an Enter, a blur or a press shows.
  function commit(value: string, explicit: boolean): string | undefined {
    const problem = problemWith(value, today);
    if (problem !== undefined) {
      if (explicit) {
        setError(problem);
      }

      return undefined;
    }

    setError(undefined);
    if (value !== date) {
      void navigate(`/reports/daily/${value}`, { replace: pushedThisVisit.current });
      pushedThisVisit.current = true;
    }

    return value;
  }

  const settled = draft === date && error === undefined;
  const empty = settled && check.data?.hasNotes === false;

  function failed(cause: unknown, target: string): string | undefined {
    if (cause instanceof ApiError) {
      if (cause.code === 'report.no_notes') {
        // The day has no notes after all: the empty state, with the buttons unavailable, in place of a failure.
        queryClient.setQueryData(hasNotesQuery(target).queryKey, { date: target, hasNotes: false } satisfies HasNotes);
        return undefined;
      }

      if (cause.status === 429) {
        return strings.report.tooManyDownloads;
      }

      if (cause.status === 403) {
        // No longer a manager: the shell's role guard shows Page not found once `/me` says so.
        refreshMe(queryClient).catch(() => {});
      }
    }

    // A 401 has the session flow, which `api()` started, and shows nothing here.
    const failure = classify(cause);
    return failure === 'signedOut' ? undefined : msg.notDone('downloaded', failure);
  }

  function press(format: FileFormat) {
    const value = input.current?.value ?? draft;
    let target = date;
    if (value !== date) {
      const committed = commit(value, true);
      if (committed === undefined) {
        input.current?.focus();
        return;
      }

      target = committed;
    } else if (empty) {
      return;
    }

    download.start({
      scope: target,
      format,
      path: `/api/reports/daily/${target}/export?format=${format}`,
      fallbackName: `daily-notes_${target}.${format}`,
      failed: (cause) => failed(cause, target),
    });
  }

  // After every hook: the manager's role changed, which a refused request is the first sign of.
  if (isNotFound(check.error)) {
    return <PageNotFound />;
  }

  function downloadStatus(): string | undefined {
    if (slow && download.pending !== undefined) {
      return strings.report.preparing[download.pending];
    }

    return download.savedName === undefined ? undefined : msg.fileReady(download.savedName);
  }

  const busy = download.pending !== undefined;
  return (
    <PageStatus>
      <PageHeading>{pageName.dailyReport}</PageHeading>
      <div className={styles.dateRow}>
        <div className={styles.field}>
          <FieldGroup fieldId="report-date" label={strings.report.date} error={error}>
            <input
              ref={input}
              id="report-date"
              type="date"
              className={textInput.input}
              max={today}
              value={draft}
              autoComplete="off"
              aria-invalid={error !== undefined || undefined}
              aria-describedby={error === undefined ? 'report-date-status' : 'report-date-error'}
              onFocus={(event) => {
                // The browser's date field has a focus stop for each part of the date. Moving between them is still
                // one visit: the event's related target is then the field itself, retargeted out of its shadow tree.
                if (!event.currentTarget.contains(event.relatedTarget)) {
                  pushedThisVisit.current = false;
                }
              }}
              onChange={(event) => {
                const value = event.currentTarget.value;
                setDraft(value);
                if (error !== undefined && problemWith(value, today) === undefined) {
                  setError(undefined);
                }
              }}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  commit(event.currentTarget.value, true);
                }
              }}
              onBlur={(event) => {
                commit(event.currentTarget.value, true);
              }}
            />
          </FieldGroup>
        </div>
        <div className={styles.steps}>
          <StepLink to={`/reports/daily/${dayBefore(date)}`} available>
            <span aria-hidden="true">{strings.report.previousGlyph}</span> {strings.report.previousDay}
          </StepLink>
          <StepLink to={`/reports/daily/${dayAfter(date)}`} available={date < today}>
            {strings.report.nextDay} <span aria-hidden="true">{strings.report.nextGlyph}</span>
          </StepLink>
        </div>
      </div>
      <p id="report-date-status" role="status" className={styles.dateStatus}>
        {settled &&
          (empty ? (
            apiErrorCopy['report.no_notes'](parseDateOnly(date))
          ) : (
            <time dateTime={date}>{dateLong(parseDateOnly(date))}</time>
          ))}
        {error !== undefined && <span className="visually-hidden">{error}</span>}
      </p>
      <RequestAlert>{download.failure}</RequestAlert>
      <p role="status" className={styles.downloadStatus} data-preparing={slow || undefined}>
        {downloadStatus()}
      </p>
      <ButtonGroup>
        {formats.map(({ format, label }) => (
          <Button
            key={format}
            variant="primary"
            className={styles.download}
            busy={busy}
            busyLabel={download.pending === format ? strings.report.downloading : undefined}
            announceBusy={false}
            unavailable={empty || error !== undefined}
            aria-describedby="report-date-status"
            onClick={() => {
              press(format);
            }}
            onUnavailablePress={() => {
              press(format);
            }}
          >
            {label}
          </Button>
        ))}
      </ButtonGroup>
    </PageStatus>
  );
}

// What is wrong with a date for the field, in the words under it, or nothing when it is a date a report can be for.
function problemWith(value: string, today: string): string | undefined {
  if (!isReportDate(value)) {
    return strings.report.enterADate;
  }

  return value > today ? strings.report.dateNotFuture : undefined;
}
