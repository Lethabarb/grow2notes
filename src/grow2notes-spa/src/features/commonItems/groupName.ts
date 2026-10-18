import type { RefObject } from 'react';
import { useFieldText } from '../../components/listWrite.ts';
import { msg } from '../../copy/messages.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';

/**
 * The rule for a group's name (design.md §4.9 *States*), which the server reads by too: the message for the name as
 * cleaned, none left or more than `limits.groupName` characters, counted in UTF-16 units, as the server counts them; or
 * nothing once it passes.
 */
function groupNameRule(cleaned: string): string | undefined {
  if (cleaned === '') {
    return strings.commonItems.enterGroupName;
  }

  return cleaned.length > limits.groupName ? msg.tooLong(strings.commonItems.groupName, limits.groupName) : undefined;
}

/**
 * A group's name as a form that names a group holds it, in form-validation.md's one-field pattern (`useFieldText`):
 * checked by the group name rule, and refused by the server under `name` (common-items.md *Validation*).
 */
export function useGroupName(initial: string, field: RefObject<HTMLInputElement | null>) {
  return useFieldText(initial, groupNameRule, 'name', field);
}
