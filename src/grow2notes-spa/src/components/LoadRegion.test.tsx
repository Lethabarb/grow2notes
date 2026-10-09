import { QueryClientProvider, useQuery } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../api/api.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { LoadRegion } from './LoadRegion.tsx';
import { PageHeading } from './PageHeading.tsx';

type Participant = { id: string; name: string };

const participantsPath = '/api/participants';
const participantPath = '/api/participants/3f2a';
const todayPath = '/api/today';
const draftsPath = '/api/me/drafts';

const jane: Participant = { id: '3f2a', name: 'Jane Citizen' };

const fetchMock = vi.fn<typeof fetch>();

/** How the stubbed server answers each path: a new answer for each request. */
const answers = new Map<string, (init?: RequestInit) => Promise<Response>>();

/** Stubs `fetch` as the server, which answers each request for `path` with `answer` from now on. */
function serve(path: string, answer: (init?: RequestInit) => Promise<Response>) {
  answers.set(path, answer);
}

/** How many requests have been sent for `path`. */
function sent(path: string) {
  return fetchMock.mock.calls.filter(([sentPath]) => sentPath === path).length;
}

/** A `200` with a JSON body. */
function json(body: unknown): Promise<Response> {
  return Promise.resolve(new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } }));
}

/** A `200` with a JSON body that comes after `ms`. */
function jsonAfter(ms: number, body: unknown): Promise<Response> {
  return new Promise((resolve) => {
    setTimeout(() => {
      resolve(json(body));
    }, ms);
  });
}

/** An RFC 9457 problem with no `code`, as the API answers a failure (design.md §6.1). */
function problem(status: number): Promise<Response> {
  return Promise.resolve(
    new Response(JSON.stringify({ type: 'about:blank', title: 'Problem', status }), {
      status,
      headers: { 'Content-Type': 'application/problem+json' },
    }),
  );
}

/** Like `fetch` with no connection, which rejects at once. */
function noConnection(): Promise<Response> {
  return Promise.reject(new TypeError('Failed to fetch'));
}

/** Like `fetch` to a server that never answers: it rejects only when its signal aborts, with the signal's reason. */
function noAnswer(init?: RequestInit): Promise<Response> {
  return new Promise((_resolve, reject) => {
    const signal = init?.signal;
    signal?.addEventListener('abort', () => {
      reject(signal.reason as Error);
    });
  });
}

/** A screen's query, which passes TanStack's signal to `api()`, as every screen's does. */
function useApi<T>(path: string) {
  return useQuery({ queryKey: [path], queryFn: ({ signal }) => api<T>(path, { signal }) });
}

function ParticipantList({ participants }: { participants: Participant[] }) {
  return participants.length === 0
    ? <p>No participants yet.</p>
    : <ul>{participants.map(({ id, name }) => <li key={id}>{name}</li>)}</ul>;
}

/** A screen with a fixed `<h1>` and one query. */
function ParticipantsPage() {
  const participants = useApi<Participant[]>(participantsPath);
  return (
    <>
      <PageHeading>Participants</PageHeading>
      <LoadRegion queries={[participants]} loading="Loading participants…" what="The participant list">
        {/* Called only once every query has data. */}
        {() => <ParticipantList participants={participants.data!} />}
      </LoadRegion>
    </>
  );
}

/** A screen with a fixed `<h1>` and two queries shown together, as Today's list and drafts are. */
function TodayPage() {
  const today = useApi<Participant[]>(todayPath);
  const drafts = useApi<Participant[]>(draftsPath);
  return (
    <>
      <PageHeading>Today</PageHeading>
      <LoadRegion queries={[today, drafts]} loading="Loading participants…" what="The participant list">
        {() => (
          <>
            <ParticipantList participants={today.data!} />
            <ParticipantList participants={drafts.data!} />
          </>
        )}
      </LoadRegion>
    </>
  );
}

