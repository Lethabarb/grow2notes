import styles from './TickList.module.css';

/** A row of a tick list: the item's ID, which names its box, and its wording as the snapshot holds it. */
export type TickItem = { id: string; text: string };

type TickListFieldProps = {
  /** What each box's id starts with, before the item's ID, such as `goal`, so that `goal-hint` is the hint's. */
  idPrefix: string;
  /** The list's heading, in its legend, such as "1. Goals" or, among the common items, a group's name. */
  heading: string;
  /** The heading's level: an `<h2>` for a section of the form, which is the default, or an `<h3>` inside one. */
  headingLevel?: 2 | 3;
  /** What a tick means, under the legend, such as "Ticked means reached."; none where `hintId` is given. */
  hint?: string;
  /** The ID of a hint that the form shows once for several lists, which describes the list in place of its own. */
  hintId?: string;
  /** In place of the list when it has no items, such as "No goals set up for Jane yet. A manager can add them.". */
  emptyText?: string;
  /** In the snapshot's order, which the list keeps. */
  items: readonly TickItem[];
  /** The IDs of the items ticked, which the form owns. */
  ticked: ReadonlySet<string>;
  /** A box ticked or unticked, as the person's own tap on it or its text did. */
  onToggle: (itemId: string, isTicked: boolean) => void;
  className?: string;
};

/**
 * An editable tick list of the note form (checkbox-list.md `TickListField`), which neither fetches nor saves: a
 * `<fieldset>`, its `<legend>` the list's `<h2>`, described by the hint under it, then a full-width row per item, the
 * whole row the item's `<label>`, around the native box, drawn 40 px with borders, and the item's wording, never
 * truncated; or, with no items, the heading and the sentence alone, as an empty fieldset confuses screen readers. A tap
 * anywhere on the row toggles the box, as a label does, with no handler of its own. The common items' lists are
 * `<h3>`s described by one hint shared by all, and T09's flag row and T13's group rows take its `.box` and row styles.
 */
export function TickListField({
  idPrefix,
  heading,
  headingLevel = 2,
  hint,
  hintId,
  emptyText,
  items,
  ticked,
  onToggle,
  className,
}: TickListFieldProps) {
  const groupClass = className === undefined ? styles.group : `${styles.group} ${className}`;
  const Heading = headingLevel === 2 ? 'h2' : 'h3';
  if (items.length === 0) {
    return (
      <div className={groupClass}>
        <Heading className={styles.heading}>{heading}</Heading>
        {emptyText !== undefined && <p className={styles.empty}>{emptyText}</p>}
      </div>
    );
  }

  const ownHintId = `${idPrefix}-hint`;
  return (
    <fieldset className={groupClass} aria-describedby={hintId ?? ownHintId}>
      <legend className={styles.legend}>
        <Heading className={styles.heading}>{heading}</Heading>
      </legend>
      {hintId === undefined && (
        <p id={ownHintId} className={styles.hint}>
          {hint}
        </p>
      )}
      {items.map((item) => {
        const id = `${idPrefix}-${item.id}`;
        return (
          // Keyed by the item, so that a box keeps its DOM node, and focus, when the list is loaded again.
          <label key={item.id} htmlFor={id} className={styles.row}>
            <input
              id={id}
              type="checkbox"
              className={styles.box}
              name={idPrefix}
              value={item.id}
              checked={ticked.has(item.id)}
              onChange={(event) => {
                onToggle(item.id, event.currentTarget.checked);
              }}
            />
            <span className={styles.text}>{item.text}</span>
          </label>
        );
      })}
    </fieldset>
  );
}
