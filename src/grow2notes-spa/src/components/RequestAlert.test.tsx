import { onlineManager, QueryClientProvider, useMutation } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { useState, type FormEvent, type ReactNode } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api, classify } from '../api/api.ts';
import { createQueryClient } from '../api/queryClient.ts';
import { msg } from '../copy/messages.ts';
import { Button } from './Button.tsx';
import { ButtonGroup } from './ButtonGroup.tsx';
import { RequestAlert } from './RequestAlert.tsx';

const promptsPath = '/api/admin/guide-prompts';
const antiforgeryPath = '/api/auth/antiforgery';

// The prompts' RowVersion, as the API's JSON gives it.
const rowVersion = 'AAAAAAAAB9E=';

const fetchMock = vi.fn<typeof fetch>();

/**
 * Stubs `fetch` as the server: the antiforgery endpoint always gives a token, so that a test counts and answers only
 * the requests its form sends, and every other request gets a new answer from `answer`.
 */
function serve(answer: (init?: RequestInit) => Promise<Response>) {
  fetchMock.mockImplementation((path, init) =>
    path === antiforgeryPath
      ? Promise.resolve(new Response(null, { status: 204, headers: { 'X-XSRF-TOKEN': 'token-1' } }))
      : answer(init),
  );
}

/** How many requests the form has sent, leaving out those for the antiforgery token. */
function sent() {
  return fetchMock.mock.calls.filter(([path]) => path !== antiforgeryPath).length;
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

/** An RFC 9457 problem with no `code`, as the API answers a failure (design.md §6.1). */
function problem(status: number): Promise<Response> {
  return Promise.resolve(
    new Response(JSON.stringify({ type: 'about:blank', title: 'Problem', status }), {
      status,
      headers: { 'Content-Type': 'application/problem+json' },
    }),
  );
}

/**
 * The form's words for a failed save, taken from its mutation's error as every form takes them: none before a press
 * has failed or while the next press is in flight, and none for a `401`, which the session flow has.
 */
function saveFailed(error: Error | null, safe: string | undefined): string | undefined {
  if (error === null) {
    return undefined;
  }

  const failure = classify(error);
  return failure === 'signedOut' ? undefined : msg.notDone('saved', failure, safe);
}

/** A form that saves one field, as Guide prompts' does, with what is safe after a failure, if anything. */
function PromptsForm({ safe }: { safe?: string }) {
  const [text, setText] = useState('What did you do together today?');
  const save = useMutation({
    mutationFn: (prompts: string) =>
      api(promptsPath, { method: 'PUT', body: { text: prompts }, ifMatch: rowVersion }),
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    save.mutate(text);
  }

  return (
    <form noValidate onSubmit={submit}>
      <label htmlFor="prompts">Guide prompts</label>
      <textarea id="prompts" value={text} onChange={(event) => setText(event.currentTarget.value)} />
      <RequestAlert>{saveFailed(save.error, safe)}</RequestAlert>
      <ButtonGroup>
        <Button type="submit" variant="primary" busy={save.isPending} busyLabel="Saving…">
          Save
        </Button>
      </ButtonGroup>
    </form>
  );
}

/** Renders `form` under a query client of its own, made as `main.tsx` makes the app's. */
function renderForm(form: ReactNode = <PromptsForm />) {
  render(<QueryClientProvider client={createQueryClient()}>{form}</QueryClientProvider>);
}

/** The form's alert. */
function alert() {
  return screen.getByRole('alert');
}

/** Focuses Save and presses it, as a person does, and returns it. */
function pressSave() {
  const button = screen.getByRole('button', { name: 'Save' });
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Moves the fake clock on by `ms`, and lets every request and render that falls due by then run. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/**
 * Lets TanStack hand the mutation's new state to the form, which it does on a `setTimeout` of 0 that the fake clock
 * runs 1 ms later when it is set while the clock is moving.
 */
async function settle() {
  await wait(1);
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  onlineManager.setOnline(true);
  fetchMock.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('RequestAlert', () => {
  it('is in the form from the first render, empty, directly above its buttons', () => {
    // No effect runs here, so an alert that an effect added would be missing.
    const markup = renderToStaticMarkup(
      <QueryClientProvider client={createQueryClient()}>
        <PromptsForm />
      </QueryClientProvider>,
    );

    expect(markup).toMatch(/<div role="alert" class="[^"]+"><\/div><div class="[^"]+"><button type="submit"/);
  });

  it('takes no space while empty, and is not hidden', () => {
    renderForm();

    expect(alert()).toBeEmptyDOMElement();
    expect(alert()).toBeVisible();
    expect(alert()).not.toHaveStyle({ marginBlockEnd: 'var(--space-4)' });
  });

  it('says "no connection" when a press fails with status 0, with focus still on the button', async () => {
    serve(noConnection);
    renderForm();

    const button = pressSave();
    await settle();

    expect(alert().textContent).toBe('Not saved: no connection. Try again.');
    // Bold in the error colour, with the space between it and the buttons that it takes only while it holds words.
    expect(alert()).toHaveStyle({ fontWeight: '700', color: 'var(--colour-error)', marginBlockEnd: 'var(--space-4)' });
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(sent()).toBe(1);
  });

  it('sends a press at once while the browser reports offline, and says "no connection" when it fails', async () => {
    onlineManager.setOnline(false);
    serve(noConnection);
    renderForm();

    const button = pressSave();
    await settle();

    expect(sent()).toBe(1);
    expect(alert().textContent).toBe('Not saved: no connection. Try again.');
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
  });

  it('empties at once when the button is pressed again, and says it again when that press fails too', async () => {
    serve(noConnection);
    renderForm();
    const button = pressSave();
    await settle();
    const region = alert();
    expect(region.textContent).toBe('Not saved: no connection. Try again.');
    serve(noAnswer);

    pressSave();
    await wait(0);
    expect(region).toBeEmptyDOMElement();
    expect(button).toHaveAttribute('aria-disabled', 'true');

    // The wrapper's 10 s timeout, which fails it with status 0.
    await wait(10_000);
    await settle();
    expect(alert()).toBe(region);
    expect(region.textContent).toBe('Not saved: no connection. Try again.');
    expect(button).toHaveFocus();
    expect(sent()).toBe(2);
  });

  it('says "something went wrong" for a 503', async () => {
    serve(() => problem(503));
    renderForm();

    const button = pressSave();
    await settle();

    expect(alert().textContent).toBe('Not saved: something went wrong. Try again.');
    expect(button).toHaveFocus();
  });

  it('puts what is safe before "Try again."', async () => {
    serve(() => problem(503));
    renderForm(<PromptsForm safe="Your changes are still on this page." />);

    pressSave();
    await settle();

    expect(alert().textContent).toBe(
      'Not saved: something went wrong. Your changes are still on this page. Try again.',
    );
  });

  it('says nothing for a 401, which the session flow has', async () => {
    serve(() => problem(401));
    renderForm();

    const button = pressSave();
    await wait(60_000);

    expect(alert()).toBeEmptyDOMElement();
    expect(button).toHaveFocus();
    expect(button).not.toHaveAttribute('aria-disabled');
    expect(sent()).toBe(1);
  });
});
