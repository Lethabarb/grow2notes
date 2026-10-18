import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Tag } from './Tag.tsx';
import styles from './Tag.module.css';

describe('Tag', () => {
  it('says Flagged as plain text in the reading order, with no role, name of its own, tooltip or tab stop', () => {
    render(
      <p>
        Submitted <Tag kind="flagged" />
      </p>,
    );

    const tag = screen.getByText('Flagged');
    expect(tag.tagName).toBe('SPAN');
    expect(tag.closest('p')).toHaveTextContent(/^Submitted Flagged$/);
    for (const attribute of ['role', 'aria-label', 'aria-live', 'aria-hidden', 'tabindex', 'title']) {
      expect(tag).not.toHaveAttribute(attribute);
    }
  });

  it('is Flagged in the attention tone, inline-block, to drop whole to the next line, at regular weight', () => {
    render(<Tag kind="flagged" />);

    const tag = screen.getByText('Flagged');
    expect(tag).toHaveClass(styles.tag, styles.attention);
    expect(tag).toHaveStyle({ display: 'inline-block', fontWeight: '400', padding: '0.125em 0.5em' });
    expect(tag).not.toHaveStyle({ whiteSpace: 'nowrap' });
    expect(tag).not.toHaveStyle({ textOverflow: 'ellipsis' });
  });
});
