import type { Page, Route } from '@playwright/test';
import { expect, test } from './fixtures.ts';
import {
  failPrompts,
  nextVersion,
  promptsAnswer,
  promptsStore,
  promptsVersion,
  savedAnswer,
  savedPrompts,
  sentSave,
  servePrompts,
  serveSaves,
} from './guidePrompts.ts';
import { boxOf } from './layout.ts';
import { manager, serveMe, worker } from './me.ts';
import { holdNext } from './requests.ts';
import { people, serveUsers } from './users.ts';

// Manage > Guide prompts in the published app (S01.04.01, S01.04.02), against guide-prompts.md's criteria for access
// and structure, loading, the field, the limit and count, Save and leaving. The tests answer /api/auth/me and
// /api/admin/guide-prompts in the browser (me.ts, guidePrompts.ts), so that neither request reaches the app, but for
// the one that signs in for real and saves to the app's own database.

const address = '/manage/guide-prompts';
const help = 'Prompts are a guide only. They disappear as soon as someone starts typing and are not saved into notes.';
const limitHint = 'You can enter up to 1,000 characters';
const tooLong = 'Guide prompts must be 1,000 characters or less';
const conflict =
  'Another manager saved the guide prompts while you were editing. Your text is still here. Select Save to replace '
  + 'their version with yours.';
// "Saved 4:12 pm", with the non-breaking space that keeps the time and "pm" together (microcopy.md §3).
const savedAtFourTwelve = 'Saved 4:12\u00A0pm';
// Any time on the server's clock, for a save that the app itself answered.
const savedAnyTime = /^Saved (?:\d{1,2}:\d{2}\u00A0[ap]m|midday|midnight)$/;
// The colours of the hint's words and the box's border, and of an error (tokens.css).
const secondaryText = 'rgb(72, 73, 73)';
const controlBorder = 'rgb(11, 12, 12)';
const error = 'rgb(164, 0, 15)';

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const field = (page: Page) => main(page).getByRole('textbox', { name: 'Guide prompts', exact: true });
// The form's one button, named Save, or "Saving…" once it has been busy for 400 ms.
const save = (page: Page) => main(page).locator('form').getByRole('button');
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
// The region's line, beside the page's visually hidden status region and outside the form, which has the save status
// (guide-prompts.md *Live regions*).
const statusLine = (page: Page) =>
  main(page).getByRole('status').and(page.locator(':not(.visually-hidden):not(form *)'));
const saveStatus = (page: Page) => main(page).locator('form').getByRole('status');
const requestAlert = (page: Page) => main(page).locator('form').getByRole('alert');
const fieldError = (page: Page) => main(page).locator('#guidePrompts-error');
// The count under the box, hidden from screen readers, and its visually hidden twin, which they hear.
const shownCount = (page: Page) => main(page).locator('form p[aria-hidden="true"]');
const countTwin = (page: Page) => main(page).locator('form [aria-live="polite"]');
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });
// The nav items marked current, Manage alone (guide-prompts.md component 1).
const marked = (page: Page) => page.getByRole('banner').locator('[aria-current]');

/** Whether the page's focus is on `<body>`, where it stays when nothing has moved it. */
const focusOnBody = (page: Page) => page.evaluate(() => document.activeElement === document.body);

/**
 * `count` characters of text, in lines of 90 with a line break after each, which counts as one character, and never
 * one at the end, which Save would trim: 1,000 characters hold 10 line breaks.
 */
const textOf = (count: number) =>
  Array.from({ length: count }, (_, index) => (index % 91 === 90 && index < count - 1 ? '\n' : 'x')).join('');

/** Whether the page asks before it goes: whether it cancels a `beforeunload`, as a page with text not saved does. */
const warnsBeforeUnload = (page: Page) =>
  page.evaluate(() => {
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event);
    return event.defaultPrevented;
  });

/** Opens the page as `manager`, with `text` as the organisation's prompts, and waits for the field. */
async function openWith(page: Page, text = savedPrompts) {
  await serveMe(page, manager);
  await servePrompts(page, text);
  await page.goto(address);
  await expect(field(page)).toHaveValue(text);
}

/**
 * The `PUT`s of the prompts that the page sends from now on, however they are met. A count that should not have grown
 * is read once the test has the answer to a request of its own (tests/README.md).
 */
function countSaves(page: Page): () => number {
  let count = 0;
  page.on('request', (request) => {
    if (request.method() === 'PUT' && new URL(request.url()).pathname === '/api/admin/guide-prompts') {
      count += 1;
    }
  });
  return () => count;
}

/**
 * Notes in the page each field and each button, such as Save, that `<main>` gains from now on, however briefly it
 * stays, so that a test can show that none came before an answer.
 */
async function watchForTheForm(page: Page) {
  await page.evaluate(() => {
    const seen: string[] = [];
    Object.assign(window, { formSeen: seen });
    new MutationObserver((records) => {
      for (const { addedNodes } of records) {
        for (const node of addedNodes) {
          if (!(node instanceof Element)) {
            continue;
          }

          for (const element of [node, ...node.querySelectorAll('textarea, button')]) {
            if (element.matches('textarea, button')) {
              seen.push(element.localName);
            }
          }
        }
      }
    }).observe(document.querySelector('main')!, { childList: true, subtree: true });
  });
}

/** What `watchForTheForm` has noted so far. */
const formSeen = (page: Page) => page.evaluate(() => (window as unknown as { formSeen: string[] }).formSeen);

/** The `GET`s for the prompts that the page sends from now on. */
function countPromptsRequests(page: Page): () => number {
  let count = 0;
  page.on('request', (request) => {
    if (request.method() === 'GET' && new URL(request.url()).pathname === '/api/admin/guide-prompts') {
      count += 1;
    }
  });
  return () => count;
}

