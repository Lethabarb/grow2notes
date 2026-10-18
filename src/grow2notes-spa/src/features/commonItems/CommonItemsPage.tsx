import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState, type FormEvent, type MouseEvent, type RefObject } from 'react';
import { flushSync } from 'react-dom';
import { Button } from '../../components/Button.tsx';
import { ListEditor, MovePair, type MoveList } from '../../components/ListEditor.tsx';
import rows from '../../components/ListEditor.module.css';
import { ListConflict, ListFrame } from '../../components/ListFrame.tsx';
import { useListFrame } from '../../components/listFrameContext.ts';
import {
  cleanText,
  failureAlert,
  focusStayedIn,
  ListChanged,
  sendUnlessListChanged,
  useListWrite,
} from '../../components/listWrite.ts';
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

// Archived groups' key among the elements that take focus after a change, which a group's Archive opens before the
// group's Restore there takes focus (common-items.md component 8).
const archivedGroupsKey = 'archived-groups';

/**
 * What every write to a group needs: what a move does (`MoveList`), in the page's one scope, with the read of its one
 * query again, the page's list conflict, the frame's focus that follows a change, and the order of the groups that
 * move; the groups as last read, which a write reads as it runs; and Archived groups, which a group's Archive opens.
 */
type GroupWriter = MoveList & {
  /** The groups as the page's one query last read them. */
  latest: () => AdminCommonItemGroup[];
  /**
   * Opens Archived groups once the render that a group's Archive brings is in the DOM, as it may have only just
   * appeared, then focuses the element registered under `key`, if one is given.
   */
  openArchived: (key: string | undefined) => void;
};

/** The active group of ID `id` among `groups`, or `undefined` once it is archived or gone. */
function activeGroup(groups: AdminCommonItemGroup[], id: string): AdminCommonItemGroup | undefined {
  return groups.find((group) => group.id === id && group.archivedAtUtc === null);
}

/** The archived group of ID `id` among `groups`, or `undefined` once it is active or gone. */
function archivedGroup(groups: AdminCommonItemGroup[], id: string): AdminCommonItemGroup | undefined {
  return groups.find((group) => group.id === id && group.archivedAtUtc !== null);
}

/** The archived groups among `groups`, newest archived first (common-items.md component 8). */
function archivedNewestFirst(groups: AdminCommonItemGroup[]): AdminCommonItemGroup[] {
  return groups
    .flatMap((group) => (group.archivedAtUtc === null ? [] : [{ group, archivedAt: Date.parse(group.archivedAtUtc) }]))
    .sort((a, b) => b.archivedAt - a.archivedAt)
    .map(({ group }) => group);
}

/**
 * The groups that a manager moves among `groups`, the active ones but Every note, in the list's order, which always has
 * Every note first, so that no move can put a group above it (A43).
 */
function movableGroups(groups: AdminCommonItemGroup[]): AdminCommonItemGroup[] {
  return groups.filter((group) => !group.isEveryNote && group.archivedAtUtc === null);
}

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
 * in the list's order, Every note first, then the New group form, then Archived groups while a group is archived. No
 * add form shows before the groups have loaded, as one could add to a list the manager cannot see. It reads the groups
 * from the page's one query, as the frame renders it again with each request for focus, reading the query as the
 * write that asked left it, so that a group just added has its section, and its New item field, by the time the frame
 * focuses that field, a group just renamed its Rename, a group just archived its Restore and a group just restored its
 * Archive. Every write to a group is in the page's one scope, and reads the one query again, as every item write does
 * (common-items.md component 5).
 */
function Groups() {
  const frame = useListFrame();
  const queryClient = useQueryClient();
  // Already read by the page, which renders this only once it has the groups.
  const groups = useQuery({ ...commonItemGroupsQuery, refetchOnMount: false }).data!;
  const active = groups.filter((group) => group.archivedAtUtc === null);
  const movable = movableGroups(groups);
  const archived = archivedNewestFirst(groups);
  const latest = () => queryClient.getQueryData(commonItemGroupsQuery.queryKey) ?? [];
  const writer: GroupWriter = {
    scopeId: commonItemsScope,
    reread: () => queryClient.refetchQueries({ queryKey: commonItemGroupsQuery.queryKey, exact: true }),
    changedMeanwhile: frame.changedMeanwhile,
    written: frame.written,
    focusTarget: frame.focusTarget,
    focusAfterRender: frame.focusAfterRender,
    order: () => movableGroups(latest()).map((group) => group.id),
    reorder: commonItemGroupsApi.reorder,
    latest,
    openArchived: (key) => {
      frame.openArchivedAfterRender(archivedGroupsKey, key);
    },
  };

  return (
    <>
      <ListConflict />
      {active.map((group) => (
        <GroupSection
          key={group.id}
          group={group}
          writer={writer}
          active={active}
          first={group === movable[0]}
          last={group === movable.at(-1)}
        />
      ))}
      <NewGroupForm writer={writer} />
      {archived.length > 0 && <ArchivedGroups groups={archived} writer={writer} />}
    </>
  );
}

