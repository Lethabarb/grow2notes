import { TickListField } from '../../components/TickListField.tsx';
import tickList from '../../components/TickList.module.css';
import { strings } from '../../copy/strings.ts';
import { isOffered } from './commonItemGroups.ts';
import type { DraftGroup } from './noteQueries.ts';

type CommonItemsFieldProps = {
  /** The snapshot's groups, Every note first, in the managers' order. */
  groups: readonly DraftGroup[];
  /** The IDs of the groups picked, which the form owns. */
  picked: ReadonlySet<string>;
  /** The IDs of the items ticked, which the form owns. */
  ticked: ReadonlySet<string>;
  /** In place of the section's lists when no group has an item, such as "No common items set up.". */
  emptyText: string;
  className?: string;
  /** A group's box ticked or unticked, as the person's own tap on it or its name did. */
  onPick: (group: DraftGroup, isPicked: boolean) => void;
  /** An item's box ticked or unticked. */
  onToggle: (itemId: string, isTicked: boolean) => void;
};

/**
 * Section 2 of the note form (group-picker.md): a plain `<div>` with the section's `<h2>` and its hint, then Every
 * note's list, then the picker, a `<fieldset>` of one tick box per group that has items, under the question of what
 * happened, then each such group's list in a wrapper that is always in the page, in the managers' order whichever order
 * the groups were picked in, and `hidden` while the group is not picked, which takes its boxes out of the tab order and
 * the accessibility tree. Ticking a group shows its items at once, with focus left on its box. With no items in any
 * group, the heading and the sentence alone.
 */
export function CommonItemsField({
  groups,
  picked,
  ticked,
  emptyText,
  className,
  onPick,
  onToggle,
}: CommonItemsFieldProps) {
  const everyNote = groups.find((group) => group.isEveryNote && group.items.length > 0);
  const offered = groups.filter(isOffered);
  if (everyNote === undefined && offered.length === 0) {
    return (
      <div className={className}>
        <h2 className={tickList.heading}>{strings.note.commonItems}</h2>
        <p className={tickList.empty}>{emptyText}</p>
      </div>
    );
  }

  return (
    <div className={className}>
      <h2 className={tickList.heading}>{strings.note.commonItems}</h2>
      <p id="common-hint" className={tickList.hint}>
        {strings.note.commonItemsHint}
      </p>
      {everyNote !== undefined && (
        <TickListField
          idPrefix="common"
          headingLevel={3}
          heading={everyNote.name}
          hintId="common-hint"
          items={everyNote.items}
          ticked={ticked}
          onToggle={onToggle}
        />
      )}
      {offered.length > 0 && (
        <fieldset className={`${tickList.group} ${tickList.block}`} aria-describedby="picker-hint">
          <legend className={tickList.legend}>
            <h3 className={tickList.heading}>{strings.note.whichHappened}</h3>
          </legend>
          <p id="picker-hint" className={tickList.hint}>
            {strings.note.whichHappenedHint}
          </p>
          {offered.map((group) => {
            const id = `pick-${group.id}`;
            return (
              // Keyed by the group, so that a box keeps its DOM node, and focus, when the lists are loaded again.
              <label key={group.id} htmlFor={id} className={tickList.row}>
                <input
                  id={id}
                  type="checkbox"
                  className={tickList.box}
                  name="pickedGroup"
                  value={group.id}
                  aria-controls={`group-${group.id}`}
                  aria-expanded={picked.has(group.id)}
                  checked={picked.has(group.id)}
                  onChange={(event) => {
                    onPick(group, event.currentTarget.checked);
                  }}
                />
                <span className={tickList.text}>{group.name}</span>
              </label>
            );
          })}
        </fieldset>
      )}
      {offered.map((group) => (
        <div key={group.id} id={`group-${group.id}`} className={tickList.block} hidden={!picked.has(group.id)}>
          <TickListField
            idPrefix="common"
            headingLevel={3}
            heading={group.name}
            hintId="common-hint"
            items={group.items}
            ticked={ticked}
            onToggle={onToggle}
          />
        </div>
      ))}
    </div>
  );
}
