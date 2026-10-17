import { render, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { focusPageHeading } from './focusPageHeading.ts';
import { Notice } from './Notice.tsx';
import { PageHeading } from './PageHeading.tsx';

// participants.md's archived notice, shortened.
const words = 'Jane Citizen is archived, so this page is read-only.';

afterEach(() => {
  vi.restoreAllMocks();
});

/** Renders `notice` alone, and gives its box. */
function renderNotice(notice: ReactElement): HTMLElement {
  const { container } = render(notice);
  return container.firstElementChild as HTMLElement;
}

describe('Notice', () => {
  it('is plain page content: its words in a paragraph, an icon hidden from screen readers, no role or tab stop', () => {
    const notice = renderNotice(<Notice>{words}</Notice>);

    expect(screen.getByText(words).tagName).toBe('P');
    expect(notice.textContent).toBe(words);
    expect(notice.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
    for (const attribute of ['role', 'aria-live', 'tabindex', 'data-route-focus']) {
      expect(notice).not.toHaveAttribute(attribute);
    }
    expect(notice).not.toHaveFocus();
  });

  it('puts its action after its words', () => {
    renderNotice(<Notice action={<button type="button">Restore</button>}>{words}</Notice>);

    const restore = screen.getByRole('button', { name: 'Restore' });
    expect(screen.getByText(words).compareDocumentPosition(restore) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('takes focus as it appears when it is to, scrolling to it, in place of a focus pending for the <h1>', () => {
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');
    // A page with no <h1> yet, whose next one would take the focus.
    focusPageHeading();

    const notice = renderNotice(
      <Notice takesFocus action={<button type="button">Restore</button>}>
        {words}
      </Notice>,
    );

    expect(notice).toHaveFocus();
    expect(notice).toHaveAttribute('tabindex', '-1');
    expect(focus.mock.contexts.at(-1)).toBe(notice);
    expect(focus.mock.calls.at(-1)?.[0]).toBeUndefined();
    render(<PageHeading>Jane Citizen</PageHeading>);
    expect(notice).toHaveFocus();
  });
});
