import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests (T029; tech discussion TE5): a real browser against the whole platform started by Aspire on the
 * flowza_test_* databases with demo data. Start them with run-e2e.ps1 (it prepares and starts everything; see its
 * header); `npm test` alone expects that platform to be running already (run-e2e.ps1 -KeepRunning).
 */
export default defineConfig({
  testDir: './specs',
  // One platform and one database for all tests: run one test at a time, in file order.
  workers: 1,
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  // In CI the github reporter turns each failure into an annotation on the run (readable without the full log).
  reporter: process.env.CI
    ? [['list'], ['github'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.FLOWZA_E2E_BASE_URL ?? 'http://localhost:5100',
    locale: 'en-US',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
