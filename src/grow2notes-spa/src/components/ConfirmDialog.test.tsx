import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { StrictMode, type ComponentProps } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { buttonClass } from './buttonClass.ts';
import { ConfirmDialog } from './ConfirmDialog.tsx';

// Guide prompts' leave dialog (guide-prompts.md component 7), whose top button is the safe one and has focus, and
// whose bottom button confirms.
const title = 'Leave without saving?';
const body = 'Your changes to the guide prompts are not saved.';
const stay = 'Stay on this page';
const leave = 'Leave without saving';

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

function dialog() {
  const element = screen.getByRole('alertdialog');
  if (!(element instanceof HTMLDialogElement)) {
    throw new Error('The alert dialog is not a <dialog>.');
  }

  return element;
}

/** Runs the task in which the browser fires `close`, which vitest.setup.ts's stand-in for close() queues. */
async function closeEventFired() {
  await act(() => vi.runAllTimersAsync());
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
    expect(within(dialog()).getByRole('heading', { level: 2 })).toHaveTextContent(title);
  });

  it('has two buttons, the primary on top and the secondary under it, that submit nothing', () => {
    renderDialog();

    const buttons = within(dialog()).getAllByRole('button');
    expect(buttons.map((button) => button.textContent)).toEqual([stay, leave]);
    expect(buttons[0].className).toContain(buttonClass('primary'));
    expect(buttons[1].className).toContain(buttonClass('secondary'));
    for (const button of buttons) {
      expect(button).toHaveAttribute('type', 'button');
      expect(button).not.toHaveAttribute('disabled');
    }
  });

  it.each<Setup & { focused: string }>([
    { confirmButton: 'secondary', initialFocus: 'primary', focused: stay },
    { confirmButton: 'primary', initialFocus: 'secondary', focused: leave },
  ])('puts focus on the $initialFocus button as it opens', ({ focused, ...setup }) => {
    renderDialog(setup);

    expect(screen.getByRole('button', { name: focused })).toHaveFocus();
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
    const cancel = new Event('cancel', { cancelable: true });

    // As the browser does for Escape or Android's back gesture: `cancel`, then, unless it was stopped, the close.
    fireEvent(dialog(), cancel);
    expect(cancel.defaultPrevented).toBe(false);
    dialog().close();
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
