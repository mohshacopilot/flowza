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
  // Screenshot comparisons (spec 007 UI-10, plan 007 §10, PA5): baselines come from CI's Linux runner; locally they run
  // only with FLOWZA_E2E_SCREENSHOTS=1. 1 % of pixels may differ (anti-aliasing); animations and the caret are stopped.
  expect: {
    timeout: 10_000,
    toHaveScreenshot: { maxDiffPixelRatio: 0.01, animations: 'disabled', caret: 'hide' },
  },
  projects: [
    // The behaviour suites (feature 001) once, in a desktop browser.
    { name: 'chromium', use: { ...devices['Desktop Chrome'] }, testIgnore: /07-visual-design/ },
    // The visual design (feature 007) at the three widths of plan 007 §5.5: desktop, tablet (side panel collapsed by
    // default, C4) and phone (menu button and drawer, UI-05).
    {
      name: 'desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } },
      testMatch: /07-visual-design/,
    },
    {
      name: 'tablet',
      use: { ...devices['Desktop Chrome'], viewport: { width: 900, height: 1100 } },
      testMatch: /07-visual-design/,
    },
    {
      name: 'phone',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 375, height: 812 },
        isMobile: true,
        hasTouch: true,
        deviceScaleFactor: 1,
      },
      testMatch: /07-visual-design/,
    },
  ],
});
