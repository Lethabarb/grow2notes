import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { focusPageHeading } from './focusPageHeading.ts';
import { PageHeading } from './PageHeading.tsx';

describe('PageHeading', () => {
  it('is the page heading, which script can focus but Tab does not reach, as wide as its text', () => {
    render(<PageHeading>Participants</PageHeading>);
    const heading = screen.getByRole('heading', { level: 1, name: 'Participants' });

    expect(heading).toHaveAttribute('id', 'page-heading');
    expect(heading).toHaveAttribute('tabindex', '-1');
    expect(heading).toHaveStyle({ inlineSize: 'fit-content', maxInlineSize: '100%' });
  });
});

describe('focusPageHeading', () => {
  it('focuses the page heading', () => {
    render(
      <>
        <PageHeading>Participants</PageHeading>
        <button type="button">Try again</button>
      </>,
    );
    screen.getByRole('button').focus();

    focusPageHeading();

    expect(screen.getByRole('heading', { level: 1 })).toHaveFocus();
  });

  it('leaves focus where it is on a page with no heading yet', () => {
    render(<button type="button">Try again</button>);
    const button = screen.getByRole('button');
    button.focus();

    focusPageHeading();

    expect(button).toHaveFocus();
  });
});
