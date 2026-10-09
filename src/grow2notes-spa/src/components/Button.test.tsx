import { act, fireEvent, render, screen } from '@testing-library/react';
import { createRef, type FormEvent, type MouseEvent } from 'react';
import { afterEach, beforeEach, describe, expect, expectTypeOf, it, vi } from 'vitest';
import { Button } from './Button.tsx';
import { buttonClass, type ButtonVariant } from './buttonClass.ts';
import { PageStatus } from './PageStatus.tsx';

/** A form's submit handler that records each submit, and stops jsdom navigating, which it cannot do. */
function submitHandler() {
  return vi.fn((event: FormEvent) => event.preventDefault());
}

/** The fill, label colour and pointer of a button or a link styled as one. jsdom keeps a `var()` as written. */
function look(element: HTMLElement) {
  const style = getComputedStyle(element);
  return { fill: style.getPropertyValue('background'), label: style.getPropertyValue('color'), cursor: style.cursor };
}

/** The three styles' fills and labels (primary-actions.md §1). */
const looks = {
  primary: { fill: 'var(--colour-action)', label: 'var(--colour-on-action)', cursor: 'pointer' },
  secondary: { fill: 'var(--colour-surface)', label: 'var(--colour-action)', cursor: 'pointer' },
  warning: { fill: 'var(--colour-error)', label: 'var(--colour-on-action)', cursor: 'pointer' },
} satisfies Record<ButtonVariant, ReturnType<typeof look>>;

