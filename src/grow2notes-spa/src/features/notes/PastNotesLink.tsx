import { Link } from 'react-router';
import { pageName } from '../../copy/pageNames.ts';
import styles from './PastNotesLink.module.css';

/**
 * The Past notes link of a note's page (note-form.md component 5, participant-notes.md read view component 5): a plain
 * text link on a line of its own, to the participant's Past notes, a new visit from the newest 30, in the same place
 * under the note's heading on the form and on the read view, so that it is found in the same spot on both.
 */
export function PastNotesLink({ participantId }: { participantId: string }) {
  return (
    <p className={styles.line}>
      <Link to={`/participants/${participantId}/notes`} className={styles.link}>
        {pageName.pastNotes}
      </Link>
    </p>
  );
}
