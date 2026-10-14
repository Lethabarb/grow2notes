import { PageHeading } from '../components/PageHeading.tsx';
import { PageStatus } from '../components/PageStatus.tsx';
import { pageName } from '../copy/pageNames.ts';
import { strings } from '../copy/strings.ts';
import { useFocusOnReplace } from './routeChange.ts';
import { ShellFrame } from './ShellFrame.tsx';
import { usePageTitle } from './usePageTitle.ts';
import { useWholePageMessage } from './wholePageMessage.ts';

/**
 * The crash page under the nav (app-shell.md component 4), which the screens' route error boundary shows in `<main>` in
 * place of a screen that threw while rendering, with the nav and Account still there. It tells the shell that it
 * shows, for the title, which comes back as it goes on the next location. It leaves the header's mark as the address
 * gives it, as the screen that crashed is the one the address names.
 */
export function CrashPage() {
  useWholePageMessage(pageName.thereIsAProblem);

  return <CrashMessage />;
}

/**
 * The shell's crash page (app-shell.md component 4): the root route's error element, in place of the shell when it
 * throws while rendering, as a header that cannot read `/me`'s answer does. The same frame with the wordmark as plain
 * text, and no nav or Account, which might throw again. It sets its own title, as no shell is there to set it.
 */
export function ShellCrashPage() {
  usePageTitle(pageName.thereIsAProblem);

  return (
    <ShellFrame me={undefined} page={undefined}>
      <CrashMessage />
    </ShellFrame>
  );
}

/**
 * Both crash pages' message, in its own `PageStatus`, as the screen's went with it, whose `<h1>` takes focus when it
 * takes the place of what had focus, as after a press or a refresh (the shell's route-change rule). Go to Today is a
 * plain link, not the router's, so that the page loads afresh, with nothing of the broken state that memory held
 * (empty-loading-error.md *Whole-page messages*).
 */
function CrashMessage() {
  useFocusOnReplace();

  return (
    <PageStatus>
      <PageHeading>{strings.thereIsAProblem}</PageHeading>
      <p>{strings.savedIsKept}</p>
      <p>
        <a href="/">{strings.goToToday}</a>
      </p>
    </PageStatus>
  );
}
