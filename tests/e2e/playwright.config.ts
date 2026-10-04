import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  // A test.only left in by mistake would otherwise skip every other test and still pass the run.
  forbidOnly: !!process.env.CI,
  // No retries: the app runs on the same machine as the browser, so a test that passes only on a retry is flaky and
  // should fail. Without retries, a trace is kept for every failed test instead of only for retried ones.
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    // CI sets E2E_BASE_URL to where it runs the published app; the default is where tests/README.md starts it.
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5000',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