/** A screen whose `<h1>` is data: the participant's name. */
function ParticipantPage() {
  const participant = useApi<Participant>(participantPath);
  return (
    <LoadRegion
      queries={[participant]}
      loading="Loading participant…"
      what="This participant"
      fallbackHeading="Participant"
    >
      {() => <PageHeading>{participant.data!.name}</PageHeading>}
    </LoadRegion>
  );
}

/** A screen whose query key is the day it shows, as the daily report's `?date=` is (date-navigation.md). */
function ReportPage({ day }: { day: string }) {
  const report = useApi<Participant[]>(`/api/report/${day}`);
  return (
    <>
      <PageHeading>Daily report</PageHeading>
      <LoadRegion queries={[report]} loading="Loading report…" what="The report">
        {() => <ParticipantList participants={report.data!} />}
      </LoadRegion>
    </>
  );
}

/** Renders `page` under a query client of its own, made as `main.tsx` makes the app's, and returns the client. */
function renderPage(page: ReactNode) {
  const client = createQueryClient();
  render(<QueryClientProvider client={client}>{page}</QueryClientProvider>);
  return client;
}

/** The region's line, the page's one status region. */
function line() {
  return screen.getByRole('status');
}

/** Moves the fake clock on by `ms`, and lets every request, retry and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/**
 * Lets TanStack hand the queries' new state to the page, which it does on a `setTimeout` of 0 that the fake clock
 * runs 1 ms later when it is set while the clock is moving.
 */
async function settle() {
  await wait(1);
}

/** Focuses Try again and presses it, as a keyboard user would, and returns it. */
function pressTryAgain() {
  const button = screen.getByRole('button', { name: 'Try again' });
  button.focus();
  fireEvent.click(button);
  return button;
}

