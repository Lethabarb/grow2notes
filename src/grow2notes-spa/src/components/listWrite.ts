import { useMutation } from '@tanstack/react-query';
import { useEffect, useState, type RefObject } from 'react';
import { ApiError, classify } from '../api/api.ts';
import type { Cause } from '../copy/strings.ts';

/**
 * What a write to a page's lists needs of them (list-editor.md *Implementation notes*), which a `ListEditor`'s rows
 * and Common items' group parts share: the TanStack mutation scope of every write on the page, such as
 * `goals:<participantId>`, or `common-items` for every group's list and every group (common-items.md component 5), so
 * that the writes run one at a time; the read of the lists again, which a write waits for, so that the next write
 * starts from what the server holds; and the page's list conflict, which a write that found the lists changed by
 * someone else shows, and focuses, and the next write that succeeds takes away.
 */
export type ListWriter = {
  scopeId: string;
  reread: () => Promise<void>;
  changedMeanwhile: () => void;
  written: () => void;
};

/**
 * A write that found the list changed by someone else, once the list was read again, as its row is no longer there or
 * the server refused it as such; the list conflict then shows (list-editor.md *Request errors*).
 */
export class ListChanged extends Error {
  override name = 'ListChanged';
}

/**
 * Sends a write that the server refuses with `status` when the list has changed since it was read (list-editor.md
 * *Request errors*): a reorder's `422 config.order_mismatch` (design.md §6.7), an archive's or a restore's `412`, or
 * an add's or a Save's `409 config.group_archived`, as the list's group was archived meanwhile (common-items.md
 * *Failures*). That refusal reads the list again and fails as `ListChanged`, so that the list conflict shows over what
 * the server now holds; any other failure fails as it is.
 */
export async function sendUnlessListChanged(
  list: ListWriter,
  status: number,
  send: () => Promise<unknown>,
): Promise<void> {
  try {
    await send();
  } catch (failed) {
    if (!(failed instanceof ApiError && failed.status === status)) {
      throw failed;
    }

    await list.reread();
    throw new ListChanged();
  }
}

/**
 * A write to the list (list-editor.md *Implementation notes*): in the page's one scope, so that it waits for the write
 * before it; sent once, never again by itself, as an add sent again after a lost answer would add the row or the group
 * twice; then the list read again before the next write starts, as the screen shows a change only once the server has
 * it, and what `send` gave passed on. Once it succeeds, the list conflict goes; once it finds the list changed, the
 * conflict shows, though the form or the row that sent it may have gone.
 */
export function useListWrite<T, R>(list: ListWriter, send: (variables: T) => Promise<R>) {
  return useMutation({
    mutationFn: async (variables: T) => {
      const result = await send(variables);
      await list.reread();
      return result;
    },
    onSuccess: () => {
      list.written();
    },
    onError: (failed) => {
      if (failed instanceof ListChanged) {
        list.changedMeanwhile();
      }
    },
    scope: { id: list.scopeId },
    retry: 0,
  });
}

/**
 * What a write that failed says in the alert of the row, the group or the form that sent it (list-editor.md *Request
 * errors*): `message` with the cause; or nothing for the list conflict, which shows above the lists, or for a `401`,
 * which the session flow has, as `api()` started it.
 */
export function failureAlert(failed: unknown, message: (cause: Cause) => string): string | undefined {
  if (failed instanceof ListChanged) {
    return undefined;
  }

  const cause = classify(failed);
  return cause === 'signedOut' ? undefined : message(cause);
}

/**
 * A row's text or a group's name as its form sends it (list-editor.md *Implementation notes*, common-items.md
 * *Validation*): each line break, with the spaces around it, as one space, and the ends trimmed, as the server cleans
 * it too.
 */
export function cleanText(text: string): string {
  return text.replace(/\s*[\r\n]+\s*/g, ' ').trim();
}

/**
 * Whether focus is still in `origin`, the form or the button that started a write, or nowhere: only then does the
 * write's end move it, as focus is never pulled away from where the person has gone while the write was out
 * (common-items.md *Queueing*).
 */
export function focusStayedIn(origin: HTMLElement | null): boolean {
  const focused = document.activeElement;
  return focused === null || focused === document.body || origin?.contains(focused) === true;
}

/**
 * The text of a one-field form, a row's text or a group's name, as the form holds it (form-validation.md's one-field
 * pattern): as typed, in this page's memory only (D22); its inputs, after which the count's twin speaks; and its
 * message, shown by a press that finds the text refused, or by the server's refusal of it, each of which moves focus
 * to the field, even with the same message, and, once shown, checked again at each input, and taken away, silently,
 * once the text passes.
 *
 * @param initial The text the field opens with.
 * @param rule The message for the text as cleaned, or `undefined` once it passes.
 * @param errorKey The field's key in a `422`'s errors, `text` for a row and `name` for a group (common-items.md
 *   *Validation*).
 * @param field The field, which a refusal focuses.
 */
export function useFieldText(
  initial: string,
  rule: (cleaned: string) => string | undefined,
  errorKey: string,
  field: RefObject<HTMLElement | null>,
) {
  const [text, setText] = useState(initial);
  const [inputs, setInputs] = useState(0);
  const [error, setError] = useState<string>();
  const [refusals, setRefusals] = useState(0);

  // After the render that names the message in the field's description, so that a screen reader reads it with focus.
  useEffect(() => {
    if (refusals > 0) {
      field.current?.focus();
    }
  }, [refusals, field]);

  function refuse(message: string) {
    setError(message);
    setRefusals((presses) => presses + 1);
  }

  return {
    text,
    setText,
    inputs,
    error,
    edit: (next: string) => {
      setText(next);
      setInputs((count) => count + 1);
      setError((shown) => (shown === undefined ? undefined : rule(cleanText(next))));
    },
    /** The text as its form sends it, or `undefined` once it is refused, with its message shown. */
    checked: (): string | undefined => {
      const cleaned = cleanText(text);
      const refused = rule(cleaned);
      if (refused === undefined) {
        return cleaned;
      }

      refuse(refused);
      return undefined;
    },
    /** Whether `failed` is the server's refusal of the text, a `422` under the field's key, shown on the field. */
    refusedByServer: (failed: unknown): boolean => {
      const refusal = failed instanceof ApiError && failed.status === 422 ? failed.errors?.[errorKey]?.[0] : undefined;
      if (refusal !== undefined) {
        refuse(refusal);
      }

      return refusal !== undefined;
    },
  };
}
