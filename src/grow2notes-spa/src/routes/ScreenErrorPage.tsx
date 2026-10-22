import { useRouteError } from 'react-router';
import { isNotEditable, isNotFound } from '../api/api.ts';
import { CannotEditNote } from './CannotEditNote.tsx';
import { CrashPage } from './CrashPage.tsx';
import { PageNotFound } from './PageNotFound.tsx';

/**
 * What the screens' route error boundary shows in `<main>`, under the nav, in place of a screen that threw while
 * rendering (app-shell.md component 4): Page not found for a region that a `404` or a manager's `403` refused, which
 * `LoadRegion` throws; "You cannot edit this note" for a refused edit, thrown by it or by the note form; and the crash
 * page for anything else. React Router clears it on the next location.
 */
export function ScreenErrorPage() {
  const error = useRouteError();
  if (isNotFound(error)) {
    return <PageNotFound />;
  }

  return isNotEditable(error) ? <CannotEditNote /> : <CrashPage />;
}
