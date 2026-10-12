import { strings } from '../../copy/strings.ts';
import styles from './SetupKey.module.css';

/**
 * The setup key (sign-in.md component 16; totp-setup.md): the term "Setup key", then the key in capitals, in groups of
 * four, which a page translation never changes, then its note. There is no Copy button, so the app never puts the key
 * on the clipboard (D68), but a person may copy it by hand: a touch or a click selects the whole key, and as the groups
 * hold no space characters between them, it copies as one word.
 */
export function SetupKey({ sharedKey }: { sharedKey: string }) {
  const groups = sharedKey.toUpperCase().match(/.{1,4}/g) ?? [];

  return (
    <>
      <dl className={styles.setupKey}>
        <dt className={styles.term}>{strings.setup.authenticatorApp.setupKey}</dt>
        <dd className={styles.key} translate="no">
          {groups.map((group, index) => (
            // By place, as a key may repeat a group.
            <span key={index} className={styles.group}>{group}</span>
          ))}
        </dd>
      </dl>
      <p>{strings.setup.authenticatorApp.setupKeyNote}</p>
    </>
  );
}
