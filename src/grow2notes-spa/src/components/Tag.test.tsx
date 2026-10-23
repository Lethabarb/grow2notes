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

  it('is Edited in the neutral tone, as plain text after Flagged in a row', () => {
    render(
      <p>
        Submitted <Tag kind="flagged" /> <Tag kind="edited" />
      </p>,
    );

    const tag = screen.getByText('Edited');
    expect(tag.tagName).toBe('SPAN');
    expect(tag).toHaveClass(styles.tag, styles.neutral);
    expect(tag).not.toHaveClass(styles.attention);
    expect(tag.closest('p')).toHaveTextContent(/^Submitted Flagged Edited$/);
    for (const attribute of ['role', 'aria-label', 'tabindex', 'title']) {
      expect(tag).not.toHaveAttribute(attribute);
    }
  });

  it('is To review in the attention tone and Reviewed in the neutral tone, as plain text', () => {
    render(
      <p>
        <Tag kind="toReview" /> <Tag kind="reviewed" />
      </p>,
    );

    const toReview = screen.getByText('To review');
    expect(toReview).toHaveClass(styles.tag, styles.attention);
    const reviewed = screen.getByText('Reviewed');
    expect(reviewed).toHaveClass(styles.tag, styles.neutral);
    for (const tag of [toReview, reviewed]) {
      expect(tag.tagName).toBe('SPAN');
      for (const attribute of ['role', 'aria-label', 'tabindex', 'title']) {
        expect(tag).not.toHaveAttribute(attribute);
      }
    }
  });
});

describe('Tag, Past-day note', () => {
  it('is Past-day note in the neutral tone, as plain text after a pause', () => {
    render(
      <p>
        <Tag kind="pastDay" /> written by Jo Smith
      </p>,
    );

    const tag = screen.getByText('Past-day note');
    expect(tag.tagName).toBe('SPAN');
    expect(tag).toHaveClass(styles.tag, styles.neutral);
    expect(tag).not.toHaveClass(styles.attention);
    for (const attribute of ['role', 'aria-label', 'tabindex', 'title']) {
      expect(tag).not.toHaveAttribute(attribute);
    }
  });
});
