import { useRef, useState, type ReactNode } from 'react';
import type { StartedBy } from '../../api/api.ts';
import { Button } from '../../components/Button.tsx';
import { Notice } from '../../components/Notice.tsx';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import { TickRowRead } from '../../components/TickListRead.tsx';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { DiscardDialog } from './DiscardDialog.tsx';
import type { DraftRead } from './noteQueries.ts';
import { PastNotesLink } from './PastNotesLink.tsx';
import readView from './ReadView.module.css';
import { ReadSections } from './ReadView.tsx';
import styles from './SomeoneElsesDraft.module.css';

type SomeoneElsesDraftProps = {
  /** The note's identity `<h1>`, the participant's name and the note date, as the form shows it. */
  heading: ReactNode;
  participantId: string;
  noteDate: string;
  /** The participant's full name, as stored, which the Discard dialog names. */
  name: string;
  /** Who started the draft, and when. */
  startedBy: StartedBy;
  /** The in-progress sentence, which says who started the draft and when, and that only they can finish it. */
  sentence: string;
  /** The author's working copy, which `GET {base}` gives a manager alone; none for a worker. */
  content: DraftRead | undefined;
};

/**
 * Another user's draft at the shared note address (design.md §4.2's "Someone else's draft", §4.4; participant-notes.md
 * view C), which only its author may open in the form. A worker gets the `<h1>`, the in-progress sentence in a notice,
 * and the Past notes link, and nothing of the draft. A manager, given the author's working copy, gets the same
 * sentence, then Discard draft and Past notes, then the draft's sections as the read view shows a note's, and its flag
 * as a read-only tick row, "Flag for manager", with its reason when it is ticked and has one; never the Flagged tag, as
 * a flag on a draft alerts nobody (§3.6). The draft is shown as it was when the page opened, as the page never reads it
 * again while it is open (note-read-view.md *Someone else's draft*).
 */
export function SomeoneElsesDraft({
  heading,
  participantId,
  noteDate,
  name,
  startedBy,
  sentence,
  content,
}: SomeoneElsesDraftProps) {
  return (
    <>
      {heading}
      <Notice>{sentence}</Notice>
      {content !== undefined && (
        <DiscardDraft participantId={participantId} noteDate={noteDate} name={name} startedBy={startedBy} />
      )}
      <PastNotesLink participantId={participantId} />
      {content !== undefined && (
        <>
          <ReadSections goals={content.goals} narrative={content.narrative} />
          <div className={readView.section}>
            <TickRowRead text={strings.note.flagForManager} isTicked={content.isFlagged} />
            {content.isFlagged && content.flagReason !== null && (
              <p className={`${readView.written} ${styles.reason}`}>{msg.flagReason(content.flagReason)}</p>
            )}
          </div>
        </>
      )}
    </>
  );
}

type DiscardDraftProps = Pick<SomeoneElsesDraftProps, 'participantId' | 'noteDate' | 'name' | 'startedBy'>;

/**
 * A manager's Discard draft (participant-notes.md component 9), a secondary button, as red is kept for the dialog's
 * confirming button (*Conflicts resolved* 23), which opens the one Discard dialog, whose body says who started the
 * draft and when. Go back, Escape and Back close it with nothing discarded, focus going back to Discard draft, unless
 * something else has taken it meanwhile, such as the warning before the idle sign-out. A failure of a request whose
 * dialog a browser closed while it was out shows in the alert above the button, as the note form's does above Submit
 * note (confirm-dialog.md *Behaviour* 7).
 */
function DiscardDraft({ participantId, noteDate, name, startedBy }: DiscardDraftProps) {
  const root = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  const [discarding, setDiscarding] = useState(false);
  const [failure, setFailure] = useState<string>();

  function notDiscarded(why?: string) {
    setDiscarding(false);
    setFailure(why);
    const focused = document.activeElement;
    if (focused === null || focused === document.body || (root.current?.contains(focused) ?? false)) {
      button.current?.focus();
    }
  }

  return (
    <div ref={root} className={styles.discard}>
      <RequestAlert>{failure}</RequestAlert>
      <Button
        ref={button}
        onClick={() => {
          setFailure(undefined);
          setDiscarding(true);
        }}
      >
        {strings.note.discardDraft}
      </Button>
      {discarding && (
        <DiscardDialog
          participantId={participantId}
          noteDate={noteDate}
          name={name}
          startedBy={startedBy}
          onClose={notDiscarded}
        />
      )}
    </div>
  );
}
