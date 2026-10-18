import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode, type ComponentProps } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { buttonClass } from './buttonClass.ts';
import { ConfirmDialog } from './ConfirmDialog.tsx';
import { closeOpenDialogs } from './openDialogs.ts';

// Guide prompts' leave dialog (guide-prompts.md component 7), whose top button is the safe one and has focus, and
// whose bottom button confirms.
const title = 'Leave without saving?';
const body = 'Your changes to the guide prompts are not saved.';
const stay = 'Stay on this page';
const leave = 'Leave without saving';

// users.md's Reset sign-in dialog for Alex Park (*Dialogs*), whose top button confirms by sending a request, and
// which opens on its title.
const resetTitle = 'Reset sign-in for Alex Park?';
const resetBody = 'Only do this after you have checked, by phone or in person, that Alex Park is the one asking.';
const reset = 'Reset sign-in for Alex Park';
const goBack = 'Go back';
const resetting = 'Resetting sign-in…';
const notReset = 'Not reset: no connection. Try again.';
const stale = 'Someone else has just changed Alex Park’s account. Go back to see the latest details.';

type Setup = Pick<ComponentProps<typeof ConfirmDialog>, 'confirmButton' | 'initialFocus'>;

const leaveDialog: Setup = { confirmButton: 'secondary', initialFocus: 'primary' };

/** Renders the dialog under StrictMode, as `main.tsx` renders the app, and gives its `onClose`. */
function renderDialog({ confirmButton, initialFocus }: Setup = leaveDialog) {
  const onClose = vi.fn<(confirmed: boolean) => void>();
  render(
    <StrictMode>
      <ConfirmDialog
        title={title}
        primary={stay}
        secondary={leave}
        confirmButton={confirmButton}
        initialFocus={initialFocus}
        onClose={onClose}
      >
        <p>{body}</p>
      </ConfirmDialog>
    </StrictMode>,
  );
  return onClose;
}

/** How the page shows the reset's request, which it changes as the request goes. */
type RequestState = Pick<ComponentProps<typeof ConfirmDialog>, 'busy' | 'error' | 'canConfirm'>;

/**
 * Renders the Reset sign-in dialog under StrictMode, in `tone`, the warning one unless given, and gives its
 * `onConfirm` and `onClose`, and a function that renders it again with the request in a new state.
 */
function renderResetDialog(tone: ComponentProps<typeof ConfirmDialog>['tone'] = 'warning') {
  const onConfirm = vi.fn<() => void>();
  const onClose = vi.fn<(confirmed: boolean) => void>();
  const withState = (state: RequestState) => (
    <StrictMode>
      <ConfirmDialog
        title={resetTitle}
        primary={reset}
        secondary={goBack}
        confirmButton="primary"
        initialFocus="title"
        tone={tone}
        busyLabel={resetting}
        onConfirm={onConfirm}
        onClose={onClose}
        {...state}
      >
        <p>{resetBody}</p>
      </ConfirmDialog>
    </StrictMode>
  );
  const { rerender } = render(withState({}));
  return { onConfirm, onClose, show: (state: RequestState) => rerender(withState(state)) };
}

function dialog() {
  const element = screen.getByRole('alertdialog');
  if (!(element instanceof HTMLDialogElement)) {
    throw new Error('The alert dialog is not a <dialog>.');
  }

  return element;
}

/** The dialog's buttons, in order. */
const buttons = () => within(dialog()).getAllByRole('button');

/** Its `<h2>`. */
const heading = () => within(dialog()).getByRole('heading', { level: 2 });

/** Focuses the confirming button and presses it, as a person does. */
function pressReset() {
  const button = within(dialog()).getByRole('button', { name: reset });
  button.focus();
  fireEvent.click(button);
  return button;
}

/** Runs the task in which the browser fires `close`, which vitest.setup.ts's stand-in for close() queues. */
async function closeEventFired() {
  await act(() => vi.runAllTimersAsync());
}

/** Moves the fake clock on by `ms`, running each timer and render due by then. */
async function wait(ms: number) {
  await act(() => vi.advanceTimersByTimeAsync(ms));
}