test('a manager sees the <h1>, help and limit hint, the saved text exactly in #0b0c0c, and Manage marked', async ({
  page,
}) => {
  await serveMe(page, manager);
  await servePrompts(page, savedPrompts);
  await page.goto(address);

  await expect(heading(page)).toHaveText('Guide prompts');
  await expect(page).toHaveTitle('Grow2Notes – Guide prompts');
  await expect(marked(page)).toHaveText(['Manage']);
  await expect(marked(page)).toHaveAttribute('aria-current', 'true');
  await expect(main(page).getByText(help, { exact: true })).toBeVisible();
  await expect(main(page).getByText(limitHint, { exact: true })).toBeVisible();

  // The text as saved, its line breaks and blank line kept, in the body text's colour, never the placeholder's grey.
  await expect(field(page)).toHaveValue(savedPrompts);
  await expect(field(page)).toHaveCSS('color', 'rgb(11, 12, 12)');
  await expect(field(page)).toHaveAccessibleDescription(`${help} ${limitHint}`);
  for (const attribute of ['placeholder', 'maxlength', 'disabled', 'readonly', 'autofocus']) {
    await expect(field(page), attribute).not.toHaveAttribute(attribute);
  }
  await expect(field(page)).toHaveAttribute('writingsuggestions', 'false');
  await expect(field(page)).toHaveAttribute('spellcheck', 'true');
  await expect(field(page)).toHaveAttribute('autocapitalize', 'sentences');
  await expect(field(page)).toHaveAttribute('autocomplete', 'off');
  // A press on the <h1> focuses the field it labels.
  expect(await focusOnBody(page)).toBe(true);
  await heading(page).click();
  await expect(field(page)).toBeFocused();
});

test('the field is never focused by itself, on a first load or after a press of Guide prompts on Manage', async ({
  page,
}) => {
  await serveMe(page, manager);
  await servePrompts(page, savedPrompts);
  await page.goto(address);
  await expect(field(page)).toHaveValue(savedPrompts);
  await expect(field(page)).not.toBeFocused();

  await nav(page).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Guide prompts', exact: true }).click();
  await expect(field(page)).toHaveValue(savedPrompts);
  await expect(field(page)).not.toBeFocused();
});

test('a worker at the address meets Page not found, and no request for the prompts is sent', async ({ page }) => {
  await serveMe(page, worker);
  await servePrompts(page, savedPrompts);
  const promptsRequests = countPromptsRequests(page);
  await page.goto(address);

  await expect(heading(page)).toHaveText('Page not found');
  await expect(page).toHaveTitle('Grow2Notes – Page not found');
  await expect(main(page).getByRole('link', { name: 'Go to Today' })).toHaveAttribute('href', '/');
  await expect(field(page)).toHaveCount(0);
  // Reported in the order sent, so a request for the prompts, had the page sent one, would come before this one.
  await page.evaluate(() => fetch('/healthz'));
  expect(promptsRequests()).toBe(0);
});

test('with the GET held 3 s, no field or Save until its answer, nothing at 999 ms, then the loading line', async ({
  page,
}) => {
  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await serveMe(page, manager);
  const heldPrompts = await holdNext(page, '**/api/admin/guide-prompts');
  await page.goto('/manage');
  await expect(heading(page)).toHaveText('Manage');
  await watchForTheForm(page);

  // Paused a second ahead of the page's time, so that it moves only as the test runs it on from the press.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));
  await main(page).getByRole('link', { name: 'Guide prompts', exact: true }).click();
  const prompts = await heldPrompts();

  // The <h1>, help text and limit hint at once, and nothing more (guide-prompts.md *States*: Waiting).
  await page.clock.runFor(999);
  await expect(heading(page)).toHaveText('Guide prompts');
  await expect(main(page).getByText(help, { exact: true })).toBeVisible();
  await expect(main(page).getByText(limitHint, { exact: true })).toBeVisible();
  await expect(statusLine(page)).toBeEmpty();
  await page.clock.runFor(1);
  await expect(statusLine(page)).toHaveText('Loading guide prompts…');
  await page.clock.runFor(1_999);
  await expect(statusLine(page)).toHaveText('Loading guide prompts…');
  await expect(field(page)).toHaveCount(0);
  await expect(save(page)).toHaveCount(0);
  expect(await formSeen(page)).toEqual([]);

  // The answer, at 3 s, mounts the field and clears the line.
  await page.clock.runFor(1);
  await page.clock.resume();
  await prompts.fulfill(promptsAnswer(savedPrompts));
  await expect(field(page)).toHaveValue(savedPrompts);
  await expect(statusLine(page)).toBeEmpty();
  expect(await formSeen(page)).toContain('textarea');
});

test('with no connection, the prompts fail, then say still, and a Try again that loads focuses <h1>', async ({
  page,
}) => {
  await serveMe(page, manager);
  await failPrompts(page);
  await page.goto(address);

  await expect(statusLine(page)).toHaveText('The guide prompts did not load: no connection. Try again.');
  await expect(heading(page)).toHaveText('Guide prompts');
  await expect(field(page)).toHaveCount(0);

  await tryAgain(page).click();
  await expect(statusLine(page)).toHaveText('The guide prompts still did not load: no connection. Try again.');
  await expect(tryAgain(page)).toBeFocused();
  await expect(field(page)).toHaveCount(0);

  await servePrompts(page, savedPrompts);
  await tryAgain(page).click();
  await expect(field(page)).toHaveValue(savedPrompts);
  // Try again has gone with the sentence, so focus goes to the <h1>, not the field (guide-prompts.md *Interactions*).
  await expect(heading(page)).toBeFocused();
  await expect(statusLine(page)).toBeEmpty();
  await expect(tryAgain(page)).toHaveCount(0);
});

