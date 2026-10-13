import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SummaryList } from './SummaryList.tsx';
import { SummaryRow } from './SummaryRow.tsx';

/** Renders a summary list of a made-up person's two facts, and gives the `<dl>`. */
function renderList(): Element {
  const { container } = render(
    <SummaryList>
      <SummaryRow term="Email address">jane.citizen@example.org</SummaryRow>
      <SummaryRow term="Role">Worker</SummaryRow>
    </SummaryList>,
  );
  // A <dl> has no role to find it by.
  return container.children[0];
}

describe('SummaryList', () => {
  it('is a <dl> of a <div> for each row, holding the term as its <dt> and the fact as its <dd>, in order', () => {
    const list = renderList();

    expect(list.tagName).toBe('DL');
    expect(Array.from(list.children, (row) => row.tagName)).toEqual(['DIV', 'DIV']);
    expect(
      Array.from(list.children, (row) => Array.from(row.children, (cell) => `${cell.tagName} ${cell.textContent}`)),
    ).toEqual([
      ['DT Email address', 'DD jane.citizen@example.org'],
      ['DT Role', 'DD Worker'],
    ]);
  });

  it('gives screen readers each term and its fact', () => {
    renderList();

    expect(screen.getAllByRole('term').map((term) => term.textContent)).toEqual(['Email address', 'Role']);
    expect(screen.getAllByRole('definition').map((fact) => fact.textContent)).toEqual([
      'jane.citizen@example.org',
      'Worker',
    ]);
  });
});
