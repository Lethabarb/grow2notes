import { playwright } from '@vitest/browser-playwright';
import { defineConfig } from 'vitest/config';

// The format tests again, in Playwright's WebKit, Safari's engine: time and dateTime read Melbourne's time from the
// browser's own time-zone data, which can differ from Node's (microcopy.md §8). The other tests stay in jsdom only.
export default defineConfig({
  test: {
    include: ['src/copy/format.test.ts'],
    browser: {
      enabled: true,
      // A zone behind UTC, where a calendar date read in local time falls on the day before; CI's jsdom run is in
      // UTC, where it does not.
      provider: playwright({ contextOptions: { timezoneId: 'America/Los_Angeles' } }),
      headless: true,
      instances: [{ browser: 'webkit' }],
      // The tests render nothing, so a screenshot of a failure would show an empty page.
      screenshotFailures: false,
    },
  },
});
