import { useEffect } from 'react';
import { focusPageHeading } from '../../components/focusPageHeading.ts';
import { PageHeading } from '../../components/PageHeading.tsx';
import { pageName } from '../../copy/pageNames.ts';
import { strings } from '../../copy/strings.ts';

type SetupMessageProps = { heading: string; body: string };

/**
 * A whole-page setup message (sign-in.md component 12): an `<h1>`, which takes focus as it shows, so that a screen
 * reader reads it, and one sentence that says what to do instead, with no button, as nothing on this page can help.
 */
export function SetupMessage({ heading, body }: SetupMessageProps) {
  useEffect(() => {
    focusPageHeading();
  }, []);

  return (
    <>
      <PageHeading>{heading}</PageHeading>
      <p>{body}</p>
    </>
  );
}

/** sign-in.md's *No link in memory*: a setup page opened with no link, as after a refresh or from a copied address. */
export function NoLinkMessage() {
  return <SetupMessage heading={pageName.setUpYourAccount} body={strings.setup.noLink} />;
}
