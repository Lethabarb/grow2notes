import { PageStatus } from '../../components/PageStatus.tsx';
import { NoLinkMessage } from './SetupMessage.tsx';

/**
 * `/setup/password` and `/setup/app`, which show the no-link message whatever is in memory until S00.04.03 builds
 * views F and G there (sign-in.md).
 */
export function NoLinkPage() {
  return (
    <PageStatus>
      <NoLinkMessage />
    </PageStatus>
  );
}
