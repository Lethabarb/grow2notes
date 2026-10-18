import { createContext, useContext } from 'react';

/**
 * What every list in a `ListFrame` shares with the frame (list-editor.md *Implementation notes*: "the focus map is
 * shared by the page"): the id of the help text, which describes each list's text fields; the focus that follows a
 * change, wherever on the page it goes; and the list conflict, which a write in any of the lists shows and the next
 * write that succeeds in any of them takes away.
 */
export type ListFrameValue = {
  helpId: string;
  /** Whether the list conflict shows, as a write found a list changed by someone else. */
  changed: boolean;
  /** Registers an element under `key`, such as a row's Edit under `<id>:edit`, for `focusAfterRender`. */
  focusTarget: (key: string) => (element: HTMLElement | null) => void;
  /** Focuses the element registered under `key` once the render that a change brings is in the DOM. */
  focusAfterRender: (key: string) => void;
  /**
   * Opens the archived section registered under `sectionKey` once the render that an Archive brings is in the DOM, as
   * it may have only just appeared, then focuses the element registered under `key`, if one is given.
   */
  openArchivedAfterRender: (sectionKey: string, key: string | undefined) => void;
  /** Shows the list conflict, and focuses it, as a write found a list changed by someone else. */
  changedMeanwhile: () => void;
  /** Takes the list conflict away, as a write on a list has succeeded. */
  written: () => void;
};

export const ListFrameContext = createContext<ListFrameValue | undefined>(undefined);

/** The nearest `ListFrame`'s value, which every `ListEditor` and `ListConflict` needs around it. */
export function useListFrame(): ListFrameValue {
  const frame = useContext(ListFrameContext);
  if (frame === undefined) {
    throw new Error('A list is rendered outside a ListFrame.');
  }

  return frame;
}
