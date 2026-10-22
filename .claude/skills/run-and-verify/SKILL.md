---
name: run-and-verify
description: Start the whole Flowza platform (Aspire AppHost, no containers), check that every service reports ready (database, message transport, broker), run the full automated test suite, and report the results in a standard way. Use at the end of a phase, before merging a parallel stream, or whenever the owner asks whether everything works.
---

# Run and verify Flowza

## Preconditions (owner's one-time setup, see `docs/setup/local-setup.md`)
- SQL Server with the 10 `flowza_*` databases. No RabbitMQ on the development PC (owner, 2026-10-22): messages go
  through SQL Server (`flowza_messaging`).
- Passwords in the `flowza-dev` user-secrets store (`docs/setup/set-secrets.ps1`); smtp4dev installed.
- If a check fails because a password is missing, ask the owner to run `docs/setup/set-secrets.ps1`.
  **Never ask for, read or print passwords.**

## 1. Start the platform and check readiness
```powershell
powershell -ExecutionPolicy Bypass -File .claude\skills\run-and-verify\scripts\check-platform.ps1
```
- It builds, starts the AppHost, polls `/health/ready` of Identity (5101), Design (5102), Runtime (5103),
  Worker (5104) and Gateway (5100), prints each service's checks, stops everything and exits 0 only if all are ready.
- `-KeepRunning` leaves it running (e.g. for the owner to look at the Aspire dashboard or the mailbox at
  http://localhost:5000).
- If a service is not ready, read the end of the AppHost log the script names, find the cause, and report it.

## 2. Run the full test suite
```powershell
dotnet test --solution Flowza.slnx
```
Report the totals (passed / failed / skipped) and every failed test by name.
- The Azure Service Bus test is **skipped** unless `FLOWZA_ConnectionStrings__azure-servicebus` is set (ADR-0005).
- The RabbitMQ transport tests are **skipped** unless `RabbitMq:Password` is set (user secrets or
  `FLOWZA_RabbitMq__Password`). RabbitMQ is dropped (on-premises = SQL Server, cloud = Azure Service Bus; ADR-0005).
- Test projects that have no tests yet report "Zero tests ran" (exit code 8); list them as expected, not as failures,
  while their phase has not started.
- When the web app exists: also run `npm run lint`, `npm run typecheck` and `npm test` in `src/Web`.

## 3. Report (plain words, for the owner)
| Item | Result |
|---|---|
| Build | 0 warnings / errors? |
| Platform start | each service ready, with its transport |
| Tests | passed / failed / skipped, and which test projects are still empty |
| Problems | each with its cause and the proposed fix |

Never claim something works that was not checked; say clearly what was not verified.
