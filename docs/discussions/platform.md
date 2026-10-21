# Module discussion: PLT (Platform)

| | |
|---|---|
| **Status** | Decided by the owner (PL1–PL7, Q-P1–Q-P6), 2026-10-21 |
| **Feeds** | `/speckit-specify 001-platform-foundation` (sections 1–5) and `/speckit-plan` (section 6) |
| **Scope IDs** | PLT-01 to PLT-09 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) |
| **Related** | [IAM](identity-and-access.md) (password and session settings), [NTF](notifications.md) (SMTP, branding in emails), [EXP](expression-language.md) (time zone), [FRM](form-builder.md) (currency, file storage) |
| **Owning services** | All; business settings owned by Identity |

## What this file is
The result of the detailed discussion of the PLT module. Sections 1–5 are **what and why**, the input for
`spec.md`. Section 6 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
Everything that is not a business feature but makes the product **run, deploy and stay healthy**: starting
it, databases and messaging, settings, logs and health, demo data and the setup guide. On-premises customers
judge a product first by **how easily it installs and runs**.

## 2. Already decided (PLT-01 to PLT-07, CLAUDE.md)
- Five services plus the web app; **one command** starts everything (Aspire), **without Docker**.
- A database per service, with migrations; Wolverine over SQL Server with an outbox (`flowza_messaging` for messages only).
- Health checks, structured logs, correlation ID across services, **no personal data in logs**.
- A README that sets up a fresh machine in under 30 minutes; a demo-data script.

### Developer PC at run time
```
dotnet run --project src/AppHost
   ├─ Gateway        https://localhost:5001  ← the only address the browser uses
   ├─ Identity / Design / Runtime / Worker   (internal ports)
   ├─ Web (Vite dev server)
   ├─ smtp4dev       http://localhost:5000   ← every email sent
   └─ Aspire dashboard                      ← logs, traces, health
        SQL Server: the local instance (no container)
```

## 3. Owner's decisions
| # | Question | Decision |
|---|---|---|
| PL1 | Installation settings | **Technical** settings (connection strings, SMTP, file storage path, secrets) in configuration files and environment variables. **Business** settings (installation name and logo, time zone, currency, locale, password policy, session timeouts) on an admin **Settings screen** |
| PL2 | Branding | Installation **name and logo** in v0.1, on the Settings screen; used in the app header and emails (NTF N1) |
| PL3 | Demo data | A **command-line tool** that loads a sample org tree, demo users and the RS-1 and RS-2 apps; it **refuses to run on a database that already has data** |
| PL4 | Database changes in production | Development: services update their own databases at startup. Production: **SQL scripts** for the customer's DBA to review and run |
| PL5 | API documentation viewer | **Scalar** (MIT licence), **approved** under constitution 1.6; shown **only in development** |
| PL6 | Logs | Development: the Aspire dashboard. Production: **rolling log files**, plus optional export to the customer's monitoring tool (OpenTelemetry) |
| PL7 | CI | A **private GitHub repository** with GitHub Actions (build, all tests, 80% coverage gate), set up on the first build day. The owner decides when anything is pushed |

## 4. Details accepted by the owner (Q-P1–Q-P6)
| # | Detail |
|---|---|
| Q-P1 | Each service has `/health/live` (running) and `/health/ready` (database and message transport reachable) |
| Q-P2 | The correlation ID travels in the W3C `traceparent` header through the Gateway, every HTTP call and every message. Error screens show a short **reference code** that support can find in the logs |
| Q-P3 | Business settings are owned by the **Identity service**; other services receive changes through a `PlatformSettingsChanged` event and keep a copy |
| Q-P4 | The README covers prerequisites, one-time setup (SQL Server, smtp4dev, trusting the dev certificate), the start command, demo data and common problems; target under 30 minutes |
| Q-P5 | Backups are the customer's job; the README lists which databases to back up |
| Q-P6 | Every library is checked against the licensing rule; all chosen so far are MIT or Apache 2.0 |

## 5. Acceptance criteria (draft for spec.md)
**PLT-02 One command**
- PLT-02-AC1: On a Windows PC without Docker, one command starts all services, the web app and smtp4dev, and the app opens through the Gateway.

**PLT-03 Databases**
- PLT-03-AC1: On first start in development, each service creates its own database; restarting changes nothing.
- PLT-03-AC2: A production SQL script can be generated per service and run twice safely (PL4).

**PLT-04 Messaging**
- PLT-04-AC1: An event published in a transaction that rolls back is never delivered; an event from a committed transaction is delivered even if the receiver was down at the time (NFR-03).

**PLT-05 Health and logs**
- PLT-05-AC1: `/health/ready` reports unhealthy when the service's database is unreachable (Q-P1).
- PLT-05-AC2: One user action that crosses several services shows one correlation ID in all their logs (Q-P2).
- PLT-05-AC3: Logs never contain form values or personal data (NFR-07); an automated test scans log output for test values.

**PLT-06 README**
- PLT-06-AC1 (manual checklist, constitution 1.4): a fresh machine is running in under 30 minutes by following the README.

**PLT-07 Demo data**
- PLT-07-AC1: The tool loads the demo org, users and both reference apps into empty databases.
- PLT-07-AC2: The tool refuses to run when any service database already has data (PL3).

**PLT-08 Settings and branding (new scope ID)**
- PLT-08-AC1: An admin changes the installation name, logo, time zone, currency, locale, password policy and session timeouts on the Settings screen; all services use the new values without a restart (Q-P3).

**PLT-09 CI (new scope ID)**
- PLT-09-AC1: Every push runs the build, all automated tests and the coverage gate; a failure blocks the change (PL7).

## 6. Design notes (input for plan.md, not for spec.md)
- **Aspire without Docker:** the AppHost uses the local SQL Server through a connection string (no container);
  smtp4dev runs as an executable resource; Vite as a JavaScript app resource.
- **Migrations:** EF Core migrations per service; production scripts via `dotnet ef migrations script --idempotent` (safe to run twice).
- **Wolverine:** SQL Server transport and durable outbox in `flowza_messaging`; retries and a dead-letter queue.
  An admin view of dead letters comes later (v0.2).
- **Settings:** stored in Identity's database; other services keep an in-memory copy refreshed by `PlatformSettingsChanged`.
- **Logos** stored through `IFileStorage` (same as uploads).
- **Libraries checked so far:** Wolverine (MIT), Aspire (MIT), YARP (MIT), OpenIddict (Apache 2.0), Fluid (MIT),
  MailKit (MIT), Serilog (Apache 2.0), Scalar (MIT), Ant Design (MIT), React Flow core (MIT; the paid "Pro"
  add-ons are not used), dnd-kit (MIT), decimal.js (MIT), orval (MIT).