/** As the browser does for Escape or Android's back gesture: `cancel`, then the close, unless `cancel` was stopped. */
function closeRequest(element: HTMLDialogElement) {
  const cancel = new Event('cancel', { cancelable: true });
  fireEvent(element, cancel);
  if (!cancel.defaultPrevented) {
    element.close();
  }

  return cancel;
}

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe('ConfirmDialog', () => {
  it('opens with showModal() as it mounts, once under StrictMode, as an alert dialog with its title and body', () => {
    const showModal = vi.spyOn(HTMLDialogElement.prototype, 'showModal');
    renderDialog();

    expect(showModal).toHaveBeenCalledOnce();
    expect(dialog()).toHaveAttribute('open');
    expect(dialog()).toHaveAttribute('aria-modal', 'true');
    expect(dialog()).toHaveAccessibleName(title);
    expect(dialog()).toHaveAccessibleDescription(body);
    expect(heading()).toHaveTextContent(title);
  });

  it('has two buttons, the primary on top and the secondary under it, that submit nothing', () => {
    renderDialog();

    expect(buttons().map((button) => button.textContent)).toEqual([stay, leave]);
    expect(buttons()[0].className).toContain(buttonClass('primary'));
    expect(buttons()[1].className).toContain(buttonClass('secondary'));
    for (const button of buttons()) {
      expect(button).toHaveAttribute('type', 'button');
      expect(button).not.toHaveAttribute('disabled');
    }
  });

  it.each<Setup & { focused: string }>([
    { confirmButton: 'secondary', initialFocus: 'primary', focused: stay },
    { confirmButton: 'primary', initialFocus: 'secondary', focused: leave },
  ])('puts focus on the $initialFocus button as it opens, leaving the title out of focus', ({ focused, ...setup }) => {
    renderDialog(setup);

    const button = screen.getByRole('button', { name: focused });
    expect(button).toHaveFocus();
    expect(button).toHaveAttribute('autofocus');
    expect(heading()).not.toHaveAttribute('tabindex');
  });

  it.each([
    { dialogName: 'the leave dialog', open: () => renderDialog() },
    { dialogName: 'the Reset sign-in dialog', open: () => renderResetDialog() },
  ])('has, in $dialogName, its alert line above the buttons and its status line under them, empty', ({ open }) => {
    open();

    const alert = within(dialog()).getByRole('alert');
    const status = within(dialog()).getByRole('status');
    expect(alert.localName).toBe('p');
    expect(alert).toBeEmptyDOMElement();
    expect(alert.compareDocumentPosition(buttons()[0])).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(status.localName).toBe('p');
    expect(status).toBeEmptyDOMElement();
    expect(buttons()[1].compareDocumentPosition(status)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  it('closes with true, once, when its confirming button is pressed', async () => {
    const onClose = renderDialog();
    const opened = dialog();

    fireEvent.click(screen.getByRole('button', { name: leave }));
    expect(opened).not.toHaveAttribute('open');
    await closeEventFired();

    expect(onClose.mock.calls).toEqual([[true]]);
  });

  it('closes with false, once, when its other button is pressed', async () => {
    const onClose = renderDialog();
    const opened = dialog();

    fireEvent.click(screen.getByRole('button', { name: stay }));
    expect(opened).not.toHaveAttribute('open');
    await closeEventFired();

    expect(onClose.mock.calls).toEqual([[false]]);
  });

  it('confirms with whichever button it is given', async () => {
    const onClose = renderDialog({ confirmButton: 'primary', initialFocus: 'secondary' });

    fireEvent.click(screen.getByRole('button', { name: stay }));
    await closeEventFired();

    expect(onClose.mock.calls).toEqual([[true]]);
  });

  it('lets Escape\'s cancel through, and takes the close that follows as the safe choice', async () => {
    const onClose = renderDialog();

    expect(closeRequest(dialog()).defaultPrevented).toBe(false);
    await closeEventFired();

    expect(onClose.mock.calls).toEqual([[false]]);
  });

  it('takes a plain close(), as the shell gives an open dialog before its warning, as the safe choice', async () => {
    const onClose = renderDialog();

    dialog().close();
    await closeEventFired();

    expect(onClose.mock.calls).toEqual([[false]]);
  });
});

describe('ConfirmDialog that sends a request', () => {
  it('opens on its title, which script alone can focus, with no ring round it', () => {
    renderResetDialog();

    expect(heading()).toHaveTextContent(resetTitle);
    expect(heading()).toHaveFocus();
    expect(heading()).toHaveAttribute('tabindex', '-1');
    expect(heading()).toHaveAttribute('autofocus');
    expect(getComputedStyle(heading()).getPropertyValue('outline')).toBe('none');
    expect(dialog()).toHaveAccessibleName(resetTitle);
    expect(dialog()).toHaveAccessibleDescription(resetBody);
  });

  it('gives an Enter pressed as it opens no button to press, sending nothing and closing nothing', async () => {
    const { onConfirm, onClose } = renderResetDialog();

    const focused = document.activeElement ?? document.body;
    expect(focused).toBe(heading());
    fireEvent.keyDown(focused, { key: 'Enter' });
    fireEvent.keyUp(focused, { key: 'Enter' });
    await closeEventFired();

    expect(onConfirm).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    expect(dialog()).toHaveAttribute('open');
  });

  it('has its confirming button on top and Go back under it, neither disabled', () => {
    renderResetDialog();

    expect(buttons().map((button) => button.textContent)).toEqual([reset, goBack]);
    expect(buttons()[1].className).toContain(buttonClass('secondary'));
    expect(document.querySelector('[disabled], [aria-disabled]')).toBeNull();
  });

  it.each([
    { tone: 'warning', variant: 'warning', fill: 'var(--colour-error)' },
    { tone: 'standard', variant: 'primary', fill: 'var(--colour-action)' },
  ] as const)('draws its confirming button in the $variant style in the $tone tone', ({ tone, variant, fill }) => {
    renderResetDialog(tone);

    const confirm = within(dialog()).getByRole('button', { name: reset });
    expect(confirm.className).toContain(buttonClass(variant));
    // jsdom keeps a `var()` as written.
    expect(getComputedStyle(confirm).getPropertyValue('background')).toBe(fill);
  });

  it('calls onConfirm for its confirming button, staying open, and closes with false on Go back', async () => {
    const { onConfirm, onClose } = renderResetDialog();

    pressReset();
    await closeEventFired();

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onClose).not.toHaveBeenCalled();
    expect(dialog()).toHaveAttribute('open');

    fireEvent.click(within(dialog()).getByRole('button', { name: goBack }));
    await closeEventFired();
    expect(onClose.mock.calls).toEqual([[false]]);
  });

  it('while busy, has both buttons aria-disabled with their labels, and lets no press or Escape through', async () => {
    const { onConfirm, onClose, show } = renderResetDialog();
    const confirm = pressReset();
    show({ busy: true });

    for (const button of buttons()) {
      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(button).not.toHaveAttribute('disabled');
    }
    expect(buttons().map((button) => button.textContent)).toEqual([reset, goBack]);
    fireEvent.click(confirm);
    fireEvent.click(within(dialog()).getByRole('button', { name: goBack }));
    expect(closeRequest(dialog()).defaultPrevented).toBe(true);
    await wait(5_000);

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onClose).not.toHaveBeenCalled();
    expect(dialog()).toHaveAttribute('open');
    expect(confirm).toHaveFocus();
    expect(buttons().map((button) => button.textContent)).toEqual([reset, goBack]);
  });

  it('says its busy line from 1,000 ms of busy, and not at 999 ms, and empties it once the request ends', async () => {
    const { show } = renderResetDialog();
    pressReset();
    show({ busy: true });
    const status = within(dialog()).getByRole('status');

    await wait(999);
    expect(status).toBeEmptyDOMElement();
    await wait(1);
    expect(status).toHaveTextContent(resetting);

    show({ error: notReset });
    expect(status).toBeEmptyDOMElement();
  });

  it('shows an error in its alert line, both buttons ready again and focus kept on the confirming one', async () => {
    const { onConfirm, show } = renderResetDialog();
    const confirm = pressReset();
    show({ busy: true });
    await wait(2_000);

    show({ error: notReset });

    expect(within(dialog()).getByRole('alert')).toHaveTextContent(notReset);
    for (const button of buttons()) {
      expect(button).not.toHaveAttribute('aria-disabled');
    }
    expect(confirm).toHaveFocus();
    fireEvent.click(confirm);
    expect(onConfirm).toHaveBeenCalledTimes(2);
  });

  it('once confirming is no longer possible, takes the confirming button away and puts focus on Go back', async () => {
    const { onClose, show } = renderResetDialog();
    pressReset();
    show({ busy: true });

    show({ error: stale, canConfirm: false });

    expect(within(dialog()).getByRole('alert')).toHaveTextContent(stale);
    expect(buttons().map((button) => button.textContent)).toEqual([goBack]);
    expect(buttons()[0]).toHaveFocus();
    expect(buttons()[0]).not.toHaveAttribute('aria-disabled');

    fireEvent.click(buttons()[0]);
    await closeEventFired();
    expect(onClose.mock.calls).toEqual([[false]]);
  });
});

describe('closeOpenDialogs', () => {
  it('closes every open confirmation, a busy one too, each as its safe choice, and no other dialog', async () => {
    const onLeaveClose = renderDialog();
    const { onConfirm, onClose: onResetClose, show } = renderResetDialog();
    fireEvent.click(screen.getByRole('button', { name: reset }));
    show({ busy: true });
    // A dialog that is not a confirmation, as the session warning is.
    const other = document.body.appendChild(document.createElement('dialog'));
    other.showModal();
    const confirmations = screen.getAllByRole('alertdialog');
    expect(confirmations).toHaveLength(2);

    closeOpenDialogs();

    for (const confirmation of confirmations) {
      expect(confirmation).not.toHaveAttribute('open');
    }
    expect(other).toHaveAttribute('open');
    await closeEventFired();
    expect(onLeaveClose.mock.calls).toEqual([[false]]);
    expect(onResetClose.mock.calls).toEqual([[false]]);
    expect(onConfirm).toHaveBeenCalledOnce();
    other.remove();
  });

  it('closes none that has unmounted, and does nothing more to one already closed', async () => {
    const onClose = renderDialog();
    const { unmount } = render(
      <ConfirmDialog
        title={resetTitle}
        primary={reset}
        secondary={goBack}
        confirmButton="primary"
        initialFocus="title"
        onClose={vi.fn()}
      >
        <p>{resetBody}</p>
      </ConfirmDialog>,
    );
    const gone = screen.getByRole('alertdialog', { name: resetTitle });
    unmount();
    fireEvent.click(screen.getByRole('button', { name: stay }));
    await closeEventFired();
    const close = vi.spyOn(HTMLDialogElement.prototype, 'close');

    closeOpenDialogs();
    await closeEventFired();

    expect(close.mock.contexts).not.toContain(gone);
    expect(onClose.mock.calls).toEqual([[false]]);
  });
});

// note-form.md's Submit confirmation (component 14), whose title is its words and then the participant's name.
describe('ConfirmDialog with a name line', () => {
  const lead = 'Submit today’s note for';

  /** Renders the Submit confirmation for `name`, opened on its title. */
  function renderSubmit(name: string) {
    render(
      <StrictMode>
        <ConfirmDialog
          title={lead}
          titleName={name}
          primary={`Submit note for ${name}`}
          secondary={goBack}
          confirmButton="primary"
          initialFocus="title"
          onConfirm={vi.fn()}
          onClose={vi.fn()}
        >
          <p>Thursday 1 October 2026</p>
          <p>Flagged for manager: No</p>
        </ConfirmDialog>
      </StrictMode>,
    );
  }

  it('names the dialog with its words and the name, the name on a line of its own, never translated', () => {
    renderSubmit('Jane Citizen');

    expect(dialog()).toHaveAccessibleName('Submit today’s note for Jane Citizen');
    expect(heading()).toHaveFocus();
    const name = within(heading()).getByText('Jane Citizen');
    expect(name.tagName).toBe('SPAN');
    expect(name).toHaveAttribute('translate', 'no');
    expect(name).toHaveStyle({ display: 'block', fontSize: 'var(--font-size-h1)', fontWeight: '700' });
    expect(dialog()).toHaveAccessibleDescription('Thursday 1 October 2026 Flagged for manager: No');
  });

  it('wraps an unbroken name of 100 characters anywhere, never upper-casing it or cutting it short', () => {
    const long = 'X'.repeat(100);
    renderSubmit(long);

    const name = within(heading()).getByText(long);
    expect(name).toHaveStyle({ overflowWrap: 'anywhere' });
    for (const cut of [{ textTransform: 'uppercase' }, { textOverflow: 'ellipsis' }, { whiteSpace: 'nowrap' }]) {
      expect(name).not.toHaveStyle(cut);
    }
  });
});