test('each visit sends a fresh GET, and the second shows no field until its own answer', async ({ page }) => {
  await serveMe(page, manager);
  await servePrompts(page, savedPrompts);
  const promptsRequests = countPromptsRequests(page);
  await page.goto('/manage');
  await main(page).getByRole('link', { name: 'Guide prompts', exact: true }).click();
  await expect(field(page)).toHaveValue(savedPrompts);
  await nav(page).getByRole('link', { name: 'Manage', exact: true }).click();
  await expect(heading(page)).toHaveText('Manage');

  // Another manager has saved since.
  const newer = 'What went well today?';
  const heldPrompts = await holdNext(page, '**/api/admin/guide-prompts');
  await main(page).getByRole('link', { name: 'Guide prompts', exact: true }).click();
  const prompts = await heldPrompts();
  await expect(heading(page)).toHaveText('Guide prompts');
  // The page is up, and a cached copy would have mounted the field with it.
  await expect(field(page)).toHaveCount(0);

  await prompts.fulfill(promptsAnswer(newer, 'AAAAAAAAB9I='));
  await expect(field(page)).toHaveValue(newer);
  expect(promptsRequests()).toBe(2);
});

test('an empty saved value shows an empty box of about 6 lines, with no placeholder', async ({ page }) => {
  await serveMe(page, manager);
  await servePrompts(page, '');
  await page.goto(address);

  await expect(field(page)).toHaveValue('');
  await expect(field(page)).not.toHaveAttribute('placeholder');
  // 11em of the box's 18 px text, its padding and border inside (guide-prompts.md 6b).
  expect((await boxOf(field(page))).height).toBeCloseTo(198, 0);
});

test('typing, pasting and deleting in the field send no request', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await serveMe(page, manager);
  await servePrompts(page, savedPrompts);
  await page.goto(address);
  await expect(field(page)).toHaveValue(savedPrompts);
  const sent: string[] = [];
  page.on('request', (request) => sent.push(`${request.method()} ${new URL(request.url()).pathname}`));

  await field(page).click();
  await page.keyboard.press('ControlOrMeta+End');
  await page.keyboard.type('\nHow did the day go?');
  await page.evaluate(() => navigator.clipboard.writeText('\nAny visitors?'));
  await page.keyboard.press('ControlOrMeta+V');
  await expect(field(page)).toHaveValue(`${savedPrompts}\nHow did the day go?\nAny visitors?`);
  await page.keyboard.press('Backspace');
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.press('Delete');
  await expect(field(page)).toHaveValue('');

  // Reported in the order sent, so any request that the editing sent would come before this one.
  await page.evaluate(() => fetch('/healthz'));
  expect(sent).toEqual(['GET /healthz']);
});

for (const { width, boxWidth } of [
  // The whole column inside the phone's 16 px gutters.
  { width: 375, boxWidth: 375 - 2 * 16 },
  // The note form's 40rem column.
  { width: 1280, boxWidth: 40 * 16 },
]) {
  test(`at ${width} px, the box has 18 px text, 27 px lines, 12 px padding, 2 px border, ${boxWidth} px wide`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    await serveMe(page, manager);
    await servePrompts(page, savedPrompts);
    await page.goto(address);

    await expect(field(page)).toHaveCSS('font-size', '18px');
    await expect(field(page)).toHaveCSS('line-height', '27px');
    await expect(field(page)).toHaveCSS('padding', '12px');
    await expect(field(page)).toHaveCSS('border-width', '2px');
    expect((await boxOf(field(page))).width).toBe(boxWidth);
  });
}

test('the count shows from 900 characters, red over 1,000 with the box, and its twin speaks 1 s after typing', async ({
  page,
}) => {
  // Before the page loads, so that its timers run on Playwright's clock, which runs as the real one until told.
  await page.clock.install();
  await openWith(page);
  // Paused, so that the twin's second runs only as the test runs it on.
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));

  await field(page).fill(textOf(899));
  await expect(shownCount(page)).toHaveCount(0);
  for (const [length, words] of [
    [900, 'You have 100 characters remaining'],
    [999, 'You have 1 character remaining'],
    [1_000, 'You have 0 characters remaining'],
  ] as const) {
    await field(page).fill(textOf(length));
    await expect(shownCount(page)).toHaveText(words);
    await expect(shownCount(page)).toHaveAttribute('aria-hidden', 'true');
    await expect(shownCount(page)).toHaveCSS('color', secondaryText);
    await expect(shownCount(page)).toHaveCSS('font-weight', '400');
    await expect(field(page)).toHaveCSS('border-top-color', controlBorder);
  }

  await field(page).fill(textOf(1_001));
  await expect(shownCount(page)).toHaveText('You have 1 character too many');
  await expect(shownCount(page)).toHaveCSS('color', error);
  await expect(shownCount(page)).toHaveCSS('font-weight', '700');
  // The box's border in the error colour, but not yet the error's 3 px, as no press has checked the text.
  await expect(field(page)).toHaveCSS('border-top-color', error);
  await expect(field(page)).toHaveCSS('border-top-width', '2px');
  await expect(field(page)).not.toHaveAttribute('aria-invalid');
  await expect(fieldError(page)).toHaveCount(0);

  // Five inputs with no time between them, and the twin has said nothing; 1 s after the last, it says the count.
  await expect(countTwin(page)).toBeEmpty();
  await page.clock.runFor(999);
  await expect(countTwin(page)).toBeEmpty();
  await page.clock.runFor(1);
  await expect(countTwin(page)).toHaveText('You have 1 character too many');
});

