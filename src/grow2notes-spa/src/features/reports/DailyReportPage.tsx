import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router';
import { ApiError, classify } from '../../api/api.ts';
import { download } from '../../api/download.ts';
import { refreshMe } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { ButtonGroup } from '../../components/ButtonGroup.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { apiErrorCopy } from '../../copy/apiErrors.ts';
import { parseDateOnly } from '../../copy/format.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';

type Format = 'docx' | 'pdf';

const dateShape = /^\d{4}-\d{2}-\d{2}$/;

/**
 * The Report screen at `/reports/daily/:date` (daily-report.md), for managers alone, inside `ManagerOnly`: its static
 * `<h1>`, the failure line, and Download Word and Download PDF. A press fetches the day's file (design.md §6.5) and
 * saves it (A78); both buttons are `aria-disabled` while it is out, so that a second press sends nothing, and the
 * pressed one shows "Downloading…" after 400 ms. A failure says so above the buttons, which stay as they were; focus
 * never moves; nothing is kept in the browser. Leaving the page abandons the request.
 *
 * T17 adds the date field, the status lines and the empty state; until then a day with no notes is a line here.
 */
export function DailyReportPage() {
  const { date = '' } = useParams();
  const queryClient = useQueryClient();
  const [pending, setPending] = useState<Format>();
  const [alert, setAlert] = useState<string>();
  // The request in flight, if any: a ref, as a second press can come before the render that makes the buttons busy.
  const inFlight = useRef<AbortController>(undefined);

  useEffect(
    () => () => {
      inFlight.current?.abort();
    },
    [],
  );

  async function press(format: Format) {
    if (inFlight.current !== undefined) {
      return;
    }

    // A date that is no yyyy-MM-dd is not sent in a path (T17's arrival rules replace this guard).
    if (!dateShape.test(date)) {
      setAlert(msg.notDone('downloaded', 'server'));
      return;
    }

    const controller = new AbortController();
    inFlight.current = controller;
    setAlert(undefined);
    setPending(format);
    try {
      const path = `/api/reports/daily/${date}/export?format=${format}`;
      await download(path, `daily-notes_${date}.${format}`, controller.signal);
    } catch (failure) {
      if (!controller.signal.aborted) {
        failed(failure);
      }
    }

    inFlight.current = undefined;
    if (!controller.signal.aborted) {
      setPending(undefined);
    }
  }

  function failed(failure: unknown) {
    if (failure instanceof ApiError && failure.code === 'report.no_notes') {
      setAlert(apiErrorCopy['report.no_notes'](parseDateOnly(date)));
      return;
    }

    if (failure instanceof ApiError && failure.status === 403) {
      // No longer a manager: the shell's role guard shows Page not found once `/me` says so.
      refreshMe(queryClient).catch(() => {});
    }

    // A 401 has the session flow, which `api()` started, and shows nothing here.
    const cause = classify(failure);
    setAlert(cause === 'signedOut' ? undefined : msg.notDone('downloaded', cause));
  }

  const busy = pending !== undefined;
  return (
    <PageStatus>
      <PageHeading>{pageName.dailyReport}</PageHeading>
      <RequestAlert>{alert}</RequestAlert>
      <ButtonGroup>
        <Button
          variant="primary"
          busy={busy}
          busyLabel={pending === 'docx' ? strings.report.downloading : undefined}
          onClick={() => void press('docx')}
        >
          {strings.report.downloadWord}
        </Button>
        <Button
          variant="primary"
          busy={busy}
          busyLabel={pending === 'pdf' ? strings.report.downloading : undefined}
          onClick={() => void press('pdf')}
        >
          {strings.report.downloadPdf}
        </Button>
      </ButtonGroup>
    </PageStatus>
  );
}
