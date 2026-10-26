import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { api, classify, isNotFound } from '../../api/api.ts';
import { useMe } from '../../api/me.ts';
import { Button } from '../../components/Button.tsx';
import { ButtonGroup } from '../../components/ButtonGroup.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { useFocusOnReplace } from '../../routes/routeChange.ts';
import { useWholePageMessage } from '../../routes/wholePageMessage.ts';
import styles from './ConnectPage.module.css';
import { leaveFor } from './leaveFor.ts';

/** What `GET /api/connect` says about the assistant, from the operator's registration of it (mcp-server.md §4.4). */
type ConnectRequest = {
  assistantName: string;
  company: string;
  dataLocation: string;
  clientIdHost: string;
  redirectHost: string;
  /** Every redirect address registered for the assistant is on this computer. */
  loopbackOnly: boolean;
};

// The only address that Allow and Cancel go to: the server's own, which issues the code or refuses.
const authorisePrefix = '/oauth/authorize?';

/**
 * Connect an AI assistant at `/connect?request=…` (mcp-server.md §4.4), where an assistant's sign-in leads. It is
 * outside the manager gate: a manager or Support account chooses Allow or Cancel, a worker is refused and can only
 * Cancel, and a request that is missing, expired or not known shows the general error. The choice is posted, and the
 * browser then goes to the address that the server answers with, where the code is issued, or `access_denied` sent.
 */
export function ConnectPage() {
  const [params] = useSearchParams();
  const request = params.get('request');

  // Keyed by the request, so that another request on this route starts afresh.
  return request === null || request === '' ? <ProblemPage /> : <RequestScreen key={request} request={request} />;
}

/** The general error in place of the screen, with its `<h1>` focused as the screen's buttons go (app-shell.md, 4). */
function ProblemPage() {
  useWholePageMessage(pageName.thereIsAProblem);
  useFocusOnReplace();

  return (
    <PageStatus>
      <PageHeading>{strings.thereIsAProblem}</PageHeading>
    </PageStatus>
  );
}

function RequestScreen({ request }: { request: string }) {
  const [refused, setRefused] = useState(false);
  const details = useQuery({
    queryKey: ['connect', request],
    queryFn: ({ signal }) => api<ConnectRequest>(`/api/connect?request=${encodeURIComponent(request)}`, { signal }),
  });

  // Decided before the region, which would show Page not found for a 404 or a 403.
  if (refused || isNotFound(details.error)) {
    return <ProblemPage />;
  }

  return (
    <PageStatus>
      <LoadRegion
        queries={[details]}
        loading={strings.connect.loading}
        what={strings.connect.what}
        fallbackHeading={strings.connect.heading}
      >
        {/* Called only once the query has data. */}
        {() => (
          <Decision
            request={request}
            assistant={details.data!}
            onRefused={() => {
              setRefused(true);
            }}
          />
        )}
      </LoadRegion>
    </PageStatus>
  );
}

type DecisionProps = {
  request: string;
  assistant: ConnectRequest;
  /** The request is no longer valid, or the server's answer was not an address to go to. */
  onRefused: () => void;
};

function Decision({ request, assistant, onRefused }: DecisionProps) {
  const me = useMe();
  const decide = useMutation({
    mutationFn: (allow: boolean) =>
      api<{ next: string }>('/api/connect', { method: 'POST', body: { request, allow } }),
    onSuccess: ({ next }) => {
      if (next.startsWith(authorisePrefix)) {
        leaveFor(next);
      } else {
        onRefused();
      }
    },
    onError: (failure) => {
      if (isNotFound(failure)) {
        onRefused();
      }
    },
  });
  // Locked once the answer is in too, as the page is on its way to the server's address. The button pressed is busy,
  // and the other unavailable, which a press does nothing on.
  const locked = decide.isPending || decide.isSuccess;
  const allowing = decide.variables === true;

  let failure: string | undefined;
  if (decide.isError) {
    const cause = classify(decide.error);
    // A 401 has the session flow, which `api()` started, and shows nothing here.
    if (cause !== 'signedOut') {
      failure = allowing ? msg.connect.notConnected(cause) : msg.notCancelled(cause);
    }
  }

  const cancel = (
    <Button
      unavailable={locked}
      busy={locked && !allowing}
      busyLabel={strings.connect.cancelling}
      onClick={() => {
        decide.mutate(false);
      }}
    >
      {strings.cancel}
    </Button>
  );

  if (me.role === 'Worker') {
    return (
      <>
        <PageHeading>{strings.connect.heading}</PageHeading>
        <p>{strings.connect.workerRefusal}</p>
        <div className={styles.actions}>
          <RequestAlert>{failure}</RequestAlert>
          <ButtonGroup>{cancel}</ButtonGroup>
        </div>
      </>
    );
  }

  return (
    <>
      <PageHeading>{strings.connect.heading}</PageHeading>
      <p>{msg.connect.signedInAs(me.displayName, me.role)}</p>
      <p>
        {msg.connect.wantsToConnect(
          assistant.assistantName,
          assistant.company,
          assistant.clientIdHost,
          assistant.redirectHost,
        )}
      </p>
      <p>{strings.connect.canDo}</p>
      <p>{msg.connect.dataGoesTo(assistant.company, assistant.dataLocation)}</p>
      <p>{msg.connect.approvedAccount(assistant.assistantName)}</p>
      {assistant.loopbackOnly && <p>{strings.connect.loopbackWarning}</p>}
      <div className={styles.actions}>
        <RequestAlert>{failure}</RequestAlert>
        <ButtonGroup>
          <Button
            variant="primary"
            unavailable={locked}
            busy={locked && allowing}
            busyLabel={strings.connect.connecting}
            onClick={() => {
              decide.mutate(true);
            }}
          >
            {strings.connect.allow}
          </Button>
          {cancel}
        </ButtonGroup>
      </div>
    </>
  );
}
