import { useState } from 'react';
import { CharacterCount } from '../../components/CharacterCount.tsx';
import fieldGroup from '../../components/FieldGroup.module.css';
import textBox from '../../components/TextBox.module.css';
import tickList from '../../components/TickList.module.css';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import styles from './FlagForManager.module.css';

type FlagForManagerProps = {
  /** Whether the box is ticked, which the form owns. */
  flagged: boolean;
  /** The reason as typed, which the form keeps while the box is unticked, so that a re-tick brings it back. */
  reason: string;
  /** The box ticked or unticked, a change for autosave. */
  onFlaggedChange: (flagged: boolean) => void;
  /** Each input into Reason, with the whole text, a change for autosave. */
  onReasonChange: (reason: string) => void;
};

/**
 * Flag for manager and its Reason (note-form.md component 11, conditional-reveal.md), the form's block after Guided
 * notes, with no heading: the tick row, in the tick lists' 56 px row and 40 px box, which says that it controls the
 * Reason block and whether that is shown; then the block, always in the page, so that the polite twin of its count is
 * there before anything is written into it, and `hidden` while the box is unticked, which takes it out of the tab
 * order and the accessibility tree. A tick shows it at once, with focus left on the box and nothing scrolled (SC
 * 3.2.2). The block holds a 4 px bar under the box's centre, the bold label, the hidden hint that Reason is read with,
 * the field and its count. Nothing stops the typing: there is no `maxlength`, and the count says how many characters
 * are left, or how many too many, counted as the server counts them.
 */
export function FlagForManager({ flagged, reason, onFlaggedChange, onReasonChange }: FlagForManagerProps) {
  // Reason's inputs, after which the count's twin speaks once typing has stopped; none as a flagged draft loads, so
  // that its count is not announced.
  const [inputs, setInputs] = useState(0);

  return (
    <div className={styles.flag}>
      <label htmlFor="flag-for-manager" className={tickList.row}>
        <input
          id="flag-for-manager"
          type="checkbox"
          className={tickList.box}
          checked={flagged}
          aria-controls="flag-reason-group"
          aria-expanded={flagged}
          onChange={(event) => {
            onFlaggedChange(event.currentTarget.checked);
          }}
        />
        <span className={tickList.text}>{strings.note.flagForManager}</span>
      </label>
      <div id="flag-reason-group" className={styles.reveal} hidden={!flagged}>
        <label htmlFor="flag-reason" className={fieldGroup.label}>
          {strings.note.reason}
        </label>
        <p id="flag-reason-hint" className="visually-hidden">
          {msg.characterLimitHint(limits.flagReason)}
        </p>
        <textarea
          id="flag-reason"
          name="flagReason"
          rows={4}
          className={`${textBox.box} ${styles.field}`}
          value={reason}
          aria-required="true"
          aria-describedby="flag-reason-hint"
          onChange={(event) => {
            setInputs((count) => count + 1);
            onReasonChange(event.currentTarget.value);
          }}
        />
        <CharacterCount length={reason.length} limit={limits.flagReason} inputs={inputs} />
      </div>
    </div>
  );
}