test('a 1,200-character paste is kept whole, and Save shows the error, focuses the field and sends nothing', async ({
  page,
  context,
}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await openWith(page);
  const saves = countSaves(page);
  const pasted = textOf(1_200);

  await field(page).click();
  await page.keyboard.press('ControlOrMeta+A');
  await page.evaluate((text) => navigator.clipboard.writeText(text), pasted);
  await page.keyboard.press('ControlOrMeta+V');
  await expect(field(page)).toHaveValue(pasted);
  await expect(shownCount(page)).toHaveText('You have 200 characters too many');

  await save(page).click();
  await expect(fieldError(page)).toHaveText(`Error: ${tooLong}`);
  await expect(field(page)).toBeFocused();
  await expect(field(page)).toHaveValue(pasted);
  await expect(field(page)).toHaveAttribute('aria-invalid', 'true');
  await expect(field(page)).toHaveAccessibleDescription(`${help} ${limitHint} Error: ${tooLong}`);
  await expect(field(page)).toHaveCSS('border-top-width', '3px');
  await expect(field(page)).toHaveCSS('border-top-color', error);
  await expect(fieldError(page)).toHaveCSS('color', error);
  await expect(fieldError(page)).toHaveCSS('font-weight', '700');
  // The bar down the start edge of the message, the box and the count (guide-prompts.md 6a).
  const group = main(page).locator('form [data-invalid]');
  await expect(group).toHaveCSS('border-left-width', '4px');
  await expect(group).toHaveCSS('border-left-color', error);
  await expect(page).toHaveTitle('Error: Grow2Notes – Guide prompts');
  await page.evaluate(() => fetch('/healthz'));
  expect(saves()).toBe(0);

  // Deleting down to 1,000 clears it.
  await page.keyboard.press('ControlOrMeta+End');
  for (let deleted = 0; deleted < 200; deleted += 1) {
    await page.keyboard.press('Backspace');
  }
  await expect(field(page)).toHaveValue(textOf(1_000));
  await expect(fieldError(page)).toHaveCount(0);
  await expect(field(page)).not.toHaveAttribute('aria-invalid');
  await expect(field(page)).toHaveAccessibleDescription(`${help} ${limitHint}`);
  await expect(field(page)).toHaveCSS('border-top-width', '2px');
  await expect(page).toHaveTitle('Grow2Notes – Guide prompts');
  await expect(shownCount(page)).toHaveText('You have 0 characters remaining');
});

test("1,000 characters holding 10 line breaks save, with If-Match from the GET's ETag", async ({ page }) => {
  await openWith(page);
  const sent = await serveSaves(page, (route) => route.fulfill(savedAnswer()));
  const text = textOf(1_000);
  expect(text.split('\n')).toHaveLength(11);

  await field(page).fill(text);
  await save(page).click();

  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  expect(sent).toEqual([{ text, ifMatch: `"${promptsVersion}"` }]);
  await expect(fieldError(page)).toHaveCount(0);
});

test('Save sends the text trimmed at its ends alone, and "" for spaces and line breaks alone', async ({ page }) => {
  await openWith(page);
  const sent = await serveSaves(page, (route) => route.fulfill(savedAnswer()));
  const typed = '\n  Mood today?\n\n  What did you do together?  \n\n';

  await field(page).fill(typed);
  await save(page).click();
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  // The field is not rewritten.
  await expect(field(page)).toHaveValue(typed);
  await field(page).fill('  \n\n \t ');
  await save(page).click();
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);

  expect(sent).toEqual([
    { text: 'Mood today?\n\n  What did you do together?', ifMatch: `"${promptsVersion}"` },
    { text: '', ifMatch: `"${nextVersion}"` },
  ]);
});

test('Save is never disabled, with no change or over the limit, and two quick presses send one PUT', async ({
  page,
}) => {
  await openWith(page);
  const saves = countSaves(page);
  await expect(save(page)).not.toHaveAttribute('disabled');
  await expect(save(page)).not.toHaveAttribute('aria-disabled');
  await field(page).fill(textOf(1_001));
  await expect(save(page)).not.toHaveAttribute('disabled');
  await expect(save(page)).not.toHaveAttribute('aria-disabled');
  await field(page).fill('What went well today?');
  const heldSave = await holdNext(page, '**/api/admin/guide-prompts');

  await save(page).dblclick();
  const put = await heldSave();
  await expect(save(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(save(page)).not.toHaveAttribute('disabled');
  await put.fulfill(savedAnswer());

  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await expect(save(page)).not.toHaveAttribute('aria-disabled');
  await page.evaluate(() => fetch('/healthz'));
  expect(saves()).toBe(1);
});

test('with the PUT held 2 s, Save says "Saving…" from 400 ms at the same size, and the field stays editable', async ({
  page,
}) => {
  await page.clock.install();
  await openWith(page);
  const heldSave = await holdNext(page, '**/api/admin/guide-prompts');
  const size = await boxOf(save(page));
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1_000));

  await save(page).click();
  const put = await heldSave();
  await page.clock.runFor(399);
  await expect(save(page)).toHaveAccessibleName('Save');
  await expect(save(page)).toHaveAttribute('aria-disabled', 'true');
  await expect(saveStatus(page)).toBeEmpty();
  await page.clock.runFor(1);
  await expect(save(page)).toHaveAccessibleName('Saving…');
  await expect(save(page).getByText('Saving…')).toBeVisible();
  // Said, hidden, by the save status (guide-prompts.md 6f).
  await expect(saveStatus(page)).toHaveText('Saving…');
  expect(await boxOf(save(page))).toEqual(size);

  await expect(field(page)).toBeEditable();
  await field(page).press('ControlOrMeta+End');
  await page.keyboard.type('\nAny visitors?');
  await expect(field(page)).toHaveValue(`${savedPrompts}\nAny visitors?`);
  await page.clock.runFor(1_600);
  await expect(save(page)).toHaveAccessibleName('Saving…');

  await page.clock.resume();
  await put.fulfill(savedAnswer());
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await expect(save(page)).toHaveAccessibleName('Save');
  expect(await sentSave(put)).toEqual({ text: savedPrompts, ifMatch: `"${promptsVersion}"` });
});

