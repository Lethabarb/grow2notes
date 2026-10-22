import { Link, useParams } from 'react-router';
import { PageHeading } from '../components/PageHeading.tsx';
import { PageStatus } from '../components/PageStatus.tsx';
import { strings } from '../copy/strings.ts';
import { useFocusOnReplace } from './routeChange.ts';

/**
 * "You cannot edit this note" (empty-loading-error.md *Whole-page messages*), for an edit refused with `403
 * note.not_editable`, reached by an old link or a stale screen, inside the shell, so that the header and nav stay. It
 * is not Page not found, as the note exists and the person may read it, so it tells the shell nothing and the page keeps
 * the route's title, "Note". Its `<h1>` takes focus as Page not found's does, and its link goes to the note's read view.
 */
export function CannotEditNote() {
  const { participantId = '', noteDate = '' } = useParams();
  useFocusOnReplace();

  return (
    <PageStatus>
      <PageHeading>{strings.cannotEditNote.heading}</PageHeading>
      <p>{strings.cannotEditNote.body}</p>
      <p>
        <Link to={`/participants/${participantId}/notes/${noteDate}`}>{strings.cannotEditNote.readTheNote}</Link>
      </p>
    </PageStatus>
  );
}