describe('Button', () => {
  it('is a button that submits nothing unless it is given a type', () => {
    const submit = submitHandler();
    render(
      <form onSubmit={submit}>
        <Button>Go back</Button>
        <Button type="submit">Send invite</Button>
      </form>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Go back' }));
    expect(screen.getByRole('button', { name: 'Go back' })).toHaveAttribute('type', 'button');
    expect(submit).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Send invite' }));
    expect(submit).toHaveBeenCalledOnce();
  });

  it('passes its ref, its class name and its other attributes to the button', () => {
    const ref = createRef<HTMLButtonElement>();
    render(
      <Button ref={ref} id="close" name="action" value="close" aria-label="Close" className="extra">
        ×
      </Button>,
    );
    const button = screen.getByRole('button', { name: 'Close' });

    expect(ref.current).toBe(button);
    expect(button).toHaveAttribute('id', 'close');
    expect(button).toHaveAttribute('name', 'action');
    expect(button).toHaveAttribute('value', 'close');
    expect(button.className).toBe(`${buttonClass()} extra`);
  });

  it('has the fill and label colours of its style, secondary unless given one', () => {
    render(
      <>
        <Button variant="primary">Invite user</Button>
        <Button variant="warning">Deactivate</Button>
        <Button>Edit</Button>
      </>,
    );

    expect(look(screen.getByRole('button', { name: 'Invite user' }))).toEqual(looks.primary);
    expect(look(screen.getByRole('button', { name: 'Deactivate' }))).toEqual(looks.warning);
    expect(look(screen.getByRole('button', { name: 'Edit' }))).toEqual(looks.secondary);
  });

  describe('when available', () => {
    it('calls onClick once on a press, with the press', () => {
      // currentTarget is the button only while the press is being handled, so the handler returns it.
      const click = vi.fn((event: MouseEvent<HTMLButtonElement>) => event.currentTarget);
      render(<Button onClick={click}>Go back</Button>);
      const button = screen.getByRole('button', { name: 'Go back' });

      fireEvent.click(button);

      expect(click).toHaveBeenCalledOnce();
      expect(click).toHaveReturnedWith(button);
    });

    it('calls onClick and submits its form when it is a submit button', () => {
      const submit = submitHandler();
      const click = vi.fn();
      render(
        <form onSubmit={submit}>
          <Button type="submit" onClick={click}>Send invite</Button>
        </form>,
      );

      fireEvent.click(screen.getByRole('button', { name: 'Send invite' }));

      expect(click).toHaveBeenCalledOnce();
      expect(submit).toHaveBeenCalledOnce();
    });
  });

  it('is never rendered with disabled, and its type refuses it', () => {
    expectTypeOf(Button).parameter(0).toHaveProperty('unavailable');
    // @ts-expect-error -- the props leave out disabled, which would take the button out of the tab order.
    expectTypeOf(Button).parameter(0).toHaveProperty('disabled');

    const { rerender } = render(<Button>Save</Button>);
    const button = screen.getByRole('button');
    expect(button).not.toHaveAttribute('disabled');
    expect(button).not.toHaveAttribute('aria-disabled');

    for (const state of [{ unavailable: true }, { busy: true }, { unavailable: true, busy: true }]) {
      rerender(<Button {...state}>Save</Button>);

      expect(button).not.toHaveAttribute('disabled');
      expect(button).toHaveAttribute('aria-disabled', 'true');
    }
  });

  describe('when unavailable', () => {
    it('has aria-disabled and its visible reason as its description, and stays focusable', () => {
      render(
        <>
          <p id="reason">No submitted notes for Thursday 1 October 2026.</p>
          <Button unavailable unavailableReasonId="reason">Download Word</Button>
        </>,
      );
      const button = screen.getByRole('button', { name: 'Download Word' });
      button.focus();

      expect(button).toHaveAttribute('aria-disabled', 'true');
      expect(button).toHaveAccessibleDescription('No submitted notes for Thursday 1 October 2026.');
      expect(button).toHaveFocus();
    });

    it('keeps a description of its own, after the reason', () => {
      const button = (unavailable: boolean) => (
        <>
          <p id="reason">Only an invited user can be sent a new invite.</p>
          <Button unavailable={unavailable} unavailableReasonId="reason" aria-describedby="hint">
            Resend invite
          </Button>
          <p id="hint">Sends a new setup link.</p>
        </>
      );
      const { rerender } = render(button(true));

      expect(screen.getByRole('button')).toHaveAccessibleDescription(
        'Only an invited user can be sent a new invite. Sends a new setup link.',
      );

      rerender(button(false));

      expect(screen.getByRole('button')).toHaveAccessibleDescription('Sends a new setup link.');
    });

    it('calls onUnavailablePress, not onClick, on a press, and submits no form', () => {
      const submit = submitHandler();
      const click = vi.fn();
      const unavailablePress = vi.fn();
      render(
        <form onSubmit={submit}>
          <Button type="submit" unavailable onClick={click} onUnavailablePress={unavailablePress}>
            Submit note
          </Button>
        </form>,
      );

      fireEvent.click(screen.getByRole('button', { name: 'Submit note' }));

      expect(unavailablePress).toHaveBeenCalledOnce();
      expect(click).not.toHaveBeenCalled();
      expect(submit).not.toHaveBeenCalled();
    });

    it('has the unavailable look, which an available or busy button does not take', () => {
      const submitNote = (state: { unavailable?: boolean; busy?: boolean }) => (
        <Button variant="primary" {...state}>Submit note</Button>
      );
      const { rerender } = render(submitNote({ unavailable: true }));
      const button = screen.getByRole('button');

      // The dashed border is checked by hand (task 4): jsdom drops a `border` that holds a `var()`.
      expect(button).toHaveAttribute('data-unavailable');
      expect(look(button)).toEqual({
        fill: 'var(--colour-surface-muted)',
        label: 'var(--colour-text-secondary)',
        cursor: 'not-allowed',
      });

      for (const state of [{}, { busy: true }, { unavailable: true, busy: true }]) {
        rerender(submitNote(state));

        expect(button).not.toHaveAttribute('data-unavailable');
        expect(look(button)).toEqual(looks.primary);
      }
    });
  });

  describe('when busy', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('calls nothing on a press, and submits no form', () => {
      const submit = submitHandler();
      const click = vi.fn();
      render(
        <form onSubmit={submit}>
          <Button type="submit" busy busyLabel="Sending…" onClick={click}>Send invite</Button>
        </form>,
      );

      fireEvent.click(screen.getByRole('button', { name: 'Send invite' }));

      expect(click).not.toHaveBeenCalled();
      expect(submit).not.toHaveBeenCalled();
    });

    it('calls nothing on a press while unavailable too, not even onUnavailablePress', () => {
      const submit = submitHandler();
      const click = vi.fn();
      const unavailablePress = vi.fn();
      render(
        <form onSubmit={submit}>
          <Button type="submit" busy busyLabel="Sending…" unavailable onClick={click}
            onUnavailablePress={unavailablePress}>
            Send invite
          </Button>
        </form>,
      );

      fireEvent.click(screen.getByRole('button', { name: 'Send invite' }));

      expect(click).not.toHaveBeenCalled();
      expect(unavailablePress).not.toHaveBeenCalled();
      expect(submit).not.toHaveBeenCalled();
    });

    it('shows its busy label after 400 ms, in the same grid cell, and writes it to the page status region', () => {
      render(
        <PageStatus>
          <Button busy busyLabel="Saving changes…">Save changes</Button>
        </PageStatus>,
      );
      const button = screen.getByRole('button');
      const label = screen.getByText('Save changes');
      const busyLabel = screen.getByText('Saving changes…');
      const region = screen.getByRole('status');

      act(() => {
        vi.advanceTimersByTime(399);
      });

      expect(button).toHaveAccessibleName('Save changes');
      expect(label).toBeVisible();
      expect(busyLabel).not.toBeVisible();
      expect(region).toBeEmptyDOMElement();

      act(() => {
        vi.advanceTimersByTime(1);
      });

      expect(button).toHaveAccessibleName('Saving changes…');
      expect(busyLabel).toBeVisible();
      expect(label).not.toBeVisible();
      // Both labels stay in the button, in one grid cell, so that it keeps the width of the longer one.
      expect(button).toContainElement(label);
      expect(button).toContainElement(busyLabel);
      expect(label).toHaveStyle({ gridArea: '1 / 1' });
      expect(busyLabel).toHaveStyle({ gridArea: '1 / 1' });

      act(() => {
        vi.advanceTimersToNextFrame();
      });

      expect(region).toHaveTextContent('Saving changes…');
    });

    it('writes nothing to the page status region with announceBusy={false}', () => {
      render(
        <PageStatus>
          <Button busy busyLabel="Saving…" announceBusy={false}>Save</Button>
        </PageStatus>,
      );

      act(() => {
        vi.advanceTimersByTime(400);
        vi.advanceTimersToNextFrame();
      });

      expect(screen.getByText('Saving…')).toBeVisible();
      expect(screen.getByRole('status')).toBeEmptyDOMElement();
    });

    it('shows and writes nothing when it stops being busy before 400 ms', () => {
      const { rerender } = render(
        <PageStatus>
          <Button busy busyLabel="Saving…">Save</Button>
        </PageStatus>,
      );

      act(() => {
        vi.advanceTimersByTime(399);
      });
      rerender(
        <PageStatus>
          <Button busyLabel="Saving…">Save</Button>
        </PageStatus>,
      );
      act(() => {
        vi.advanceTimersByTime(1000);
        vi.advanceTimersToNextFrame();
      });

      expect(screen.getByRole('button')).toHaveAccessibleName('Save');
      expect(screen.getByText('Saving…')).not.toBeVisible();
      expect(screen.getByRole('status')).toBeEmptyDOMElement();
    });

    it('waits 400 ms again each time it is busy', () => {
      const page = (busy: boolean) => (
        <PageStatus>
          <Button busy={busy} busyLabel="Saving…">Save</Button>
        </PageStatus>
      );
      const { rerender } = render(page(true));
      act(() => {
        vi.advanceTimersByTime(400);
      });
      rerender(page(false));
      expect(screen.getByRole('button')).toHaveAccessibleName('Save');

      rerender(page(true));
      act(() => {
        vi.advanceTimersByTime(399);
      });

      expect(screen.getByRole('button')).toHaveAccessibleName('Save');
    });

    it('shows its busy label outside every PageStatus too, with nowhere to announce it', () => {
      render(<Button busy busyLabel="Saving…">Save</Button>);

      act(() => {
        vi.advanceTimersByTime(400);
        vi.advanceTimersToNextFrame();
      });

      expect(screen.getByRole('button')).toHaveAccessibleName('Saving…');
    });
  });
});

describe('buttonClass', () => {
  it('gives each style the shared class and one of its own', () => {
    const classes = (['primary', 'secondary', 'warning'] as const).map((variant) => buttonClass(variant).split(' '));

    for (const names of classes) {
      expect(names).toHaveLength(2);
      expect(names).not.toContain('undefined');
    }
    expect(new Set(classes.map(([shared]) => shared)).size).toBe(1);
    expect(new Set(classes.map(([, own]) => own)).size).toBe(3);
  });

  it('styles a link as the button of the same style, secondary unless given one', () => {
    render(
      <>
        <a href="/users/new" className={buttonClass('primary')}>Invite user</a>
        <a href="/reports/daily" className={buttonClass()}>Daily report</a>
      </>,
    );

    expect(look(screen.getByRole('link', { name: 'Invite user' }))).toEqual(looks.primary);
    expect(look(screen.getByRole('link', { name: 'Daily report' }))).toEqual(looks.secondary);
  });
});
