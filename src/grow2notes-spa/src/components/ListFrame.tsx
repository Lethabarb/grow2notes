import { useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { strings } from '../copy/strings.ts';
import styles from './ListFrame.module.css';
import { ListFrameContext, useListFrame, type ListFrameValue } from './listFrameContext.ts';

// The list conflict's key among the elements that take focus after a change.
const conflictKey = 'conflict';

type ListFrameProps = {
  /** The help text's id, by which each list's text fields name it in their description. */
  helpId: string;
  /** The help text above the lists, or nothing for read-only lists, as nothing in them can be changed. */
  help?: string;
  /** The lists, each a `ListEditor`, with the `ListConflict` above them, where the page shows it. */
  children: ReactNode;
};

/**
 * What every list that managers edit on a page shares (list-editor.md *Implementation notes*): the help text, once,
 * above the lists, which describes each of their text fields; the focus map, so that focus follows a change into
 * whichever list it lands in; and the list conflict, which a write in any of the lists shows, in its one place above
 * them, and the next write that succeeds in any of them takes away (common-items.md components 2 and 4). A
 * participant's Goals section has its one list in a frame of its own; Common items has every group's in one.
 *
 * Its value is new at each render, so that every list renders again with a request for focus, reading its query's
 * data as the write that asked left it, though TanStack has yet to tell the list: the element to focus, such as the
 * Restore of a row just archived, is then in the DOM by the time the request's layout effect runs.
 */
export function ListFrame({ helpId, help, children }: ListFrameProps) {
  const [changed, setChanged] = useState(false);
  const focus = useFocusAfterRender();
  const value: ListFrameValue = {
    helpId,
    changed,
    focusTarget: focus.target,
    focusAfterRender: focus.request,
    openArchivedAfterRender: focus.openArchived,
    changedMeanwhile: () => {
      setChanged(true);
      focus.request(conflictKey);
    },
    written: () => {
      setChanged(false);
    },
  };

  return (
    <ListFrameContext value={value}>
      {help !== undefined && (
        <p id={helpId} className={styles.help}>
          {help}
        </p>
      )}
      {children}
    </ListFrameContext>
  );
}

/**
 * The list conflict (list-editor.md *Request errors*), rendered only once a write has found a list changed by someone
 * else, and focused, by script alone, until the next write that succeeds; where the page puts it, above its lists.
 */
export function ListConflict() {
  const { changed, focusTarget } = useListFrame();

  return changed ? (
    <p ref={focusTarget(conflictKey)} tabIndex={-1} className={styles.conflict}>
      {strings.listRows.listChanged}
    </p>
  ) : null;
}

/**
 * Focus that follows the item (list-editor.md *Implementation notes*): each element that may take it registers under
 * a key, and a request focuses the one under its key in a layout effect, once the render that the change brings is in
 * the DOM and before it is painted, as React may have only just added the element, or moved it. `openArchived` opens
 * an archived section first, registered under its own key, as an Archive does, as its row's Restore cannot take focus
 * while the section is closed, and the section may have only just appeared; given no key, it moves no focus.
 */
function useFocusAfterRender() {
  const elements = useRef(new Map<string, HTMLElement>());
  // A new object at each request, so that a second request for the same key focuses it again.
  const [request, setRequest] = useState<{ key: string | undefined; section: string | undefined }>();

  useLayoutEffect(() => {
    if (request === undefined) {
      return;
    }

    const section = request.section === undefined ? undefined : elements.current.get(request.section);
    if (section instanceof HTMLDetailsElement) {
      section.open = true;
    }

    if (request.key !== undefined) {
      elements.current.get(request.key)?.focus();
    }
  }, [request]);

  return {
    target: (key: string) => (element: HTMLElement | null) => {
      if (element === null) {
        elements.current.delete(key);
      } else {
        elements.current.set(key, element);
      }
    },
    request: (key: string) => {
      setRequest({ key, section: undefined });
    },
    openArchived: (section: string, key: string | undefined) => {
      setRequest({ key, section });
    },
  };
}
