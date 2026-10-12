import type { IndexRouteObject, NonIndexRouteObject } from 'react-router';
import { pageName, type PageName } from '../copy/pageNames.ts';
import { AppShell } from './AppShell.tsx';
import { ManagePage } from './ManagePage.tsx';
import { ManagerOnly } from './ManagerOnly.tsx';
import { PageNotFound } from './PageNotFound.tsx';
import { TodayPage } from './TodayPage.tsx';

/**
 * What a route declares for the shell: its page name, from which the shell sets the title (app-shell.md, 3) and, for
 * Page not found, marks no nav item (component 2b).
 */
export type RouteHandle = { pageName: PageName };

/** A route of the table, whose `handle`, which React Router types as `any`, must name a page from the list. */
export type AppRoute =
  | (Omit<IndexRouteObject, 'handle'> & { handle?: RouteHandle })
  | (Omit<NonIndexRouteObject, 'handle' | 'children'> & { handle?: RouteHandle; children?: AppRoute[] });

/**
 * The app's routes, which `main.tsx` gives to `createBrowserRouter` and the tests to `createMemoryRouter`: one root
 * layout, the shell, around every screen.
 */
export const routes: AppRoute[] = [
  {
    element: <AppShell />,
    children: [
      { index: true, element: <TodayPage />, handle: { pageName: pageName.today } },
      // Every manager screen goes in here.
      {
        element: <ManagerOnly />,
        children: [{ path: 'manage', element: <ManagePage />, handle: { pageName: pageName.manage } }],
      },
      // Inside the shell, so that the header and nav stay on an address no route matches (component 4).
      { path: '*', element: <PageNotFound />, handle: { pageName: pageName.pageNotFound } },
    ],
  },
];
