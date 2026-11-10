import { ChoiceRow } from '../../components/ChoiceRow.tsx';
import { FieldGroup } from '../../components/FieldGroup.tsx';
import type { RowSetting } from '../../components/ListEditor.tsx';
import { strings } from '../../copy/strings.ts';

/**
 * A goal's term (A150), the one setting of the Goals list: in a goal's edit form, a "Term" fieldset of two radios,
 * Short term and Medium or long term, the goal's stored term chosen; and, under a Medium or long term goal's text in
 * the list, the muted line "Medium or long term". A goal added is Short term, and shows no line.
 */
export const goalTerm: RowSetting = {
  isOn: (row) => row.isMediumOrLongTerm === true,
  line: strings.listEditor.goal.mediumOrLongTerm,
  control: ({ idPrefix, on, onChange }) => (
    <FieldGroup fieldId={idPrefix} label={strings.listEditor.goal.term} group>
      <ChoiceRow
        id={`${idPrefix}-short`}
        name={idPrefix}
        label={strings.listEditor.goal.shortTerm}
        checked={!on}
        onChange={() => {
          onChange(false);
        }}
      />
      <ChoiceRow
        id={`${idPrefix}-long`}
        name={idPrefix}
        label={strings.listEditor.goal.mediumOrLongTerm}
        checked={on}
        onChange={() => {
          onChange(true);
        }}
      />
    </FieldGroup>
  ),
};
