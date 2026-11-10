import choiceRow from '../../components/ChoiceRow.module.css';
import type { RowSetting } from '../../components/ListEditor.tsx';
import tickList from '../../components/TickList.module.css';
import { strings } from '../../copy/strings.ts';

/**
 * A common item's Ask for details when ticked (A151), the one setting of an item: in an item's edit form, a tick box
 * with its hint, which says what it does to notes started from now on; and, under the text of an item with it on in the
 * list, the muted line "Asks for details".
 */
export const needsDetail: RowSetting = {
  isOn: (row) => row.needsDetail === true,
  line: strings.listEditor.item.asksForDetails,
  control: ({ idPrefix, on, onChange }) => (
    <div>
      <label htmlFor={idPrefix} className={tickList.row}>
        <input
          id={idPrefix}
          type="checkbox"
          className={tickList.box}
          checked={on}
          aria-describedby={`${idPrefix}-hint`}
          onChange={(event) => {
            onChange(event.currentTarget.checked);
          }}
        />
        <span className={tickList.text}>{strings.listEditor.item.needsDetail}</span>
      </label>
      <p id={`${idPrefix}-hint`} className={choiceRow.hint}>
        {strings.listEditor.item.needsDetailHint}
      </p>
    </div>
  ),
};
