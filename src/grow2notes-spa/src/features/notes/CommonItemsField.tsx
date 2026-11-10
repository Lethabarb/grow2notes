import { TickListField } from '../../components/TickListField.tsx';
import tickList from '../../components/TickList.module.css';
import { strings } from '../../copy/strings.ts';
import { isOffered } from './commonItemGroups.ts';
import { ItemDetailField } from './ItemDetailField.tsx';
import type { DraftCommonItem, DraftGroup } from './noteQueries.ts';

type CommonItemsFieldProps = {
  /** The snapshot's groups, Every note first, in the managers' order. */
  groups: readonly DraftGroup[];
  /** The IDs of the groups picked, which the form owns. */
  picked: ReadonlySet<string>;
  /** The IDs of the items ticked, which the form owns. */
  ticked: ReadonlySet<string>;
  /** The details typed for the ticked items that ask for them, by the item's ID, which the form owns. */
  details: Readonly<Record<string, string>>;
  /** Submit's message for each Details box that failed it, by the item's ID, until the input that fixes it. */
  detailErrors?: Readonly<Record<string, string>>;
  /** In place of the section's lists when no group has an item, such as "No common items set up.". */
  emptyText: string;
  className?: string;
  /** A group's box ticked or unticked, as the person's own tap on it or its name did. */
  onPick: (group: DraftGroup, isPicked: boolean) => void;
  /** An item's box ticked or unticked. */
  onToggle: (itemId: string, isTicked: boolean) => void;
  /** Each input into an item's Details box, with the whole text. */
  onDetail: (itemId: string, text: string) => void;
};

/**
 * The common items of the note form (group-picker.md): a plain `<div>` with the hint, then Every note's list, then the
 * picker, a `<fieldset>` of one tick box per group that has items, under the question of what happened, then each such
 * group's list in a wrapper that is always in the page, in the managers' order whichever order the groups were picked
 * in, and `hidden` while the group is not picked, which takes its boxes out of the tab order and the accessibility
 * tree. Each list is headed by its group's name, as the Word template's sections are (A159). Ticking a group shows its
 * items at once, with focus left on its box. Under a ticked item that asks for details, its Details box. With no items
 * in any group, the sentence alone.
 */
export function CommonItemsField({
  groups,
  picked,
  ticked,
  details,
  detailErrors,
  emptyText,
  className,
  onPick,
  onToggle,
  onDetail,
}: CommonItemsFieldProps) {
  const everyNote = groups.find((group) => group.isEveryNote && group.items.length > 0);
  const offered = groups.filter(isOffered);
  if (everyNote === undefined && offered.length === 0) {
    return (
      <div className={className}>
        <p className={tickList.empty}>{emptyText}</p>
      </div>
    );
  }

  const detailsBox = (item: DraftCommonItem) =>
    item.needsDetail && ticked.has(item.id) ? (
      <ItemDetailField
        itemId={item.id}
        itemText={item.text}
        value={details[item.id] ?? ''}
        error={detailErrors?.[item.id]}
        onChange={(text) => {
          onDetail(item.id, text);
        }}
      />
    ) : null;

  return (
    <div className={className}>
      <p id="common-hint" className={tickList.hint}>
        {strings.note.commonItemsHint}
      </p>
      {everyNote !== undefined && (
        <TickListField
          idPrefix="common"
          heading={everyNote.name}
          hintId="common-hint"
          items={everyNote.items}
          ticked={ticked}
          onToggle={onToggle}
          after={detailsBox}
        />
      )}
      {offered.length > 0 && (
        <fieldset className={`${tickList.group} ${tickList.block}`} aria-describedby="picker-hint">
          <legend className={tickList.legend}>
            <h2 className={tickList.heading}>{strings.note.whichHappened}</h2>
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
            heading={group.name}
            hintId="common-hint"
            items={group.items}
            ticked={ticked}
            onToggle={onToggle}
            after={detailsBox}
          />
        </div>
      ))}
    </div>
  );
}
