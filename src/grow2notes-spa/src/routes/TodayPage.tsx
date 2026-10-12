import { useMe } from '../api/me.ts';
import { PageHeading } from '../components/PageHeading.tsx';
import { PageStatus } from '../components/PageStatus.tsx';
import { dateToday, parseDateOnly } from '../copy/format.ts';
import { pageName } from '../copy/pageNames.ts';
import { Sep } from '../copy/Sep.tsx';

/**
 * Today at `/`, for now its heading alone (today.md component 3): "Today · Thursday 1 October", from the server's date
 * in Melbourne, never the device's clock (A33). E02 builds the list under it.
 */
export function TodayPage() {
  const { today } = useMe();

  return (
    <PageStatus>
      <PageHeading>
        {pageName.today}
        <Sep />
        <time dateTime={today}>{dateToday(parseDateOnly(today))}</time>
      </PageHeading>
    </PageStatus>
  );
}
