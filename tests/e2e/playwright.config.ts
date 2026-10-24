import { defineConfig, devices } from '@playwright/test';

const saveSpecs = ['report-download.spec.ts', 'record-export-download.spec.ts'];

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
    // The headers that every request sends are fixtures.ts's clientHeaders(), set there for each test and each device.
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    // A report download's save step is the one thing here that differs between engines (daily-report.md,
    // record-export.md), so only the specs that check it, fully mocked, run in these two.
    { name: 'firefox', use: { ...devices['Desktop Firefox'] }, testMatch: saveSpecs },
    { name: 'webkit', use: { ...devices['Desktop Safari'] }, testMatch: saveSpecs },
  ],
});
