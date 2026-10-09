import styles from './Button.module.css';

export type ButtonVariant = 'primary' | 'secondary' | 'warning';

/**
 * The classes of one of the three button styles (primary-actions.md §1), which `Button` uses and which a link
 * that goes somewhere but looks like a button takes, so it is still announced as a link and can open in a new tab.
 */
export function buttonClass(variant: ButtonVariant = 'secondary'): string {
  return `${styles.button} ${styles[variant]}`;
}
