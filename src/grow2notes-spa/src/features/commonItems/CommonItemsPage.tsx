import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState, type FormEvent } from 'react';
import { Button } from '../../components/Button.tsx';
import { ListEditor } from '../../components/ListEditor.tsx';
import { ListConflict, ListFrame } from '../../components/ListFrame.tsx';
import { useListFrame, type ListFrameValue } from '../../components/listFrameContext.ts';
import { cleanText, failureAlert, focusStayedIn, useListWrite, type ListWriter } from '../../components/listWrite.ts';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { useAnnounce } from '../../components/pageStatusContext.ts';
import { RequestAlert } from '../../components/RequestAlert.tsx';
import textBox from '../../components/TextBox.module.css';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import styles from './CommonItemsPage.module.css';
import {
  commonItemGroupsApi,
  commonItemGroupsQuery,
  commonItemsApi,
  commonItemsScope,
  type AdminCommonItemGroup,
} from './commonItemsQuery.ts';
import { useGroupName } from './groupName.ts';
import { GroupNameField } from './GroupNameField.tsx';

// The help text's id, by which every item field names it in its description (common-items.md component 2).
const itemsHelpId = 'items-help';

// The New group field's id (common-items.md component 7).
const newGroupFieldId = 'group-new-name';

/**
 * What every write to a group needs: what every write on the page does (`ListWriter`), in the page's one scope, with
 * the read of its one query again and the page's list conflict; and the frame's focus that follows a change.
 */
type GroupWriter = ListWriter & Pick<ListFrameValue, 'focusAfterRender'>;

/**
 * Manage > Common items at `/manage/common-items` (common-items.md), on the wider page: its `<h1>` and the help text at
 * once, as both are fixed; then the region that loads the groups, which shows the groups once they have loaded. Every
 * group's list is in the one frame, so that the help text, the conflict and focus after a change are the page's.
 */
export function CommonItemsPage() {
  const groups = useQuery(commonItemGroupsQuery);

  return (
    <PageStatus>
      <PageHeading>{pageName.commonItems}</PageHeading>
      <ListFrame helpId={itemsHelpId} help={strings.listEditor.item.help}>
        <LoadRegion queries={[groups]} loading={strings.commonItems.loading} what={strings.commonItems.items}>
          {/* Called only once the query has data. */}
          {() => <Groups />}
        </LoadRegion>
      </ListFrame>
    </PageStatus>
  );
}

/**
 * What the load region shows once the groups have loaded: the list conflict's place, a section for each active group,
 * in the list's order, Every note first, then the New group form. No add form shows before the groups have loaded, as
 * one could add to a list the manager cannot see. It reads the groups from the page's one query, as the frame renders
 * it again with each request for focus, reading the query as the write that asked left it, so that a group just added
 * has its section, and its New item field, by the time the frame focuses that field. Every write to a group is in the
 * page's one scope, and reads the one query again, as every item write does (common-items.md component 5).
 */
function Groups() {
  const frame = useListFrame();
  const queryClient = useQueryClient();
  // Already read by the page, which renders this only once it has the groups.
  const groups = useQuery({ ...commonItemGroupsQuery, refetchOnMount: false }).data!;
  const writer: GroupWriter = {
    scopeId: commonItemsScope,
    reread: () => queryClient.refetchQueries({ queryKey: commonItemGroupsQuery.queryKey, exact: true }),
    changedMeanwhile: frame.changedMeanwhile,
    written: frame.written,
    focusAfterRender: frame.focusAfterRender,
  };

  return (
    <>
      <ListConflict />
      {groups.flatMap((group) => (group.archivedAtUtc === null ? [<GroupSection key={group.id} group={group} />] : []))}
      <NewGroupForm writer={writer} />
    </>
  );
}

/**
 * An active group's section (common-items.md component 5), a region named by its `<h2>`, the group's name as stored,
 * which wraps anywhere and is never cut short: Every note's with the line that says why it has no group buttons, as
 * it can never be renamed, moved or archived (A43); then the group's items, in a list of their own whose words say
 * " in [group name]" where the page's lists would otherwise read alike, each item's edit field `item-{id}-text`
 * (component 6), and its add form, whose field is `item-new-{groupId}`, which adds to the end of this group alone.
 */
function GroupSection({ group }: { group: AdminCommonItemGroup }) {
  const headingId = `group-${group.id}-heading`;

  return (
    <section aria-labelledby={headingId} className={styles.group}>
      <h2 id={headingId} className={styles.heading}>
        {group.name}
      </h2>
      {group.isEveryNote && <p className={styles.everyNote}>{strings.commonItems.everyNote}</p>}
      <ListEditor
        kind="item"
        api={commonItemsApi(group.id)}
        name={group.name}
        addFieldId={newItemFieldId(group.id)}
        editFieldId={itemFieldId}
      />
    </section>
  );
}

/**
 * The New group form (common-items.md component 7), a section's gap after the last group's section, in the reading
 * column: the group name field, labelled "New group", then the alert for a request that failed, then Add group,
 * secondary and label width. Add group checks the name alone, in form-validation.md's one-field pattern, and sends
 * nothing while it fails: the message above the field, and focus on it. Sent, the group goes to the end of the
 * groups, empty, and once the list is read again its section is the last, just above this form; then the field is
 * emptied if it still holds the name sent, focus goes to the new group's New item field, the next useful step, as an
 * empty group does not show on notes, unless the person has gone elsewhere meanwhile, and the page's status region
 * says so. A failure keeps the name: the server's refusal of it shows on the field as Add group's own does, a `401`
 * hands over to sign-in in place, as `api()` started it, and anything else says so in the alert, focus staying on Add
 * group.
 */
function NewGroupForm({ writer }: { writer: GroupWriter }) {
  const announce = useAnnounce();
  const form = useRef<HTMLFormElement>(null);
  const field = useRef<HTMLInputElement>(null);
  const name = useGroupName('', field);
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const add = useListWrite(writer, commonItemGroupsApi.add);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    const sent = name.checked();
    if (sent === undefined) {
      return;
    }

    add.mutate(sent, {
      onSuccess: ({ id }) => {
        // Emptied only while it still holds what was sent, so that nothing typed while the add was out is lost.
        name.setText((current) => (cleanText(current) === sent ? '' : current));
        if (focusStayedIn(form.current)) {
          writer.focusAfterRender(newItemFieldId(id));
        }

        announce?.(strings.commonItems.groupAdded);
      },
      onError: (failed) => {
        if (!name.refusedByServer(failed)) {
          setFailure(failureAlert(failed, msg.notAdded));
        }
      },
    });
  }

  return (
    <form ref={form} noValidate className={`${textBox.column} ${styles.newGroup}`} onSubmit={submit}>
      <GroupNameField
        fieldId={newGroupFieldId}
        label={strings.commonItems.newGroup}
        name={name.text}
        error={name.error}
        inputs={name.inputs}
        fieldRef={field}
        onChange={name.edit}
      />
      <RequestAlert>{failure}</RequestAlert>
      <Button type="submit" className={styles.addGroup} busy={add.isPending} busyLabel={strings.adding}>
        {strings.commonItems.addGroup}
      </Button>
    </form>
  );
}

/** A group's New item field's id (common-items.md component 5), which takes focus after Add group adds the group. */
function newItemFieldId(groupId: string): string {
  return `item-new-${groupId}`;
}

/** An item's edit field's id (common-items.md component 6). */
function itemFieldId(itemId: string): string {
  return `item-${itemId}-text`;
}