type GroupProps = { group: AdminCommonItemGroup; writer: GroupWriter };

/** Where a group is among the groups that move, which makes its Move up or Move down unavailable at either end. */
type GroupEnds = {
  /** Whether the group is the first after Every note. */
  first: boolean;
  /** Whether the group is the last. */
  last: boolean;
};

type GroupSectionProps = GroupProps &
  GroupEnds & {
    /** The active groups, in the list's order, which each item's Group radios list. */
    active: AdminCommonItemGroup[];
  };

/**
 * An active group's section (common-items.md component 5), a region named by its `<h2>`, the group's name as stored,
 * which wraps anywhere and is never cut short: Every note's with the line that says why it has no group buttons, as
 * it can never be renamed, moved or archived (A43), and every other group's in its heading row; then the group's
 * items, in a list of their own whose words say " in [group name]" where the page's lists would otherwise read alike,
 * each item's edit field `item-{id}-text`, with its Group radios, `item-{id}-group`, listing `active`, the active
 * groups in the list's order (component 6), and its add form, whose field is `item-new-{groupId}`, which adds to the
 * end of this group alone.
 */
function GroupSection({ group, writer, active, first, last }: GroupSectionProps) {
  const headingId = `group-${group.id}-heading`;

  return (
    <section aria-labelledby={headingId} className={styles.group}>
      {group.isEveryNote ? (
        <>
          <h2 id={headingId} className={styles.heading}>
            {group.name}
          </h2>
          <p className={styles.everyNote}>{strings.commonItems.everyNote}</p>
        </>
      ) : (
        <GroupHead group={group} writer={writer} first={first} last={last} headingId={headingId} />
      )}
      <ListEditor
        kind="item"
        api={commonItemsApi(group.id)}
        name={group.name}
        addFieldId={newItemFieldId(group.id)}
        editFieldId={itemFieldId}
        groups={{ own: group.id, active, fieldName: itemGroupFieldName }}
      />
    </section>
  );
}

/**
 * The heading row of a group other than Every note (common-items.md component 5), a wrapping flex line as an item row
 * is: its `<h2>`, then the group's alert slot, always rendered, which a failed move or Archive writes to, and its
 * buttons in two pairs, Move up and Move down, then Rename and Archive, each label width, with ", [name] group" after
 * its label for screen readers alone; beside the heading while the row has room, under it otherwise. Move up and Move
 * down are the item rows' own, moving the group among the groups but Every note, which stays first, so Move up on the
 * first group after it and Move down on the last are unavailable; a move sends every one of those groups' IDs, and the
 * sections take their new order only once the list is read again, focus following the button as its whole section
 * moves. Rename puts the rename form in the buttons' place, under the `<h2>`, which stays, so that the region keeps
 * its name and the manager sees what is being renamed, until Save or Cancel closes it and focus goes back to Rename,
 * unless a Save finds it gone elsewhere as it ends. The form is rendered inside the press, so that focus goes to its
 * field, with the caret after the name, while the tap still runs, which is what lets a phone open its keyboard. Its
 * typed name is the group's own, so it stays as typed while other groups change.
 *
 * Archive asks nothing, as a restore undoes it (design.md §4.9). It is busy at once, "Archiving…" after 400 ms, and
 * waits its turn in the page's scope; when it runs, it sends the group's version as the list was last read. Once the
 * list is read again, the section has left the page with its items for the top of Archived groups, which appears if
 * it was absent and opens, and focus goes to the group's Restore there, unless the person has gone elsewhere meanwhile,
 * and the page's status region says so. An Archive whose group has already left the active groups, or that the server
 * refuses as stale, shows the list conflict over the list read again; any other failure says so in the group's alert,
 * focus staying on Archive, Every note's refusal and a missing `If-Match` among them, as the screen never sends either.
 */
