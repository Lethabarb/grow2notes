import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { focusInsteadOfHeading, focusPageHeading, signedOutHeadingId } from './focusPageHeading.ts';
import { PageHeading } from './PageHeading.tsx';

afterEach(() => {
  vi.restoreAllMocks();
});

/** Renders a page with a button, focused, and no heading. */
function renderButtonFocused(): HTMLElement {
  render(<button type="button">Try again</button>);
  const button = screen.getByRole('button');
  button.focus();
  return button;
}

describe('PageHeading', () => {
  it('is the page heading, which script can focus but Tab does not reach, as wide as its text', () => {
    render(<PageHeading>Participants</PageHeading>);
    const heading = screen.getByRole('heading', { level: 1, name: 'Participants' });

    expect(heading).toHaveAttribute('id', 'page-heading');
    expect(heading).toHaveAttribute('tabindex', '-1');
    expect(heading).toHaveStyle({ inlineSize: 'fit-content', maxInlineSize: '100%' });
  });

  it("takes the signed-out-in-place view's own id, which focusPageHeading leaves for the page's", () => {
    render(
      <>
        <PageHeading id={signedOutHeadingId}>You’ve been signed out</PageHeading>
        <PageHeading>Participants</PageHeading>
      </>,
    );
    const signedOut = screen.getByRole('heading', { level: 1, name: 'You’ve been signed out' });

    focusPageHeading();

    expect(signedOut).toHaveAttribute('id', 'signed-out-heading');
    expect(signedOut).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('heading', { level: 1, name: 'Participants' })).toHaveFocus();
  });

  it("takes no focus pending for the page's as the signed-out view's, so the page's own takes it as it mounts", () => {
    const button = renderButtonFocused();
    focusPageHeading();

    render(<PageHeading id={signedOutHeadingId}>You’ve been signed out</PageHeading>);

    expect(button).toHaveFocus();
    render(<PageHeading>Jane Citizen</PageHeading>);
    expect(screen.getByRole('heading', { level: 1, name: 'Jane Citizen' })).toHaveFocus();
  });

  it('takes no focus as it mounts when none is pending', () => {
    const button = renderButtonFocused();

    render(<PageHeading>Participants</PageHeading>);

    expect(button).toHaveFocus();
  });
});

describe('focusPageHeading', () => {
  it('focuses the page heading, and leaves nothing pending', () => {
    render(
      <>
        <PageHeading>Participants</PageHeading>
        <button type="button">Try again</button>
      </>,
    );
    const button = screen.getByRole('button');
    button.focus();

    focusPageHeading();

    expect(screen.getByRole('heading', { level: 1 })).toHaveFocus();
    button.focus();
    render(<PageHeading>Participant</PageHeading>);
    expect(button).toHaveFocus();
  });

  it('leaves focus where it is on a page with no heading yet, for the heading that mounts next, once', () => {
    const button = renderButtonFocused();
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');

    focusPageHeading();

    expect(button).toHaveFocus();

    const { unmount } = render(<PageHeading>Jane Citizen</PageHeading>);
    const heading = screen.getByRole('heading', { level: 1 });
    expect(heading).toHaveFocus();
    // Where the page is, as the heading is not on a new screen's arrival, which the shell has scrolled already.
    expect(focus.mock.contexts.at(-1)).toBe(heading);
    expect(focus.mock.calls.at(-1)).toEqual([{ preventScroll: true }]);

    unmount();
    button.focus();
    render(<PageHeading>Jane Citizen</PageHeading>);
    expect(button).toHaveFocus();
  });
});

describe('focusInsteadOfHeading', () => {
  it('focuses the element, scrolling it into view, and takes away a focus pending for a heading', () => {
    render(<div tabIndex={-1}>Note for Jane Citizen submitted</div>);
    const banner = screen.getByText('Note for Jane Citizen submitted');
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');
    focusPageHeading();

    focusInsteadOfHeading(banner);

    expect(banner).toHaveFocus();
    expect(focus.mock.contexts.at(-1)).toBe(banner);
    expect(focus.mock.calls.at(-1)).toEqual([]);
    render(<PageHeading>Today</PageHeading>);
    expect(banner).toHaveFocus();
  });
});