test('a Date of 06:12 UTC on 1 October 2026 shows "Saved 4:12 pm", focus on Save, until a keystroke', async ({
  page,
}) => {
  await openWith(page);
  await serveSaves(page, (route) => route.fulfill(savedAnswer()));
  await field(page).fill('What went well today?');

  await save(page).click();

  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await expect(saveStatus(page).locator('time')).toHaveAttribute('datetime', '2026-10-01T06:12:00.000Z');
  await expect(saveStatus(page)).toHaveCSS('color', secondaryText);
  await expect(save(page)).toBeFocused();
  await expect(requestAlert(page)).toBeEmpty();
  // Moving in the field changes nothing; a keystroke that changes the text empties it.
  await field(page).press('ControlOrMeta+End');
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await page.keyboard.type('!');
  await expect(saveStatus(page)).toBeEmpty();
});

test('two saves in a row are both announced, as the status empties at each press', async ({ page }) => {
  await openWith(page);
  await serveSaves(page, (route) => route.fulfill(savedAnswer()));
  await save(page).click();
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  const heldSave = await holdNext(page, '**/api/admin/guide-prompts');

  await save(page).click();
  const put = await heldSave();
  await expect(saveStatus(page)).toBeEmpty();
  await put.fulfill(savedAnswer('AAAAAAAAB9M='));

  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  expect(await sentSave(put)).toEqual({ text: savedPrompts, ifMatch: `"${nextVersion}"` });
});

test('typing while a save is out leaves the page not saved after its 200, and a save then leaves it saved', async ({
  page,
}) => {
  await openWith(page);
  expect(await warnsBeforeUnload(page)).toBe(false);
  await field(page).fill('What went well today?');
  expect(await warnsBeforeUnload(page)).toBe(true);
  const heldSave = await holdNext(page, '**/api/admin/guide-prompts');

  await save(page).click();
  const put = await heldSave();
  await field(page).press('ControlOrMeta+End');
  await page.keyboard.type('\nAny visitors?');
  await put.fulfill(savedAnswer());
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  expect(await warnsBeforeUnload(page)).toBe(true);

  const sent = await serveSaves(page, (route) => route.fulfill(savedAnswer('AAAAAAAAB9M=')));
  await save(page).click();
  await expect.poll(() => sent.length).toBe(1);
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  expect(sent).toEqual([{ text: 'What went well today?\nAny visitors?', ifMatch: `"${nextVersion}"` }]);
  expect(await warnsBeforeUnload(page)).toBe(false);
});

test('a save that fails says why above Save, keeps the text and focus, and sends one PUT per press', async ({
  page,
}) => {
  await openWith(page);
  const saves = countSaves(page);
  let failure: 'no connection' | 'server' = 'no connection';
  await serveSaves(page, (route) =>
    failure === 'no connection' ? route.abort('internetdisconnected') : route.fulfill({ status: 500 }),
  );
  const typed = 'What went well today?';
  await field(page).fill(typed);

  await save(page).click();
  await expect(requestAlert(page)).toHaveText('Not saved: no connection. Try again.');
  await expect(requestAlert(page)).toHaveCSS('color', error);
  await expect(save(page)).toBeFocused();
  await expect(save(page)).not.toHaveAttribute('aria-disabled');
  await expect(saveStatus(page)).toBeEmpty();
  await expect(field(page)).toHaveValue(typed);
  await page.evaluate(() => fetch('/healthz'));
  expect(saves()).toBe(1);

  failure = 'server';
  await save(page).click();
  await expect(requestAlert(page)).toHaveText('Not saved: something went wrong. Try again.');
  await expect(save(page)).toBeFocused();
  await expect(field(page)).toHaveValue(typed);
  await page.evaluate(() => fetch('/healthz'));
  expect(saves()).toBe(2);
});

test("A loads, B saves, then A's Save meets the conflict with A's text kept, and A's next Save replaces B's", async ({
  page,
  context,
}) => {
  const store = promptsStore(savedPrompts);
  const pageB = await context.newPage();
  for (const each of [page, pageB]) {
    await serveMe(each, manager);
    await store.serve(each);
  }

  await page.goto(address);
  await expect(field(page)).toHaveValue(savedPrompts);
  await pageB.goto(address);
  const theirs = 'What went well today?';
  await field(pageB).fill(theirs);
  await save(pageB).click();
  await expect(saveStatus(pageB)).toHaveText(savedAnyTime);
  expect(store.text()).toBe(theirs);
  await pageB.close();

  const mine = `${savedPrompts}\nAny visitors?`;
  await field(page).fill(mine);
  await save(page).click();
  await expect(requestAlert(page)).toHaveText(conflict);
  await expect(field(page)).toHaveValue(mine);
  await expect(save(page)).toBeFocused();
  await expect(save(page)).not.toHaveAttribute('aria-disabled');
  await expect(saveStatus(page)).toBeEmpty();
  expect(store.text()).toBe(theirs);

  await save(page).click();
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  await expect(requestAlert(page)).toBeEmpty();
  expect(store.text()).toBe(mine);
});

