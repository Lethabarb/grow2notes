import type { IndexRouteObject, NonIndexRouteObject } from 'react-router';
import { pageName, type PageName } from '../copy/pageNames.ts';
import { UsersPage } from '../features/users/UsersPage.tsx';
import { AppShell } from './AppShell.tsx';
import { ShellCrashPage } from './CrashPage.tsx';
import { ManagePage } from './ManagePage.tsx';
import { ManagerOnly } from './ManagerOnly.tsx';
import { PageNotFound } from './PageNotFound.tsx';
import { ScreenErrorPage } from './ScreenErrorPage.tsx';
import { AuthenticatorSetup } from './setup/AuthenticatorSetup.tsx';
import { CreatePassword } from './setup/CreatePassword.tsx';
import { SetupLayout } from './setup/SetupLayout.tsx';
import { SetupPage } from './setup/SetupPage.tsx';
import { TodayPage } from './TodayPage.tsx';

/**
 * What a route declares for the shell: its page name, from which the shell sets the title (app-shell.md, 3) and, for
 * Page not found, marks no nav item (component 2b); and, for a page that a person with no session must reach, that it
 * shows in the signed-out frame whatever `/me` answers, rather than waiting for its `200` (sign-in.md component 1).
 */
export type RouteHandle = { pageName: PageName; signedOutFrame?: true };

/** A route of the table, whose `handle`, which React Router types as `any`, must name a page from the list. */
export type AppRoute =
  | (Omit<IndexRouteObject, 'handle'> & { handle?: RouteHandle })
  | (Omit<NonIndexRouteObject, 'handle' | 'children'> & { handle?: RouteHandle; children?: AppRoute[] });

const setupHandle: RouteHandle = { pageName: pageName.setUpYourAccount, signedOutFrame: true };

/**
 * The app's routes, which `main.tsx` gives to `createBrowserRouter` and the tests to `createMemoryRouter`: one root
 * layout, the shell, around every screen, with a crash page for each of its two route error boundaries, and Page not
 * found for a screen whose region was refused (app-shell.md component 4). React Router clears either on the next
 * location.
 */
export const routes: AppRoute[] = [
  {
    element: <AppShell />,
    // For a crash in the shell itself, which takes the header and nav with it.
    errorElement: <ShellCrashPage />,
    children: [
      {
        // For a screen that throws while rendering, in <main>, under the nav: a crash, or a region's refusal.
        errorElement: <ScreenErrorPage />,
        children: [
          // Beside the signed-in routes, not under them, so that a person with no session reaches setup.
          {
            path: 'setup',
            element: <SetupLayout />,
            children: [
              { index: true, element: <SetupPage />, handle: setupHandle },
              { path: 'password', element: <CreatePassword />, handle: setupHandle },
              { path: 'app', element: <AuthenticatorSetup />, handle: setupHandle },
            ],
          },
          { index: true, element: <TodayPage />, handle: { pageName: pageName.today } },
          // Every manager screen goes in here.
          {
            element: <ManagerOnly />,
            children: [
              { path: 'manage', element: <ManagePage />, handle: { pageName: pageName.manage } },
              { path: 'manage/users', element: <UsersPage />, handle: { pageName: pageName.users } },
            ],
          },
          // Inside the shell, so that the header and nav stay on an address no route matches (component 4).
          { path: '*', element: <PageNotFound />, handle: { pageName: pageName.pageNotFound } },
        ],
      },
    ],
  },
];
