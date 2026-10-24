import { useEffect, useRef, useState, type RefObject } from 'react';
import { download } from './download.ts';

export type FileFormat = 'docx' | 'pdf';

export type DownloadRequest = {
  /** What the file is of, such as a report date: a change of the hook's `scope` abandons a request for another. */
  scope: string;
  format: FileFormat;
  path: string;
  fallbackName: string;
  /** The words for a failure, or nothing when the screen shows it another way. Never called for an abort. */
  failed: (error: unknown) => string | undefined;
};

type Run = { scope: string; controller: AbortController };

type Result = { savedName: string } | { failure: string | undefined };

type State = { scope: string; format: FileFormat; result?: Result };

// Every way a request is abandoned drops the run first, so a press straight after is not taken for a second press.
function abandon(run: RefObject<Run | undefined>): void {
  const abandoned = run.current;
  run.current = undefined;
  abandoned?.controller.abort();
}

/**
 * One file download at a time for a screen (file-download.md): `start()` fetches and saves the file through
 * `download()` and the hook holds what a screen shows of it, the format in flight, the name it was saved under or the
 * failure's words. It holds no timing and no copy. The request is abandoned, and the last result forgotten, when
 * `scope` changes to one other than the request's, and abandoned when the screen goes.
 */
export function useDownload(scope: string): {
  pending: FileFormat | undefined;
  savedName: string | undefined;
  failure: string | undefined;
  start: (request: DownloadRequest) => void;
} {
  // A ref, as a second press can come before the render that makes the screen busy.
  const run = useRef<Run>(undefined);
  const [state, setState] = useState<State>();
  const [seenScope, setSeenScope] = useState(scope);

  // A press whose date is not yet the screen's starts with its own scope, before the address catches up; comparing
  // with the new `scope` keeps that press, and drops the state of any other.
  if (seenScope !== scope) {
    setSeenScope(scope);
    if (state?.scope !== scope) {
      setState(undefined);
    }
  }

  useEffect(() => {
    if (run.current !== undefined && run.current.scope !== scope) {
      abandon(run);
    }
  }, [scope]);

  useEffect(
    () => () => {
      abandon(run);
    },
    [],
  );

  function start(request: DownloadRequest): void {
    if (run.current !== undefined) {
      return;
    }

    const mine: Run = { scope: request.scope, controller: new AbortController() };
    run.current = mine;
    setState({ scope: request.scope, format: request.format });
    void perform(request, mine);
  }

  async function perform(request: DownloadRequest, mine: Run): Promise<void> {
    let result: Result;
    try {
      result = { savedName: await download(request.path, request.fallbackName, mine.controller.signal) };
    } catch (error) {
      if (run.current !== mine) {
        return;
      }

      result = { failure: request.failed(error) };
    }

    if (run.current !== mine) {
      return;
    }

    run.current = undefined;
    setState({ scope: request.scope, format: request.format, result });
  }

  const result = state?.result;
  return {
    pending: state !== undefined && result === undefined ? state.format : undefined,
    savedName: result !== undefined && 'savedName' in result ? result.savedName : undefined,
    failure: result !== undefined && 'failure' in result ? result.failure : undefined,
    start,
  };
}