test('signed in for real, a manager saves prompts with line breaks and reads them back after a reload', async ({
  page,
  invite,
  authenticator,
}) => {
  await page.goto(invite.setupLink);
  await main(page).getByRole('button', { name: 'Use a passkey (recommended)' }).click();
  await expect(heading(page)).toContainText('Today');
  expect(await authenticator.credentials()).toHaveLength(1);
  await nav(page).getByRole('link', { name: 'Manage', exact: true }).click();
  await main(page).getByRole('link', { name: 'Guide prompts', exact: true }).click();
  await expect(save(page)).toBeVisible();

  await field(page).fill(savedPrompts);
  const saved = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' && new URL(response.url()).pathname === '/api/admin/guide-prompts',
  );
  await save(page).click();

  // The app's own answer, from the organisation that admin bootstrap made.
  expect((await saved).status()).toBe(200);
  await expect(saveStatus(page)).toHaveText(savedAnyTime);
  await page.reload();
  await expect(field(page)).toHaveValue(savedPrompts);
});

// The leave dialog (guide-prompts.md component 7), and the Account panel's Sign out (app-shell.md component 2d), by its
// place rather than its name, which is "Signing out…" while it is busy.
const leaveDialog = (page: Page) => page.getByRole('alertdialog', { name: 'Leave without saving?' });
const stayOnPage = (page: Page) => leaveDialog(page).getByRole('button', { name: 'Stay on this page' });
const leaveWithoutSaving = (page: Page) => leaveDialog(page).getByRole('button', { name: 'Leave without saving' });
const navLink = (page: Page, name: string) => nav(page).getByRole('link', { name, exact: true });
const account = (page: Page) => page.getByRole('button', { name: 'Account' });
const signOut = (page: Page) => page.locator('#account-panel').getByRole('button');
const signOutLine = (page: Page) => page.locator('#account-panel').getByRole('status');
// What the tests type at the end of the saved prompts, which leaves the text not saved.
const added = '\nAny visitors?';
const typed = `${savedPrompts}${added}`;
// The colour of a secondary button's fill (tokens.css).
const surface = 'rgb(255, 255, 255)';

/**
 * Types `words` at the end of the field, key by key, as a person does, which also gives the page the activation that
 * Chromium needs before its own dialog asks about leaving (MDN, `beforeunload`).
 */
async function typeAtEnd(page: Page, words: string) {
  await field(page).click();
  await page.keyboard.press('ControlOrMeta+End');
  await page.keyboard.type(words);
}

/**
 * The browser's own dialogs, such as Chromium's for a `beforeunload` that asks, which the page opens from now on, by
 * their type. Each is accepted, so that the navigation it held goes on, and a test that expects none fails on the list
 * rather than on a navigation that never ends.
 */
function browserDialogs(page: Page): string[] {
  const seen: string[] = [];
  page.on('dialog', (dialog) => {
    seen.push(dialog.type());
    void dialog.accept();
  });
  return seen;
}

/**
 * Meets each `POST /api/auth/logout` that the page sends from now on with `answer`, in the browser, so that none
 * reaches the app, and gives how many it has sent, which a test reads once it has the answer to a request of its own.
 */
async function serveLogouts(page: Page, answer: (route: Route) => Promise<void>): Promise<() => number> {
  let count = 0;
  page.on('request', (request) => {
    if (new URL(request.url()).pathname === '/api/auth/logout') {
      count += 1;
    }
  });
  await page.route('**/api/auth/logout', answer);
  return () => count;
}

/**
 * Keeps count, in each page that loads from now on, of the `beforeunload` listeners on its window, through spies on its
 * `addEventListener` and `removeEventListener`, which `beforeUnloadListeners` reads.
 */
async function watchBeforeUnload(page: Page) {
  await page.addInitScript(() => {
    const listeners = new Set<unknown>();
    const { addEventListener, removeEventListener } = window;
    Object.assign(window, {
      addEventListener: (type: string, listener: unknown, options?: unknown) => {
        if (type === 'beforeunload') {
          listeners.add(listener);
        }

        Reflect.apply(addEventListener, window, [type, listener, options]);
      },
      removeEventListener: (type: string, listener: unknown, options?: unknown) => {
        if (type === 'beforeunload') {
          listeners.delete(listener);
        }

        Reflect.apply(removeEventListener, window, [type, listener, options]);
      },
      beforeUnloadListeners: () => listeners.size,
    });
  });
}

const beforeUnloadListeners = (page: Page) =>
  page.evaluate(() => (window as unknown as { beforeUnloadListeners: () => number }).beforeUnloadListeners());

/**
 * Expects the leave dialog to have gone from the page, which it does once the page has handled its `close` event and
 * answered the navigation or Sign out that it held; a closed `<dialog>` is hidden a task before that event.
 */
const expectNoLeaveDialog = (page: Page) => expect(main(page).locator('dialog')).toHaveCount(0);

/** Closes the open leave dialog with a plain `close()`, as the shell closes any open dialog before its warning. */
const closeTheDialog = (page: Page) =>
  page.evaluate(() => document.querySelector<HTMLDialogElement>('dialog[open]')?.close());

