import { PageStatus } from '../../components/PageStatus.tsx';
import { NoLinkMessage } from './SetupMessage.tsx';

/** `/setup/app`, which shows the no-link message whatever is in memory until S00.04.03 builds view G there. */
export function NoLinkPage() {
  return (
    <PageStatus>
      <NoLinkMessage />
    </PageStatus>
  );
}
