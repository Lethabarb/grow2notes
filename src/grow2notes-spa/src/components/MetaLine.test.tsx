import { render } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import linkRow from './LinkRow.module.css';
import { MetaLine } from './MetaLine.tsx';
import styles from './MetaLine.module.css';

const nbsp = ' ';

/** Renders `line` in a row's detail, whose small secondary text a meta line takes, and gives the detail. */
function renderInDetail(line: ReactNode): HTMLElement {
  const { container } = render(<p className={linkRow.detail}>{line}</p>);
  return container.querySelector('p')!;
}

describe('MetaLine', () => {
  // As Today's rows and Past notes' have it (TodayPage.test.tsx, PastNotesPage.test.tsx).
  it('sets its first part apart as the status word, bold and in the text colour, by default', () => {
    const detail = renderInDetail(<MetaLine parts={['Submitted', 'Alex P.']} />);

    const [status, author] = Array.from(detail.querySelectorAll(`.${styles.part}`));
    const word = status.querySelector(`.${styles.status}`);
    expect(word).toHaveTextContent(/^Submitted$/);
    expect(word).toHaveStyle({ fontWeight: '700', color: 'var(--colour-text)' });
    expect(author.querySelector(`.${styles.status}`)).toBeNull();
    expect(author).toHaveStyle({ color: 'var(--colour-text-secondary)' });
    expect(author).not.toHaveStyle({ fontWeight: '700' });
  });

  // A To review row's "{author} · flagged {time}" (flagged.md item 4 and *Cross-cutting*).
  it('with statusFirst false, sets no part apart: none in the status style, so none bold or in the text colour', () => {
    const detail = renderInDetail(
      <MetaLine
        statusFirst={false}
        parts={[
          'Priya Nair',
          <>
            flagged <time dateTime="2026-09-29T06:42:00Z">4:42 pm</time>
          </>,
        ]}
      />,
    );

    expect(detail.querySelector(`.${styles.status}`)).toBeNull();
    const parts = Array.from(detail.querySelectorAll(`.${styles.part}`));
    expect(parts.map((part) => part.textContent)).toEqual([`Priya Nair${nbsp}·, `, 'flagged 4:42 pm']);
    for (const element of detail.querySelectorAll('*')) {
      expect(element).toHaveStyle({ color: 'var(--colour-text-secondary)' });
      expect(element).not.toHaveStyle({ fontWeight: '700' });
    }
  });
});
