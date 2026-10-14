import type { Page } from '@playwright/test';
import { expect, test } from './fixtures.ts';
import { failPrompts, promptsAnswer, savedPrompts, servePrompts } from './guidePrompts.ts';
import { boxOf } from './layout.ts';
import { manager, serveMe, worker } from './me.ts';
import { holdNext } from './requests.ts';

// Manage > Guide prompts in the published app (S01.04.01), against guide-prompts.md's criteria for access and
// structure, loading and the field. The tests answer /api/auth/me and /api/admin/guide-prompts in the browser
// (me.ts, guidePrompts.ts), so that neither request reaches the app.

const address = '/manage/guide-prompts';
const help = 'Prompts are a guide only. They disappear as soon as someone starts typing and are not saved into notes.';
const limitHint = 'You can enter up to 1,000 characters';

const main = (page: Page) => page.getByRole('main');
const heading = (page: Page) => main(page).getByRole('heading', { level: 1 });
const field = (page: Page) => main(page).getByRole('textbox', { name: 'Guide prompts', exact: true });
const save = (page: Page) => main(page).getByRole('button', { name: 'Save', exact: true });
const nav = (page: Page) => page.getByRole('navigation', { name: 'Main' });
// The region's line, beside the page's visually hidden status region (guide-prompts.md *Live regions*).
const statusLine = (page: Page) => main(page).getByRole('status').and(page.locator(':not(.visually-hidden)'));
const tryAgain = (page: Page) => main(page).getByRole('button', { name: 'Try again' });
// The nav items marked current, Manage alone (guide-prompts.md component 1).
const marked = (page: Page) => page.getByRole('banner').locator('[aria-current]');

/** Whether the page's focus is on `<body>`, where it stays when nothing has moved it. */
const focusOnBody = (page: Page) => page.evaluate(() => document.activeElement === document.body);

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
