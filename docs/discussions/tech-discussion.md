# Tech discussion: how we build it

| | |
|---|---|
| **Status** | In progress (started 2026-10-21) |
| **Why** | The module discussions decided **what**; this one decides **how**, topic by topic, before any `plan.md` is written |
| **Feeds** | Every feature's `plan.md`; big, long-lasting choices also become ADRs in `docs/decisions/` |

## Topics
| # | Topic | Needed for | Status |
|---|---|---|---|
| T1 | Solution and repository structure, naming, IDs, way of working with git | 001 | ✅ Decided (code name **Flowza**) |
| T2 | Inside a service: layers, request handling, validation, errors | 001 | ⬜ |
| T3 | Data access: EF Core, migrations, concurrency, soft delete, JSON, time | 001 | ⬜ |
| T4 | Messaging: Wolverine, outbox, idempotency, event contracts and versions | 001 | ⏳ Transports decided ([ADR-0004](../decisions/0004-messaging-transports.md)); CQRS/Saga decided (AR1) |
| T5 | Security: login flow, Gateway as BFF, tokens, service-to-service calls, authorization | 001 | ⬜ |
| T6 | Gateway: routing, rate limiting, serving the web app | 001 | ⬜ |
| T7 | Observability: logs, traces, correlation, health, privacy guard | 001 | ⬜ |
| T8 | Testing: unit, integration on real SQL Server without Docker, end-to-end, coverage | 001 | ⬜ |
| T9 | Local development and CI: Aspire, SQL Server instance, smtp4dev, GitHub Actions | 001 | ⬜ |
| T10 | Frontend architecture: structure, routing, state, theming, translations, API client, accessibility | 001 (shell), 004 | ⬜ |
| T11 | Expression engine packaging (C# + TypeScript) and conformance tests | 003 | ⬜ |
| T12 | Designers: form designer (dnd-kit) and process designer (React Flow) | 004, 005 | ⬜ |
| T13 | Deployment: on-premises (Windows/IIS or services) and Azure | v1.0, decided early | ⬜ |

## Owner's inputs (2026-10-21)
- **IDE:** the solution must open, build, run and debug in **Visual Studio 2026** (start the AppHost with F5).
  The `.slnx` format is supported there.
- **SQL Server:** the owner provides the server name and login. **Secrets never go into git or chat**
  (constitution 7.6): the password is stored on the developer PC with .NET user secrets (or Windows
  authentication is used, which needs no password). The login needs permission to **create databases**
  (or a DBA creates them first), because integration tests create and drop their own test databases.
- **Databases (v0.1):** 5 per installation, all on the same SQL Server instance:
  | Database | Owner | Holds |
  |---|---|---|
  | `flowza_identity` | Identity | users, roles, org tree, business roles, settings |
  | `flowza_design` | Design | apps, versions, form and process definitions |
  | `flowza_runtime` | Runtime | requests, form data, tasks, history, copies of definitions, file metadata |
  | `flowza_worker` | Worker | email delivery log |
  | `flowza_messaging` | (transport) | message queues only, no business data |

  **Development server chosen by the owner:** the default instance on the office PC (`.`), **SQL Server 2025
  Express** (17.0.1000.7). Fine for development: Express limits each database to 10 GB and uses limited
  memory and CPU, which is enough for v0.1 and the NFR-02 test data. All databases use compatibility
  level 150 (SQL Server 2019) so nothing 2025-only is used (constitution 6.3).

  Later: `flowza_datalist` (ADR-0002) and `flowza_audit` (ADR-0001). Gateway has no database.
- **SQL login and databases (owner, 2026-10-21):** the owner uses a **SQL Server login** (password stored only
  with .NET user secrets on the PC) and **creates the databases manually**. The login needs `db_owner` on each
  of them so migrations and Wolverine can create their tables.
- **Test databases (TD1 = a):** the owner also creates **5 test databases** once: `flowza_test_identity`,
  `flowza_test_design`, `flowza_test_runtime`, `flowza_test_worker`, `flowza_test_messaging`. Integration tests
  clear their tables before each run and never touch the development databases.
  Script for all 10 databases: [docs/setup/create-databases.sql](../setup/create-databases.sql).
- **Messaging (decided, MQ1/MQ2):** **RabbitMQ and SQL Server transports both in v0.1**, chosen by configuration;
  RabbitMQ is the default on the development PC; Azure Service Bus later. The outbox is always on. The owner
  installs **Erlang + RabbitMQ** on the office PC. Recorded in [ADR-0004](../decisions/0004-messaging-transports.md).
  Redis is not used as a broker.

## Architecture style reconfirmed (2026-10-21)
The owner asked whether microservices are really needed. Claude laid out the options: (a) keep microservices,
(b) a modular monolith built to split later, (c) a 3-deployable hybrid, with costs (about 3–5 extra build
days, more for on-premises IT to run) and benefits. **The owner chose (a): continue with microservices.**
This is final; do not re-open it unless the owner asks.

## Architecture patterns: CQRS and Saga (AR1 = a, decided 2026-10-21)
**Decision:** both, each where it fits; no event sourcing; no distributed transactions.
- **CQRS-lite in every service (M1):** commands (change state through the domain model, validated, one
  transaction, publish events via the outbox) are separate from queries (read-only, shaped for screens,
  EF Core no-tracking or Dapper). Same database for both in v0.1.
- **Full CQRS at system level (later):** the Datalist read copy and reporting tables (ADR-0002, ADR-0003),
  fed by events.
- **Process manager / saga:**
  - The **workflow engine itself** is the domain-level process manager for a request. Its state lives in
    Runtime's database, so one transaction covers each step; no distributed saga is needed for the
    request flow.
  - **Wolverine sagas** are used for technical multi-step jobs that span messages and time: bulk restart
    (APP-05, M3) first; later SLA timers and escalation (v0.3) and connector calls with retries and
    compensation (v0.3).
- **Choreography** (services react to events) for simple cases such as `AppVersionPublished` → Runtime
  stores its copy, or `SendEmailRequested` → Worker sends.

## T1: Solution and repository structure
**Decided 2026-10-21:** TS2–TS8 all "a". **TS1: code name `Flowza`** (namespaces `Flowza.<Service>.<Layer>`,
solution `Flowza.slnx`, databases `flowza_<service>`). TS7 means Claude commits locally after each finished task and
never pushes without the owner's OK; the first commit holds all documents written so far.

| # | Question | Options (⭐ = Claude's recommendation) |
|---|---|---|
| TS1 | Product code name used in namespaces and package names | **a) Owner chooses a short name now ⭐** (renaming code later is painful) · b) Keep `Workflow` |
| TS2 | Solution file | **a) One solution for everything (`.slnx`, the new .NET format) ⭐** · b) One per service |
| TS3 | Projects per service | **a) Four layers (Api, Application, Domain, Infrastructure) for Identity, Design, Runtime; Gateway and Worker as single thin projects ⭐** · b) Four layers everywhere |
| TS4 | Package versions | **a) Central Package Management: all versions in one file ⭐** · b) Versions in each project |
| TS5 | IDs of records | **a) GUID version 7 (time-ordered, created in code, good for indexes and events) ⭐** · b) Database number (bigint identity) · c) Random GUID |
| TS6 | Code-quality rules | **a) .NET built-in analyzers at "recommended" level + `.editorconfig`, warnings as errors; ESLint + Prettier for TypeScript ⭐** · b) Add StyleCop/SonarAnalyzer too |
| TS7 | Git way of working | **a) Claude commits locally after each finished task (Conventional Commits with spec IDs); the owner reviews; **nothing is pushed without the owner's OK**. Short task branches merged to `main` ⭐** · b) Claude never commits; the owner commits |
| TS8 | Web app | **a) One React app for all screens (designers, inbox, admin), split by feature folders and lazy-loaded routes ⭐** · b) Separate apps for designers and runtime |
