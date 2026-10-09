import type { ReactNode } from 'react';
import styles from './ButtonGroup.module.css';

/**
 * Lays out a set of buttons and button-like links: stacked on a phone and in a row from 40rem. The order on screen is
 * the order in the DOM, so the primary action goes first.
 */
export function ButtonGroup({ children }: { children: ReactNode }) {
  return <div className={styles.group}>{children}</div>;
}