function GroupHead({ group, writer, first, last, headingId }: GroupProps & GroupEnds & { headingId: string }) {
  const announce = useAnnounce();
  const [renaming, setRenaming] = useState(false);
  // The alert's words while it holds a move's or an Archive's failure, which the group's next press, or Rename, clears.
  const [failure, setFailure] = useState<string>();
  const field = useRef<HTMLInputElement>(null);
  const renameKey = `${group.id}:rename`;
  const context = msg.groupContext(group.name);
  const archive = useListWrite(writer, async () => {
    // The version as the write runs, which the list read after the write before it may have changed.
    const current = activeGroup(writer.latest(), group.id);
    if (current === undefined) {
      throw new ListChanged();
    }

    await sendUnlessListChanged(writer, 412, () => commonItemGroupsApi.archive(group.id, current.rowVersion));
  });

  function rename() {
    flushSync(() => {
      // The form takes the alert's place, which comes back as it closes, so it must come back empty, not stale.
      setFailure(undefined);
      setRenaming(true);
    });
    const input = field.current;
    if (input !== null) {
      input.focus();
      input.setSelectionRange(input.value.length, input.value.length);
    }
  }

  function close(focusRename: boolean) {
    setRenaming(false);
    if (focusRename) {
      writer.focusAfterRender(renameKey);
    }
  }

  function pressArchive(event: MouseEvent<HTMLButtonElement>) {
    const button = event.currentTarget;
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    archive.mutate(undefined, {
      onSuccess: () => {
        writer.openArchived(focusStayedIn(button) ? `${group.id}:restore` : undefined);
        announce?.(strings.commonItems.groupArchived);
      },
      onError: (failed) => {
        setFailure(failureAlert(failed, msg.notArchived));
      },
    });
  }

  return (
    <>
      <div className={styles.groupHead}>
        <h2 id={headingId} className={styles.heading}>
          {group.name}
        </h2>
        {!renaming && (
          <div className={rows.side}>
            <RequestAlert>{failure}</RequestAlert>
            <div className={rows.actions}>
              <MovePair
                list={writer}
                id={group.id}
                first={first}
                last={last}
                context={context}
                moved={msg.groupMoved}
                onFailure={setFailure}
              />
              <div className={rows.pair}>
                <Button ref={writer.focusTarget(renameKey)} className={rows.rowButton} onClick={rename}>
                  {strings.commonItems.rename}
                  <span className="visually-hidden">{context}</span>
                </Button>
                <Button
                  ref={writer.focusTarget(`${group.id}:archive`)}
                  className={rows.rowButton}
                  busy={archive.isPending}
                  busyLabel={strings.archiving}
                  onClick={pressArchive}
                >
                  {strings.listRows.archive}
                  <span className="visually-hidden">{context}</span>
                </Button>
              </div>
            </div>
          </div>
        )}
      </div>
      {renaming && <RenameForm group={group} writer={writer} field={field} onClose={close} />}
    </>
  );
}

type RenameFormProps = GroupProps & {
  /** The Group name field, which the heading row focuses as it opens the form. */
  field: RefObject<HTMLInputElement | null>;
  /** Closes the form, focusing the group's Rename when `focusRename`. */
  onClose: (focusRename: boolean) => void;
};

/**
 * A group's rename form (common-items.md component 5), in the reading column: the Group name field, `group-{id}-name`,
 * filled with the group's name, then the alert for a request that failed, then Save, primary, and Cancel, which drops
 * what was typed, both label width. Save checks the name as Add group does, sending nothing while it fails; then, when
 * the name as Save would send it is the group's own, it closes the form as Cancel does and sends nothing. Otherwise it
 * sends the name with the group's version as the write runs, the field read-only while it is out, and once the list is
 * read again, closes the form, the heading and the group's New item label now saying the new name, focus going to
 * Rename unless the person has gone elsewhere meanwhile, and the page's status region says so. A failure keeps the
 * name, focus staying on Save: the server's refusal of the name shows on the field as Save's own does; a Save refused
 * as stale, or whose group has left the active list, shows the list conflict over the list read again, the form still
 * open with the typed name, and the next Save sends the group's new version, unless the group has left the page, and
 * its form with it (common-items.md *Failures*); a `401` hands over to sign-in in place, as `api()` started it; and
 * anything else says so in the alert, Every note's refusal and a missing `If-Match` among them, as the screen never
 * sends either. Cancel stays free while a Save is out or queued, closing the form, as a write once started runs to its
 * end and the list then shows what the server holds.
 */
