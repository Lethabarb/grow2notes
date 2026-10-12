import { useEffect } from 'react';
import { Link, useLocation } from 'react-router';
import { PageHeading } from '../components/PageHeading.tsx';
import { PageStatus } from '../components/PageStatus.tsx';
import { msg } from '../copy/messages.ts';
import { pageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';

/**
 * Page not found (app-shell.md component 4), for an address no route matches and for a worker on a manager route,
 * inside the shell, so that the header and nav stay. Its link is a router link: only the crash page's is a full load.
 */
export function PageNotFound() {
  const { key } = useLocation();

  // Its own title while it shows, after the one the shell sets from the route, which names Manage where a worker
  // finds this page in its place. Again on each location change, as the shell's is.
  useEffect(() => {
    document.title = msg.pageTitle(pageName.pageNotFound);
  }, [key]);

  return (
    <PageStatus>
      <PageHeading>{pageName.pageNotFound}</PageHeading>
      <p>{strings.pageNotFound.checkAddress}</p>
      <p>
        <Link to="/">{strings.pageNotFound.goToToday}</Link>
      </p>
    </PageStatus>
  );
}