test('with text not saved, Today opens the leave dialog on Stay, and Escape keeps the page and focus on Today', async ({
  page,
}) => {
  await openWith(page);
  await typeAtEnd(page, added);

  await navLink(page, 'Today').click();

  await expect(leaveDialog(page)).toBeVisible();
  await expect(leaveDialog(page)).toHaveAccessibleDescription('Your changes to the guide prompts are not saved.');
  await expect(leaveDialog(page).getByRole('heading', { level: 2 })).toHaveText('Leave without saving?');
  await expect(leaveDialog(page).getByRole('button')).toHaveText(['Stay on this page', 'Leave without saving']);
  await expect(stayOnPage(page)).toBeFocused();
  // Not red: a few lines of prompts are easy to type again (guide-prompts.md component 7).
  await expect(leaveWithoutSaving(page)).toHaveCSS('background-color', surface);
  await expect(page).toHaveURL(address);

  await page.keyboard.press('Escape');
  await expectNoLeaveDialog(page);
  await expect(page).toHaveURL(address);
  await expect(field(page)).toHaveValue(typed);
  await expect(navLink(page, 'Today')).toBeFocused();
});

test('with text not saved, Leave without saving goes on to Today, which then asks nothing', async ({ page }) => {
  await openWith(page);
  const dialogs = browserDialogs(page);
  await typeAtEnd(page, added);
  await navLink(page, 'Today').click();

  await leaveWithoutSaving(page).click();

  await expect(page).toHaveURL('/');
  await expect(heading(page)).toContainText('Today');
  await expectNoLeaveDialog(page);
  // The page's warning went with it.
  await page.reload();
  await expect(heading(page)).toContainText('Today');
  expect(dialogs).toEqual([]);
});

test('with the text typed and deleted back, nothing asks before a link, Back, a reload or Sign out', async ({
  page,
}) => {
  await serveMe(page, manager);
  await servePrompts(page, savedPrompts);
  const logouts = await serveLogouts(page, (route) => route.fulfill({ status: 204 }));
  const dialogs = browserDialogs(page);
  const typeAndDeleteBack = async () => {
    await expect(field(page)).toHaveValue(savedPrompts);
    await typeAtEnd(page, '!');
    await page.keyboard.press('Backspace');
    await expect(field(page)).toHaveValue(savedPrompts);
  };
  await page.goto('/manage');
  await main(page).getByRole('link', { name: 'Guide prompts', exact: true }).click();

  await typeAndDeleteBack();
  await page.goBack();
  await expect(page).toHaveURL('/manage');
  await expect(heading(page)).toHaveText('Manage');

  await page.goForward();
  await typeAndDeleteBack();
  await navLink(page, 'Today').click();
  await expect(page).toHaveURL('/');
  await expect(heading(page)).toContainText('Today');

  await page.goBack();
  await typeAndDeleteBack();
  await page.reload();
  await expect(field(page)).toHaveValue(savedPrompts);

  await typeAndDeleteBack();
  const loaded = page.waitForEvent('load');
  await account(page).click();
  await signOut(page).click();
  await loaded;
  await expect(page).toHaveURL('/');
  expect(logouts()).toBe(1);
  expect(dialogs).toEqual([]);
});

test('from Manage, with text not saved, Back asks; Escape and Stay keep the page, Leave goes back to Manage', async ({
  page,
}) => {
  await serveMe(page, manager);
  await servePrompts(page, savedPrompts);
  await page.goto('/manage');
  await main(page).getByRole('link', { name: 'Guide prompts', exact: true }).click();
  await typeAtEnd(page, added);

  await page.goBack();
  await expect(leaveDialog(page)).toBeVisible();
  await expect(stayOnPage(page)).toBeFocused();
  await page.keyboard.press('Escape');
  await expectNoLeaveDialog(page);
  await expect(page).toHaveURL(address);
  await expect(field(page)).toHaveValue(typed);

  await page.goBack();
  await expect(stayOnPage(page)).toBeFocused();
  await stayOnPage(page).click();
  await expectNoLeaveDialog(page);
  await expect(page).toHaveURL(address);
  await expect(field(page)).toHaveValue(typed);

  await page.goBack();
  await leaveWithoutSaving(page).click();
  await expect(page).toHaveURL('/manage');
  await expect(heading(page)).toHaveText('Manage');
});

test('a click on the backdrop leaves the leave dialog open and the address as it was', async ({ page }) => {
  await openWith(page);
  await typeAtEnd(page, added);
  await navLink(page, 'Today').click();
  await expect(leaveDialog(page)).toBeVisible();
  const dialogBox = await boxOf(leaveDialog(page));

  // Above the dialog, on its backdrop.
  await page.mouse.click(dialogBox.x + dialogBox.width / 2, dialogBox.y / 2);

  await expect(leaveDialog(page)).toBeVisible();
  await expect(page).toHaveURL(address);
  await stayOnPage(page).click();
  await expectNoLeaveDialog(page);
  await expect(field(page)).toHaveValue(typed);
});

test("with text not saved, a reload meets Chromium's own dialog, and after a Save it meets none", async ({ page }) => {
  await openWith(page);
  await serveSaves(page, (route) => route.fulfill(savedAnswer()));
  const dialogs = browserDialogs(page);
  await typeAtEnd(page, added);

  await page.reload();
  expect(dialogs).toEqual(['beforeunload']);
  // Accepted, so the page has loaded again, with the prompts as saved.
  await expect(field(page)).toHaveValue(savedPrompts);

  await typeAtEnd(page, added);
  await save(page).click();
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await page.reload();
  await expect(field(page)).toHaveValue(savedPrompts);
  expect(dialogs).toEqual(['beforeunload']);
});

