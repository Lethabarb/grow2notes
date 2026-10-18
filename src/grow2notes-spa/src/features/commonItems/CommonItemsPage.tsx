import { useQuery } from '@tanstack/react-query';
import { ListEditor } from '../../components/ListEditor.tsx';
import { ListConflict, ListFrame } from '../../components/ListFrame.tsx';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import styles from './CommonItemsPage.module.css';
import { commonItemGroupsQuery, commonItemsApi, type AdminCommonItemGroup } from './commonItemsQuery.ts';

// The help text's id, by which every item field names it in its description (common-items.md component 2).
const itemsHelpId = 'items-help';

/**
 * Manage > Common items at `/manage/common-items` (common-items.md), on the wider page: its `<h1>` and the help text at
 * once, as both are fixed; then the region that loads the groups, which shows, once they have loaded, the list
 * conflict's place and a section for each active group, in the list's order, Every note first. No add form shows
 * before the groups have loaded, as one could add an item to a list the manager cannot see. Every group's list is in
 * the one frame, so that the help text, the conflict and focus after a change are the page's.
 */
export function CommonItemsPage() {
  const groups = useQuery(commonItemGroupsQuery);

  return (
    <PageStatus>
      <PageHeading>{pageName.commonItems}</PageHeading>
      <ListFrame helpId={itemsHelpId} help={strings.listEditor.item.help}>
        <LoadRegion queries={[groups]} loading={strings.commonItems.loading} what={strings.commonItems.items}>
          {/* Called only once the query has data. */}
          {() => (
            <>
              <ListConflict />
              {groups.data!.flatMap((group) =>
                group.archivedAtUtc === null ? [<GroupSection key={group.id} group={group} />] : [],
              )}
            </>
          )}
        </LoadRegion>
      </ListFrame>
    </PageStatus>
  );
}

/**
 * An active group's section (common-items.md component 5), a region named by its `<h2>`, the group's name as stored,
 * which wraps anywhere and is never cut short: Every note's with the line that says why it has no group buttons, as
 * it can never be renamed, moved or archived (A43); then the group's items, in a list of their own whose words say
 * " in [group name]" where the page's lists would otherwise read alike.
 */
function GroupSection({ group }: { group: AdminCommonItemGroup }) {
  const headingId = `group-${group.id}-heading`;

  return (
    <section aria-labelledby={headingId} className={styles.group}>
      <h2 id={headingId} className={styles.heading}>
        {group.name}
      </h2>
      {group.isEveryNote && <p className={styles.everyNote}>{strings.commonItems.everyNote}</p>}
      <ListEditor kind="item" api={commonItemsApi(group.id)} name={group.name} />
    </section>
  );
}