beforeEach(() => {
  vi.useFakeTimers();
  // `api()` sends each request to a path, as a string.
  fetchMock.mockImplementation((path, init) => {
    const answer = typeof path === 'string' ? answers.get(path) : undefined;
    if (answer === undefined) {
      throw new Error('The test gave no answer for this request.');
    }

    return answer(init);
  });
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  answers.clear();
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('LoadRegion', () => {
  it('renders its line, empty, from the first render', () => {
    // No effect runs here, so a line that an effect added would be missing.
    const markup = renderToStaticMarkup(
      <QueryClientProvider client={createQueryClient()}>
        <ParticipantsPage />
      </QueryClientProvider>,
    );

    expect(markup).toMatch(/<p role="status" class="[^"]+"><\/p>$/);
  });

  it('shows nothing for 1 s, then the loading line', async () => {
    serve(participantsPath, noAnswer);
    renderPage(<ParticipantsPage />);

    await wait(999);
    expect(line()).toBeEmptyDOMElement();

    await wait(1);
    expect(line()).toHaveTextContent('Loading participants…');
  });

  it('shows the empty sentence only once the data has come, and empties the loading line', async () => {
    serve(participantsPath, () => jsonAfter(1500, []));
    renderPage(<ParticipantsPage />);

    await wait(1499);
    expect(line()).toHaveTextContent('Loading participants…');
    expect(screen.queryByText('No participants yet.')).not.toBeInTheDocument();

    await wait(1);
    await settle();
    expect(screen.getByText('No participants yet.')).toBeInTheDocument();
    expect(line()).toBeEmptyDOMElement();
  });

  it('renders the content only once every query has data', async () => {
    serve(todayPath, () => json([jane]));
    serve(draftsPath, () => jsonAfter(500, []));
    renderPage(<TodayPage />);

    await wait(499);
    expect(screen.queryByText('Jane Citizen')).not.toBeInTheDocument();

    await wait(1);
    await settle();
    expect(screen.getByText('Jane Citizen')).toBeInTheDocument();
    expect(screen.getByText('No participants yet.')).toBeInTheDocument();
  });

  it('puts the content before its line, so that a data heading is the first thing in <main>', async () => {
    serve(participantPath, () => json(jane));
    renderPage(<ParticipantPage />);

    await settle();

    const heading = screen.getByRole('heading', { level: 1, name: 'Jane Citizen' });
    expect(heading.compareDocumentPosition(line())).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  describe('after a failed first load', () => {
    it.each([
      { failure: 'no connection', answer: noConnection, failsAfter: 1000, cause: 'no connection' },
      { failure: 'a timeout', answer: noAnswer, failsAfter: 21_000, cause: 'no connection' },
      { failure: 'a 503', answer: () => problem(503), failsAfter: 1000, cause: 'something went wrong' },
      { failure: 'a 403', answer: () => problem(403), failsAfter: 0, cause: 'something went wrong' },
      { failure: 'a 404', answer: () => problem(404), failsAfter: 0, cause: 'something went wrong' },
    ])('says what did not load and why, for $failure, with Try again', async ({ answer, failsAfter, cause }) => {
      serve(participantsPath, answer);
      renderPage(<ParticipantsPage />);

      await wait(failsAfter);
      await settle();

      expect(line().textContent).toBe(`The participant list did not load: ${cause}. Try again.`);
      // Outside the line, so that its label is not read as part of the sentence.
      expect(line()).not.toContainElement(screen.getByRole('button', { name: 'Try again' }));
    });

    it('waits, with the loading line, until none of its queries is still fetching', async () => {
      serve(todayPath, () => problem(404));
      serve(draftsPath, () => jsonAfter(3000, []));
      renderPage(<TodayPage />);

      await wait(1000);
      expect(line()).toHaveTextContent('Loading participants…');
      await wait(1999);
      expect(line()).toHaveTextContent('Loading participants…');
      expect(screen.queryByRole('button')).not.toBeInTheDocument();

      await wait(1);
      await settle();
      expect(line().textContent).toBe('The participant list did not load: something went wrong. Try again.');
    });

    it('gives the cause of the first of its queries that failed', async () => {
      serve(todayPath, noConnection);
      serve(draftsPath, () => problem(503));
      renderPage(<TodayPage />);

      await wait(1000);
      await settle();

      expect(line().textContent).toBe('The participant list did not load: no connection. Try again.');
    });

    it('shows the fallback heading on a screen whose heading is data', async () => {
      serve(participantPath, noConnection);
      renderPage(<ParticipantPage />);

      await wait(1000);
      await settle();

      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Participant');
      expect(line().textContent).toBe('This participant did not load: no connection. Try again.');
    });

    it('drops the failure when the query key changes, and loads and fails as a first load does', async () => {
      serve('/api/report/monday', () => problem(503));
      const client = createQueryClient();
      const { rerender } = render(
        <QueryClientProvider client={client}>
          <ReportPage day="monday" />
        </QueryClientProvider>,
      );
      await wait(1000);
      await settle();
      expect(line().textContent).toBe('The report did not load: something went wrong. Try again.');
      serve('/api/report/sunday', noAnswer);

      rerender(
        <QueryClientProvider client={client}>
          <ReportPage day="sunday" />
        </QueryClientProvider>,
      );

      expect(line()).toBeEmptyDOMElement();
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
      await wait(999);
      expect(line()).toBeEmptyDOMElement();
      await wait(1);
      expect(line()).toHaveTextContent('Loading report…');

      // Its timeout, its one silent retry and that retry's timeout.
      await wait(20_000);
      await settle();
      expect(line().textContent).toBe('The report did not load: no connection. Try again.');
    });

    it('shows nothing for a 401, which the session flow has', async () => {
      serve(participantPath, () => problem(401));
      renderPage(<ParticipantPage />);

      await wait(60_000);

      expect(line()).toBeEmptyDOMElement();
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
      expect(screen.queryByRole('heading')).not.toBeInTheDocument();
    });
  });

  describe('Try again', () => {
    it('keeps focus, empties the line at once, and shows "Loading…" after 400 ms', async () => {
      serve(participantsPath, noConnection);
      renderPage(<ParticipantsPage />);
      await wait(1000);
      await settle();
      serve(participantsPath, noAnswer);

      const button = pressTryAgain();

      expect(line()).toBeEmptyDOMElement();
      expect(button).toHaveFocus();
      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(sent(participantsPath)).toBe(3);

      await wait(399);
      expect(button).toHaveAccessibleName('Try again');

      await wait(1);
      expect(button).toHaveAccessibleName('Loading…');
      expect(button).toHaveFocus();
      expect(line()).toBeEmptyDOMElement();
    });

    it('says "still did not load" when it fails, with focus still on it', async () => {
      serve(participantsPath, noConnection);
      renderPage(<ParticipantsPage />);
      await wait(1000);
      await settle();

      const button = pressTryAgain();
      // Its one silent retry, as on the first load.
      await wait(999);
      expect(line()).toBeEmptyDOMElement();

      await wait(1);
      await settle();
      expect(line().textContent).toBe('The participant list still did not load: no connection. Try again.');
      expect(button).toHaveFocus();
      expect(button).not.toHaveAttribute('aria-disabled');
      expect(button).toHaveAccessibleName('Try again');
    });

    it('fetches only the queries that failed', async () => {
      serve(todayPath, () => json([jane]));
      serve(draftsPath, () => problem(503));
      renderPage(<TodayPage />);
      await wait(1000);
      await settle();
      expect(line().textContent).toBe('The participant list did not load: something went wrong. Try again.');
      serve(draftsPath, () => json([]));

      pressTryAgain();
      await settle();

      expect(sent(todayPath)).toBe(1);
      expect(sent(draftsPath)).toBe(3);
      expect(screen.getByText('Jane Citizen')).toBeInTheDocument();
    });

    it('focuses the page heading once the content has loaded', async () => {
      serve(participantsPath, noConnection);
      renderPage(<ParticipantsPage />);
      await wait(1000);
      await settle();
      serve(participantsPath, () => json([jane]));

      pressTryAgain();
      await settle();

      expect(screen.getByText('Jane Citizen')).toBeInTheDocument();
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
      expect(line()).toBeEmptyDOMElement();
      expect(screen.getByRole('heading', { level: 1, name: 'Participants' })).toHaveFocus();
    });

    it('leaves focus where it is when the content loads after a failed Try again and focus has moved on', async () => {
      serve(participantsPath, noConnection);
      const client = renderPage(
        <>
          <input aria-label="Search" />
          <ParticipantsPage />
        </>,
      );
      await wait(1000);
      await settle();
      pressTryAgain();
      await wait(1000);
      await settle();
      expect(line().textContent).toBe('The participant list still did not load: no connection. Try again.');
      const search = screen.getByRole('textbox', { name: 'Search' });
      search.focus();
      serve(participantsPath, () => json([jane]));

      act(() => {
        void client.refetchQueries();
      });
      await settle();

      expect(screen.getByText('Jane Citizen')).toBeInTheDocument();
      expect(search).toHaveFocus();
    });

    it('focuses the data\'s own heading, not the fallback, on a screen whose heading is data', async () => {
      serve(participantPath, noConnection);
      renderPage(<ParticipantPage />);
      await wait(1000);
      await settle();
      serve(participantPath, () => json(jane));

      pressTryAgain();
      await settle();

      const headings = screen.getAllByRole('heading', { level: 1 });
      expect(headings).toHaveLength(1);
      expect(headings[0]).toHaveTextContent('Jane Citizen');
      expect(headings[0]).toHaveFocus();
    });
  });

  it('leaves the content on screen, with nothing said, when a refetch in the background fails', async () => {
    serve(participantsPath, () => json([jane]));
    const client = renderPage(<ParticipantsPage />);
    await wait(0);
    serve(participantsPath, noConnection);

    act(() => {
      void client.refetchQueries();
    });
    await wait(60_000);

    expect(client.getQueryState([participantsPath])?.status).toBe('error');
    expect(screen.getByText('Jane Citizen')).toBeInTheDocument();
    expect(line()).toBeEmptyDOMElement();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