test('the page listens for beforeunload only while its text is not saved', async ({ page }) => {
  await watchBeforeUnload(page);
  await openWith(page);
  await serveSaves(page, (route) => route.fulfill(savedAnswer()));
  expect(await beforeUnloadListeners(page)).toBe(0);

  await typeAtEnd(page, '!');
  await expect.poll(() => beforeUnloadListeners(page)).toBe(1);
  await page.keyboard.press('Backspace');
  await expect.poll(() => beforeUnloadListeners(page)).toBe(0);

  await page.keyboard.type('!');
  await expect.poll(() => beforeUnloadListeners(page)).toBe(1);
  await save(page).click();
  await expect(saveStatus(page)).toHaveText(savedAtFourTwelve);
  await expect.poll(() => beforeUnloadListeners(page)).toBe(0);

  await typeAtEnd(page, '?');
  await expect.poll(() => beforeUnloadListeners(page)).toBe(1);
  await navLink(page, 'Today').click();
  await leaveWithoutSaving(page).click();
  await expect(page).toHaveURL('/');
  await expect.poll(() => beforeUnloadListeners(page)).toBe(0);
});

test('with text not saved, Sign out asks first: Stay sends no logout, Leave signs out with no browser dialog', async ({
  page,
}) => {
  await openWith(page);
  const logouts = await serveLogouts(page, (route) => route.fulfill({ status: 204 }));
  const dialogs = browserDialogs(page);
  await typeAtEnd(page, added);
  await account(page).click();

  await signOut(page).click();
  await expect(leaveDialog(page)).toBeVisible();
  await expect(stayOnPage(page)).toBeFocused();
  await page.evaluate(() => fetch('/healthz'));
  expect(logouts()).toBe(0);

  await stayOnPage(page).click();
  await expectNoLeaveDialog(page);
  await expect(signOut(page)).toBeFocused();
  await expect(signOut(page)).toHaveAccessibleName('Sign out');
  await expect(signOut(page)).not.toHaveAttribute('aria-disabled');
  await expect(field(page)).toHaveValue(typed);
  await page.evaluate(() => fetch('/healthz'));
  expect(logouts()).toBe(0);

  // Gone once / has loaded again, as the new page starts with nothing in memory.
  await page.evaluate(() => Reflect.set(window, 'beforeSignOut', true));
  const loaded = page.waitForEvent('load');
  await signOut(page).click();
  await leaveWithoutSaving(page).click();
  await loaded;
  await expect(page).toHaveURL('/');
  expect(await page.evaluate(() => Reflect.has(window, 'beforeSignOut'))).toBe(false);
  expect(logouts()).toBe(1);
  expect(dialogs).toEqual([]);
});

test('a Sign out whose logout fails leaves the page guarded, by the leave dialog and by the browser', async ({
  page,
}) => {
  await openWith(page);
  await serveLogouts(page, (route) => route.abort('internetdisconnected'));
  const dialogs = browserDialogs(page);
  await typeAtEnd(page, added);
  await account(page).click();
  await signOut(page).click();

  await leaveWithoutSaving(page).click();

  await expect(signOutLine(page)).toHaveText('Not signed out: no connection. Try again.');
  await expect(field(page)).toHaveValue(typed);
  await navLink(page, 'Today').click();
  await expect(leaveDialog(page)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page).toHaveURL(address);
  await page.reload();
  expect(dialogs).toEqual(['beforeunload']);
});

test("a plain close() of the leave dialog, as the shell's warning gives it, is Stay for a link and Sign out", async ({
  page,
}) => {
  await openWith(page);
  const logouts = await serveLogouts(page, (route) => route.fulfill({ status: 204 }));
  await typeAtEnd(page, added);

  await navLink(page, 'Today').click();
  await expect(leaveDialog(page)).toBeVisible();
  await closeTheDialog(page);
  await expectNoLeaveDialog(page);
  await expect(page).toHaveURL(address);
  // Not left blocked: the next link asks again.
  await navLink(page, 'Manage').click();
  await expect(leaveDialog(page)).toBeVisible();
  await closeTheDialog(page);
  await expectNoLeaveDialog(page);

  await account(page).click();
  await signOut(page).click();
  await expect(leaveDialog(page)).toBeVisible();
  await closeTheDialog(page);
  await expectNoLeaveDialog(page);
  await expect(signOut(page)).toBeFocused();
  await expect(signOut(page)).not.toHaveAttribute('aria-disabled');
  await page.evaluate(() => fetch('/healthz'));
  expect(logouts()).toBe(0);
  await expect(page).toHaveURL(address);
  await expect(field(page)).toHaveValue(typed);
});

// S01.04.02's own criterion: design.md asks for a leave warning on Guide prompts alone.
for (const { path, name, link } of [
  { path: '/', name: 'Today', link: 'Manage' },
  { path: '/manage', name: 'Manage', link: 'Today' },
  { path: '/manage/users', name: 'Users', link: 'Today' },
]) {
  test(`${name} asks nothing before a link, a reload or Sign out`, async ({ page }) => {
    await watchBeforeUnload(page);
    await serveMe(page, manager);
    await serveUsers(page, people);
    const logouts = await serveLogouts(page, (route) => route.fulfill({ status: 204 }));
    const dialogs = browserDialogs(page);
    await page.goto(path);
    // A press on the page, which gives it the activation that Chromium needs before it asks about leaving.
    await heading(page).click();
    expect(await beforeUnloadListeners(page)).toBe(0);

    await navLink(page, link).click();
    await expect(page).not.toHaveURL(path);
    await expectNoLeaveDialog(page);
    await page.goBack();
    await expect(heading(page)).toContainText(name);

    await heading(page).click();
    await page.reload();
    await expect(heading(page)).toContainText(name);

    await heading(page).click();
    const loaded = page.waitForEvent('load');
    await account(page).click();
    await signOut(page).click();
    await loaded;
    expect(logouts()).toBe(1);
    expect(dialogs).toEqual([]);
  });
}
