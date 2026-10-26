/**
 * The page names (app-shell.md component 3), the only words a page title holds after "Grow2Notes – ", so that no name
 * or other data can reach a title. A screen that needs another adds it to app-shell.md's list first, then here.
 */
export const pageName = {
  signIn: 'Sign in',
  setUpYourAccount: 'Set up your account',
  today: 'Today',
  note: 'Note',
  pastNotes: 'Past notes',
  versionHistory: 'Version history',
  flaggedNotes: 'Flagged notes',
  dailyReport: 'Daily report',
  manage: 'Manage',
  participants: 'Participants',
  addParticipant: 'Add participant',
  participant: 'Participant',
  writePastDayNote: 'Write past-day note',
  commonItems: 'Common items',
  guidePrompts: 'Guide prompts',
  users: 'Users',
  inviteUser: 'Invite user',
  user: 'User',
  editUser: 'Edit user',
  exportRecord: 'Export record',
  pageNotFound: 'Page not found',
  thereIsAProblem: 'There is a problem',
  couldNotConnect: 'Could not connect',
  connectAnAiAssistant: 'Connect an AI assistant',
} as const;

export type PageName = (typeof pageName)[keyof typeof pageName];
