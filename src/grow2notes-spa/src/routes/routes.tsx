import type { IndexRouteObject, NonIndexRouteObject } from 'react-router';
import { pageName, type PageName } from '../copy/pageNames.ts';
import { CommonItemsPage } from '../features/commonItems/CommonItemsPage.tsx';
import { FlaggedPage } from '../features/flagged/FlaggedPage.tsx';
import { GuidePromptsPage } from '../features/guidePrompts/GuidePromptsPage.tsx';
import { NotePage } from '../features/notes/NotePage.tsx';
import { PastNotesPage } from '../features/notes/PastNotesPage.tsx';
import { VersionHistoryPage } from '../features/notes/VersionHistoryPage.tsx';
import { VersionPage } from '../features/notes/VersionPage.tsx';
import { ParticipantPage } from '../features/participants/ParticipantPage.tsx';
import { ParticipantsPage } from '../features/participants/ParticipantsPage.tsx';
import { WritePastDayNotePage } from '../features/participants/WritePastDayNotePage.tsx';
import { DailyReportPage } from '../features/reports/DailyReportPage.tsx';
import { ExportRecordPage } from '../features/reports/ExportRecordPage.tsx';
import { TodayPage } from '../features/today/TodayPage.tsx';
import { EditUserPage } from '../features/users/EditUserPage.tsx';
import { InviteUserPage } from '../features/users/InviteUserPage.tsx';
import { UserPage } from '../features/users/UserPage.tsx';
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
import { SupportGate } from './SupportGate.tsx';

/**
 * The name that a screen gives itself as the opener of the screens that it opens, which the shell records on each of
 * them (app-shell.md *Where a screen was opened from*): the complete list.
 */
export type BackKey =
  | 'today'
  | 'pastNotes'
  | 'flagged'
  | 'participant'
  | 'note'
  | 'versionHistory'
  | 'participants'
  | 'users'
  | 'user';

/**
 * What a route declares for the shell: its page name, from which the shell sets the title (app-shell.md, 3) and, for
 * Page not found, marks no nav item (component 2b); for a page that a person with no session must reach, that it
 * shows in the signed-out frame whatever `/me` answers, rather than waiting for its `200` (sign-in.md component 1);
 * for routes that are one screen, such as the Daily report's dates, the screen key that they share, so that the shell
 * moves neither focus nor scroll between them and keeps one scroll position for them all; for a list, that a pop back
 * to it focuses the row opened from it, or else its fallback, the `<h1>` unless it says `'sameIndex'`; and, for a
 * screen that opens others, its back key (*The route-change rule (one owner)*).
 */
export type RouteHandle = {
  pageName: PageName;
  signedOutFrame?: true;
  screenKey?: string;
  returnFocus?: { fallback?: 'h1' | 'sameIndex' };
  backKey?: BackKey;
};

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
          // Every signed-in screen goes in here, so that a Support account sees the Support page in place of them all.
          // Setup stays outside it, for a person with no session, and so does the Connect screen.
          {
            element: <SupportGate />,
            children: [
              {
                index: true,
                element: <TodayPage />,
                handle: { pageName: pageName.today, returnFocus: {}, backKey: 'today' },
              },
              {
                path: 'participants/:participantId/notes',
                element: <PastNotesPage />,
                handle: { pageName: pageName.pastNotes, returnFocus: {}, backKey: 'pastNotes' },
              },
              {
                path: 'participants/:participantId/notes/:noteDate',
                element: <NotePage />,
                handle: { pageName: pageName.note, backKey: 'note' },
              },
              {
                path: 'participants/:participantId/notes/:noteDate/edit',
                element: <NotePage editing />,
                handle: { pageName: pageName.note, backKey: 'note' },
              },
              // Every manager screen goes in here.
              {
                element: <ManagerOnly />,
                children: [
                  // Its rows' return points: on a pop back from a note that a review has taken off the list, the row
                  // now in its place (flagged.md *Interactions and focus*).
                  {
                    path: 'flagged',
                    element: <FlaggedPage />,
                    handle: {
                      pageName: pageName.flaggedNotes,
                      returnFocus: { fallback: 'sameIndex' },
                      backKey: 'flagged',
                    },
                  },
                  // The date is optional so that /reports/daily reaches the page's arrival rules, behind the guard.
                  {
                    path: 'reports/daily/:date?',
                    element: <DailyReportPage />,
                    handle: { pageName: pageName.dailyReport, screenKey: 'dailyReport' },
                  },
                  // Both pages of a note's version history (version-history.md *Routes*), whose rows are page A's
                  // return points. React Router ranks these static segments above any param, so none takes them for a
                  // date.
                  {
                    path: 'participants/:participantId/notes/:noteDate/versions',
                    element: <VersionHistoryPage />,
                    handle: { pageName: pageName.versionHistory, returnFocus: {}, backKey: 'versionHistory' },
                  },
                  {
                    path: 'participants/:participantId/notes/:noteDate/versions/:versionNumber',
                    element: <VersionPage />,
                    handle: { pageName: pageName.versionHistory },
                  },
                  { path: 'manage', element: <ManagePage />, handle: { pageName: pageName.manage } },
                  {
                    path: 'manage/participants',
                    element: <ParticipantsPage />,
                    handle: { pageName: pageName.participants, returnFocus: {}, backKey: 'participants' },
                  },
                  // One element for both, so that the replace after Add participant's 201 keeps it mounted
                  // (participants.md component B), and each with its own title.
                  {
                    path: 'manage/participants/new',
                    element: <ParticipantPage />,
                    handle: { pageName: pageName.addParticipant },
                  },
                  {
                    path: 'manage/participants/:participantId',
                    element: <ParticipantPage />,
                    handle: { pageName: pageName.participant, backKey: 'participant' },
                  },
                  {
                    path: 'manage/participants/:participantId/past-day-note',
                    element: <WritePastDayNotePage />,
                    handle: { pageName: pageName.writePastDayNote },
                  },
                  {
                    path: 'manage/participants/:participantId/export',
                    element: <ExportRecordPage />,
                    handle: { pageName: pageName.exportRecord },
                  },
                  {
                    path: 'manage/common-items',
                    element: <CommonItemsPage />,
                    handle: { pageName: pageName.commonItems },
                  },
                  {
                    path: 'manage/guide-prompts',
                    element: <GuidePromptsPage />,
                    handle: { pageName: pageName.guidePrompts },
                  },
                  {
                    path: 'manage/users',
                    element: <UsersPage />,
                    handle: { pageName: pageName.users, returnFocus: {}, backKey: 'users' },
                  },
                  // React Router ranks this static segment above :userId, so "new" is never taken for a user's ID.
                  { path: 'manage/users/new', element: <InviteUserPage />, handle: { pageName: pageName.inviteUser } },
                  { path: 'manage/users/:userId', element: <UserPage />, handle: { pageName: pageName.user } },
                  {
                    path: 'manage/users/:userId/edit',
                    element: <EditUserPage />,
                    handle: { pageName: pageName.editUser },
                  },
                ],
              },
              // Inside the shell, so that the header and nav stay on an address no route matches (component 4).
              { path: '*', element: <PageNotFound />, handle: { pageName: pageName.pageNotFound } },
            ],
          },
        ],
      },
    ],
  },
];
