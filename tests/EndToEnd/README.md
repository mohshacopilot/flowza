# Flowza end-to-end tests

Playwright (TypeScript) in a real browser against the **whole platform** started by Aspire, on the
`flowza_test_*` databases with the demo data (task T029, tech discussion TE5). Every screen the tests visit is also
checked with axe for WCAG 2.1 A/AA problems (NFR-11).

## Run them

Once per PC (Node.js 24 is already needed for the web app):

```powershell
cd tests\EndToEnd
npm ci
npx playwright install chromium
```

Then, from the repository folder, **with your development AppHost stopped** (the run uses the same ports):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\EndToEnd\run-e2e.ps1
```

The script builds the solution, empties the `flowza_test_*` databases (never the development ones), starts the
AppHost on them, loads the demo users with `flowza demo-data`, runs the tests and stops the platform. About
3 minutes. Options: `-NoBuild`, `-KeepRunning` (then `npx playwright test` here re-runs the tests quickly) and
`-PlaywrightArgs '--headed'` or a single file (`-PlaywrightArgs 'specs/03-lockout.spec.ts'`).

The integration tests use the same `flowza_test_*` databases: do not run both at the same time.

A failed test leaves a screenshot and a trace in `test-results/`; open the report with `npx playwright show-report`.

## How they are written

- One file per area in `specs/`, test names carry the spec IDs (`IAM_07_AC1_…`).
- Each test creates its own users with unique names (`e2e.<purpose>.<stamp>`), so tests never depend on each other
  and a re-run never collides with an earlier one (users are never deleted, C6).
- Shared steps (log in, create a user, the forced password change, the axe check) are in `support/flowza.ts`.
- Screens are found the way people find them: by role and visible name (`getByRole('button', { name: … })`).
