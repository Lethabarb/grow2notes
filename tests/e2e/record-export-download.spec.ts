import { expect, test } from './fixtures.ts';
import { manager, serveMe } from './me.ts';
import {
  defaultQuery,
  exportAddress,
  exportDay,
  firstNoteDate,
  recordExports,
  serveParticipant,
  serveRecord,
} from './recordExport.ts';
import { serveOverHttp } from './report.ts';

// The Export record screen's two exports in Chromium, Firefox and WebKit (record-export.md, file-download.md *Save
// step*): the save step is the part that differs between engines. The participant and the export are answered in the
// browser with made-up bytes (recordExport.ts), so that no session is needed; the real files are the server's tests'.

for (const { choice, format } of [
  { choice: 'Word', format: 'docx' },
  { choice: 'PDF', format: 'pdf' },
] as const) {
  const name = `participant-record_${firstNoteDate}_to_${exportDay}.${format}`;
  test(`Export as ${choice} saves ${name}, asking for ${defaultQuery(format)}, with no new page`, async ({
    page,
    context,
  }) => {
    await serveMe(page, manager);
    await serveParticipant(page);
    await serveOverHttp(page, exportAddress);
    await serveRecord(page);
    const sent = recordExports(page);
    let pagesOpened = 0;
    context.on('page', () => {
      pagesOpened += 1;
    });
    await page.goto(exportAddress);
    const main = page.getByRole('main');
    await expect(main.getByRole('heading', { level: 1 })).toHaveText('Export record');

    await main.getByRole('radio', { name: choice, exact: true }).check();
    const download = page.waitForEvent('download');
    await main.getByRole('button', { name: 'Export', exact: true }).click();

    expect((await download).suggestedFilename()).toBe(name);
    expect(sent).toEqual([defaultQuery(format)]);
    expect(pagesOpened).toBe(0);
    expect(context.pages()).toHaveLength(1);
    expect(new URL(page.url()).pathname).toBe(exportAddress);
  });
}