function RenameForm({ group, writer, field, onClose }: RenameFormProps) {
  const announce = useAnnounce();
  const form = useRef<HTMLFormElement>(null);
  const name = useGroupName(group.name, field);
  // The alert's words while it holds a request's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const save = useListWrite(writer, async (sent: string) => {
    // The version as the write runs, which the list read after the write before it may have changed.
    const current = activeGroup(writer.latest(), group.id);
    if (current === undefined) {
      throw new ListChanged();
    }

    await sendUnlessListChanged(writer, 412, () => commonItemGroupsApi.rename(group.id, sent, current.rowVersion));
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    const sent = name.checked();
    if (sent === undefined) {
      return;
    }

    if (sent === group.name) {
      onClose(true);
      return;
    }

    save.mutate(sent, {
      onSuccess: () => {
        onClose(focusStayedIn(form.current));
        announce?.(strings.commonItems.groupRenamed);
      },
      onError: (failed) => {
        if (!name.refusedByServer(failed)) {
          setFailure(failureAlert(failed, msg.notSaved));
        }
      },
    });
  }

  return (
    <form ref={form} noValidate className={`${textBox.column} ${styles.renameForm}`} onSubmit={submit}>
      <GroupNameField
        fieldId={`group-${group.id}-name`}
        label={strings.commonItems.groupName}
        name={name.text}
        error={name.error}
        inputs={name.inputs}
        readOnly={save.isPending}
        fieldRef={field}
        onChange={name.edit}
      />
      <RequestAlert>{failure}</RequestAlert>
      <div className={rows.actions}>
        <Button
          type="submit"
          variant="primary"
          className={rows.rowButton}
          busy={save.isPending}
          busyLabel={strings.saving}
        >
          {strings.save}
        </Button>
        <Button
          className={rows.rowButton}
          onClick={() => {
            onClose(true);
          }}
        >
          {strings.cancel}
        </Button>
      </div>
    </form>
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

/**
 * Archived groups (common-items.md component 8), a section's gap after the New group form, which the page renders only
 * while a group is archived: a native `<details>`, closed as it loads and left to the manager from then on but for a
 * group's Archive, which opens it, its summary "Archived groups" in the item lists' style; then the archived groups,
 * newest archived first, each its name and Restore alone, as nothing else can be done to an archived group.
 */
function ArchivedGroups({ groups, writer }: { groups: AdminCommonItemGroup[]; writer: GroupWriter }) {
  return (
    <details ref={writer.focusTarget(archivedGroupsKey)} className={styles.archivedGroups}>
      <summary className={rows.summary}>{strings.commonItems.archivedGroups}</summary>
      <ul className={rows.archivedList}>
        {groups.map((group) => (
          <ArchivedGroupRow key={group.id} group={group} writer={writer} />
        ))}
      </ul>
    </details>
  );
}

/**
 * An archived group's row (common-items.md component 8), as an archived item's is: its name, wrapping anywhere, then
 * its alert slot, always rendered, then Restore, label width, with ", [name] group" after its label for screen readers
 * alone. Restore is busy at once, "Restoring…" after 400 ms, and waits its turn in the page's scope; when it runs, it
 * sends the group's version as the list was last read. Once the list is read again, the group's section is the last
 * of the groups, with its active items, and focus goes to its Archive there, unless the person has gone elsewhere
 * meanwhile, and the page's status region says so; Archived groups goes once it is empty. A Restore whose group is no
 * longer archived, or that the server refuses as stale, shows the list conflict over the list read again; any other
 * failure says so in the row's alert, focus staying on Restore.
 */
function ArchivedGroupRow({ group, writer }: GroupProps) {
  const announce = useAnnounce();
  // The alert's words while it holds a Restore's failure, which the next press clears.
  const [failure, setFailure] = useState<string>();
  const restore = useListWrite(writer, async () => {
    // The version as the write runs, which the list read after the write before it may have changed.
    const current = archivedGroup(writer.latest(), group.id);
    if (current === undefined) {
      throw new ListChanged();
    }

    await sendUnlessListChanged(writer, 412, () => commonItemGroupsApi.restore(group.id, current.rowVersion));
  });

  function press(event: MouseEvent<HTMLButtonElement>) {
    const button = event.currentTarget;
    // Emptied at each press, so that what the press brings is written into an empty region and announced.
    setFailure(undefined);
    restore.mutate(undefined, {
      onSuccess: () => {
        if (focusStayedIn(button)) {
          writer.focusAfterRender(`${group.id}:archive`);
        }

        announce?.(strings.commonItems.groupRestored);
      },
      onError: (failed) => {
        setFailure(failureAlert(failed, msg.notRestored));
      },
    });
  }

  return (
    <li className={rows.row}>
      <div className={rows.rowInner}>
        <p className={rows.text}>{group.name}</p>
        <div className={rows.side}>
          <RequestAlert>{failure}</RequestAlert>
          <div className={rows.actions}>
            <Button
              ref={writer.focusTarget(`${group.id}:restore`)}
              className={rows.rowButton}
              busy={restore.isPending}
              busyLabel={strings.restoring}
              onClick={press}
            >
              {strings.restore}
              <span className="visually-hidden">{msg.groupContext(group.name)}</span>
            </Button>
          </div>
        </div>
      </div>
    </li>
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

/** The `name` of an item's Group radios (common-items.md component 6). */
function itemGroupFieldName(itemId: string): string {
  return `item-${itemId}-group`;
}
