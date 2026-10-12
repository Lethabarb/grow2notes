import { Link } from 'react-router';
import { PageHeading } from '../components/PageHeading.tsx';
import { PageStatus } from '../components/PageStatus.tsx';
import { pageName } from '../copy/pageNames.ts';
import styles from './ManagePage.module.css';

// In the design's order, each labelled with the name of the page it opens.
const links = [
  { to: '/manage/participants', name: pageName.participants },
  { to: '/manage/common-items', name: pageName.commonItems },
  { to: '/manage/guide-prompts', name: pageName.guidePrompts },
  { to: '/manage/users', name: pageName.users },
] as const;

/**
 * Manage at `/manage` (app-shell.md component 5): its heading and a link to each management screen, with no
 * descriptions, counts or icons. It is how managers reach the four screens on a phone and a laptop alike (D73).
 */
export function ManagePage() {
  return (
    <PageStatus>
      <PageHeading>{pageName.manage}</PageHeading>
      <ul className={styles.list}>
        {links.map(({ to, name }) => (
          <li key={to}>
            <Link to={to} className={styles.row}>
              {name}
            </Link>
          </li>
        ))}
      </ul>
    </PageStatus>
  );
}
