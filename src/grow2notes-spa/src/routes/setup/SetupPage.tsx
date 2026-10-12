import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { api, ApiError, classify } from '../../api/api.ts';
import { Button } from '../../components/Button.tsx';
import { focusPageHeading } from '../../components/focusPageHeading.ts';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { useDelayed } from '../../components/useDelayed.ts';
import { useErrorTitle } from '../../components/useErrorTitle.ts';
import { apiErrorCopy, type ApiErrorCode } from '../../copy/apiErrors.ts';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings, type Cause } from '../../copy/strings.ts';
import { ChooseMethod } from './ChooseMethod.tsx';
import { NoLinkMessage, SetupMessage } from './SetupMessage.tsx';
import { useSetup, type SetupAccount } from './setupContext.ts';
import styles from './SetupPage.module.css';

// A fast check shows nothing; one that takes longer shows the status line (empty-loading-error.md *Timing*).
const checkingLineDelay = 1000;

/** The setup link's `u` and `t`, as `POST /api/auth/setup/start` takes them (design.md §6.2). */
type SetupLink = { userId: string; token: string };

/** The link check, from the link's arrival to its answer. */
type Check =
  /** The request is in flight; `again` when Try again sent it. */
  | { state: 'checking'; again: boolean }
  /** No connection, a server failure or a `429`, which Try again may get past. */
  | { state: 'failed'; failure: Cause | 'tooManyTries' }
  /** A `410`, or a `401`, which the page says instead, with no Try again. */
  | { state: 'expired' | 'timedOut' };

/** What `/setup` shows: view D, a whole-page message, or view E once the link has checked (sign-in.md). */
type View = 'linkCheck' | 'expired' | 'timedOut' | 'noLink' | 'choose';

/**
 * `/setup` (sign-in.md view D and its *Setup* states): reads the emailed link's fragment, takes it out of the address
 * and the history at once, and checks it with `POST /api/auth/setup/start`, whose `{email, displayName}` go into the
 * setup layout's memory for view E. The token stays in this page's memory only until the check is answered, so that
 * Try again can send it again (*Interactions* 7).
 */
export function SetupPage() {
  const { account, setAccount } = useSetup();
  const { hash } = useLocation();
  const navigate = useNavigate();
  // Read once, so that StrictMode's second render still has the link after the address has lost it.
  const [link, setLink] = useState(() => readLink(hash));
  const [check, setCheck] = useState<Check | undefined>(() =>
    link === undefined ? undefined : { state: 'checking', again: false },
  );

  // Before the check is answered, and replaced, not pushed, so that neither the address bar nor the history keeps the
  // token (design.md §8.1 step 2). Through the router, so that `history.state` holds the router's own state only.
  const hasFragment = hash !== '';
  useEffect(() => {
    if (hasFragment) {
      void navigate('/setup', { replace: true });
    }
  }, [hasFragment, navigate]);

  const checking = check?.state === 'checking';
  useEffect(() => {
    if (link === undefined || !checking) {
      return;
    }

    // Aborted when the page goes, as by StrictMode's first run of this effect, before the request is sent.
    const controller = new AbortController();
    api<SetupAccount>('/api/auth/setup/start', { method: 'POST', body: link, signal: controller.signal }).then(
      (found) => {
        setLink(undefined);
        setCheck(undefined);
        setAccount(found);
      },
      (error: unknown) => {
        if (controller.signal.aborted) {
          return;
        }

        const next = outcome(error);
        if (next.state !== 'failed') {
          setLink(undefined);
        }

        setCheck(next);
      },
    );
    return () => {
      controller.abort();
    };
  }, [link, checking, setAccount]);

  // A 401 from view E's calls: the enrolment cookie has ended, and setup's memory goes with it (sign-in.md *Setup*).
  const timeOut = useCallback(() => {
    setAccount(undefined);
    setCheck({ state: 'timedOut' });
  }, [setAccount]);

  const failure = check?.state === 'failed' ? check.failure : undefined;
  useErrorTitle(failure !== undefined);
  const slow = useDelayed(check?.state === 'checking' && !check.again, checkingLineDelay);

  const view = viewOf(check, account);
  // View E follows the check in place, at the same address, so the page moves focus as a new page's arrival does.
  useEffect(() => {
    if (view === 'choose') {
      focusPageHeading();
    }
  }, [view]);

  let content: ReactNode;
  switch (view) {
    case 'expired':
      content = <SetupMessage {...apiErrorCopy['auth.setup_link_invalid']} />;
      break;
    case 'timedOut':
      content = <SetupMessage {...strings.setup.timedOut} />;
      break;
    case 'noLink':
      content = <NoLinkMessage />;
      break;
    case 'choose':
      // Only with an account in memory (viewOf).
      content = account && <ChooseMethod account={account} onTimedOut={timeOut} />;
      break;
    case 'linkCheck': {
      const retrying = check?.state === 'checking' && check.again;
      let line = slow ? strings.setup.checkingLink : '';
      if (failure !== undefined) {
        line = failure === 'tooManyTries' ? strings.setup.tooManyTries : msg.setupLinkNotChecked(failure);
      }

      content = (
        <>
          <PageHeading>{pageName.setUpYourAccount}</PageHeading>
          {/* Rendered every time, so that it is in the page before any words are written into it. Empty while Try
              again runs, so that a second failure writes its sentence again, and a screen reader says it again. */}
          <p role="status" className={styles.line}>{line}</p>
          {(failure !== undefined || retrying) && (
            <Button
              variant="primary"
              busy={retrying}
              busyLabel={strings.setup.checking}
              onClick={() => {
                setCheck({ state: 'checking', again: true });
              }}
            >
              {strings.tryAgain}
            </Button>
          )}
        </>
      );
      break;
    }
  }

  return <PageStatus>{content}</PageStatus>;
}

/** The link in the fragment, `#u=…&t=…`, read back as the server escaped it, or none without both parts. */
function readLink(hash: string): SetupLink | undefined {
  const fragment = new URLSearchParams(hash.slice(1));
  const userId = fragment.get('u');
  const token = fragment.get('t');
  return userId && token ? { userId, token } : undefined;
}

/** What a failed check leads to (sign-in.md *Setup*). */
function outcome(error: unknown): Check {
  if (error instanceof ApiError && error.code === ('auth.setup_link_invalid' satisfies ApiErrorCode)) {
    return { state: 'expired' };
  }

  if (error instanceof ApiError && error.status === 429) {
    return { state: 'failed', failure: 'tooManyTries' };
  }

  // A 401 from a setup call means that its enrolment cookie has ended.
  const failure = classify(error);
  return failure === 'signedOut' ? { state: 'timedOut' } : { state: 'failed', failure };
}

function viewOf(check: Check | undefined, account: SetupAccount | undefined): View {
  if (check?.state === 'expired' || check?.state === 'timedOut') {
    return check.state;
  }

  if (account !== undefined) {
    return 'choose';
  }

  return check === undefined ? 'noLink' : 'linkCheck';
}
