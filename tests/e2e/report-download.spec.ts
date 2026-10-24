import { expect, test } from './fixtures.ts';
import { manager, serveMe } from './me.ts';
import {
  recordExports,
  reportAddress,
  reportDay,
  serveHasNotes,
  serveOverHttp,
  serveReport,
} from './report.ts';

// The Report screen's two downloads in Chromium, Firefox and WebKit (daily-report.md, file-download.md *Save step*):
// the save step is the part that differs between engines. The export is answered in the browser with made-up bytes
// (report.ts), so that no session is needed; the real files are the server's tests'.

for (const { button, format } of [
  { button: 'Download Word', format: 'docx' },
  { button: 'Download PDF', format: 'pdf' },
]) {
  test(`${button} saves daily-notes_${reportDay}.${format}, asking for ?format=${format}, with no new page`, async ({
    page,
    context,
  }) => {
    await serveMe(page, manager);
    await serveHasNotes(page);
    await serveOverHttp(page);
    await serveReport(page);
    const sent = recordExports(page);
    let pagesOpened = 0;
    context.on('page', () => {
      pagesOpened += 1;
    });
    await page.goto(reportAddress);

    const download = page.waitForEvent('download');
    await page.getByRole('main').getByRole('button', { name: button, exact: true }).click();

    expect((await download).suggestedFilename()).toBe(`daily-notes_${reportDay}.${format}`);
    expect(sent).toEqual([`?format=${format}`]);
    expect(pagesOpened).toBe(0);
    expect(context.pages()).toHaveLength(1);
    expect(new URL(page.url()).pathname).toBe(reportAddress);
  });
}
