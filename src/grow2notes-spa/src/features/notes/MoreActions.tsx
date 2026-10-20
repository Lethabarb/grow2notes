import {
  useEffectEvent,
  useId,
  useImperativeHandle,
  useLayoutEffect,
  useRef,
  useState,
  useSyncExternalStore,
  type Ref,
} from 'react';
import { getPhase, isWarning, subscribe } from '../../api/session.ts';
import { Button } from '../../components/Button.tsx';
import { closeOpenDialogs } from '../../components/openDialogs.ts';
import { strings } from '../../copy/strings.ts';
import { DiscardDialog } from './DiscardDialog.tsx';
import styles from './NotePage.module.css';

/** What the note form asks of More actions as autosave meets a dead end, which takes it away. */
export type MoreActionsHandle = {
  /** Whether focus is on More actions, in its panel or in the Discard dialog. */
  hasFocus: () => boolean;
};

type MoreActionsProps = {
  ref?: Ref<MoreActionsHandle>;
  participantId: string;
  noteDate: string;
  /** The participant's full name, as stored, which the Discard dialog names. */
  name: string;
  /** Pauses autosave once any save out has settled: whether the Discard may then be sent. */
  stopSaving: () => Promise<boolean>;
  /** Lets the form's own move to Today go past its leave guard. */
  onLeave: () => void;
  /**
   * The Discard dialog closed with nothing discarded, for autosave to go on: with why its request failed, if a browser
   * closed the dialog while it was out.
   */
  onNotDiscarded: (failure?: string) => void;
};

/**
 * More actions (note-form.md component 13, primary-actions.md), under Submit note on the viewer's own saved draft: an
 * APG disclosure, not a menu, whose button says whether its inline panel is open, and whose panel holds the secondary
 * Discard draft, which opens the Discard dialog. Escape, from the button or the panel, closes the panel with focus on
 * More actions; and so do Go back, Escape and Back in the dialog, which close it with nothing discarded, focus going to
 * More actions as the trigger has gone with the panel (confirm-dialog.md *Behaviour* 6), unless something else has
 * taken it meanwhile, such as the warning before the idle sign-out or sign-in in place, each of which gives it back as
 * it goes.
 *
 * The warning closes the panel as it opens, whether or not the dialog is open (session-timeout.md *The warning* step
 * 1; app-shell.md *28:00 idle on a visible page*): during the render, as the header closes the Account panel, then, in
 * a layout effect, which runs before the warning's own effect, closing the dialog as Go back, as a modal dialog makes
 * the page inert, and moving focus that was in the panel or the dialog to More actions, where the warning gives it back
 * as it closes, never to a hidden Discard draft.
 */
export function MoreActions({
  ref,
  participantId,
  noteDate,
  name,
  stopSaving,
  onLeave,
  onNotDiscarded,
}: MoreActionsProps) {
  const panelId = useId();
  const root = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  const panel = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [discarding, setDiscarding] = useState(false);

  const warning = useSyncExternalStore(subscribe, () => isWarning(getPhase()));
  const [warned, setWarned] = useState(warning);
  if (warned !== warning) {
    setWarned(warning);
    if (warning) {
      setOpen(false);
    }
  }

  // Whether focus is on More actions, in its panel or in the Discard dialog, or on nothing, as once the dialog has
  // closed with the panel gone; never on what the person moved it to after a browser closed the dialog.
  function focusIsOurs(): boolean {
    const focused = document.activeElement;
    return focused === null || focused === document.body || (root.current?.contains(focused) ?? false);
  }

  const onWarning = useEffectEvent(() => {
    if (discarding) {
      // Read before the close, which moves focus.
      const ours = focusIsOurs();
      closeOpenDialogs();
      if (ours) {
        button.current?.focus();
      }
    } else if (panel.current?.contains(document.activeElement)) {
      button.current?.focus();
    }
  });

  useLayoutEffect(() => {
    if (warning) {
      onWarning();
    }
  }, [warning]);

  useImperativeHandle(ref, () => ({ hasFocus: () => root.current?.contains(document.activeElement) ?? false }));

  function closePanel() {
    setOpen(false);
    button.current?.focus();
  }

  function notDiscarded(failure?: string) {
    setDiscarding(false);
    setOpen(false);
    if (focusIsOurs()) {
      button.current?.focus();
    }

    onNotDiscarded(failure);
  }

  return (
    <div
      ref={root}
      className={styles.moreActions}
      // Not from the dialog, whose own Escape is Go back.
      onKeyDown={(event) => {
        if (event.key === 'Escape' && open && !discarding) {
          closePanel();
        }
      }}
    >
      <Button ref={button} aria-expanded={open} aria-controls={panelId} onClick={() => setOpen(!open)}>
        {strings.note.moreActions}
      </Button>
      <div ref={panel} id={panelId} className={styles.moreActionsPanel} hidden={!open}>
        <Button onClick={() => setDiscarding(true)}>{strings.note.discardDraft}</Button>
      </div>
      {discarding && (
        <DiscardDialog
          participantId={participantId}
          noteDate={noteDate}
          name={name}
          prepare={stopSaving}
          onLeave={onLeave}
          onClose={notDiscarded}
        />
      )}
    </div>
  );
}
