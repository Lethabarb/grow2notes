import { useQuery } from '@tanstack/react-query';
import { LoadRegion } from '../../components/LoadRegion.tsx';
import { PageHeading } from '../../components/PageHeading.tsx';
import { PageStatus } from '../../components/PageStatus.tsx';
import textBox from '../../components/TextBox.module.css';
import { msg } from '../../copy/messages.ts';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';
import { limits } from '../../limits.ts';
import { fieldIds } from './fieldIds.ts';
import { GuidePromptsForm } from './GuidePromptsForm.tsx';
import { guidePromptsQuery } from './guidePromptsQuery.ts';
import styles from './GuidePromptsPage.module.css';

/**
 * Manage > Guide prompts at `/manage/guide-prompts` (guide-prompts.md), in the note form's 40rem column: its `<h1>`,
 * which is the field's label, the help text and the limit hint at once, then the region that loads the prompts, whose
 * form mounts only with them, as an empty box shown while loading could be saved as no prompts.
 */
export function GuidePromptsPage() {
  const prompts = useQuery(guidePromptsQuery);

  return (
    <PageStatus>
      <div className={textBox.column}>
        <PageHeading>
          {/* Until the field mounts, it names nothing, which does no harm (guide-prompts.md component 2). */}
          <label htmlFor={fieldIds.text}>{pageName.guidePrompts}</label>
        </PageHeading>
        <p id={fieldIds.help}>{strings.guidePrompts.help}</p>
        <p id={fieldIds.limit} className={styles.limit}>
          {msg.characterLimitHint(limits.guidePrompts)}
        </p>
        <LoadRegion queries={[prompts]} loading={strings.guidePrompts.loading} what={strings.guidePrompts.thePrompts}>
          {/* Called only once the query has data. */}
          {() => <GuidePromptsForm loaded={prompts.data!} />}
        </LoadRegion>
      </div>
    </PageStatus>
  );
}
