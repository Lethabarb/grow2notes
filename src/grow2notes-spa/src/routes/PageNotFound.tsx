import { Link } from 'react-router';
import { PageHeading } from '../components/PageHeading.tsx';
import { PageStatus } from '../components/PageStatus.tsx';
import { pageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';
import { useWholePageMessage } from './wholePageMessage.ts';

/**
 * Page not found (app-shell.md component 4), for an address no route matches, for a worker on a manager route and for
 * a screen whose region a `404` or a manager's `403` refused, inside the shell, so that the header and nav stay. It
 * tells the shell that it shows, so that the header marks no item and the page has its title, and the route's title
 * comes back as it goes while the route stays: another ID on the same route, or a worker made a manager. Its link is
 * a router link: only the crash page's is a full load.
 */
export function PageNotFound() {
  useWholePageMessage(pageName.pageNotFound);

  return (
    <PageStatus>
      <PageHeading>{pageName.pageNotFound}</PageHeading>
      <p>{strings.pageNotFound.checkAddress}</p>
      <p>
        <Link to="/">{strings.goToToday}</Link>
      </p>
    </PageStatus>
  );
}
