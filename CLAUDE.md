# Workflow Platform — instructions for Claude

**Code name: Flowza** (namespaces `Flowza.<Service>.<Layer>`, solution `Flowza.slnx`, databases `flowza_<service>`).
A generic, Joget-like low-code platform: **form builder + workflow engine + organization hierarchy**.
It must fit any organization (bank/NBFC loans, HR leave, healthcare approvals, …). Business processes
are built **by configuration**, never coded into the platform.

## Read these first
0. **[docs/discussions/discussion.md](docs/discussions/discussion.md): progress log of the module discussions and what to do next (start here in every new session)**
1. [docs/constitution.md](docs/constitution.md): the rules every change must follow
2. [specs/v0.1-scope.md](specs/v0.1-scope.md): what we are building now, and what we are not
3. The feature spec in `specs/<feature>/spec.md` (e.g. `specs/001-platform-foundation/spec.md`) for the feature you are working on

## How we work: spec-driven development (SDD)
The project owner is **learning SDD** on this project. Teach while building: explain why each step exists.

1. **Specify:** the owner decides *what* and *why* in `specs/<module>/spec.md`, with acceptance criteria.
2. **Clarify:** Claude asks about gaps and ambiguity; the answers go back into the spec.
3. **Plan:** Claude writes `specs/<module>/plan.md` (the *how*); the owner reviews it.
4. **Tasks:** Claude writes `specs/<module>/tasks.md` (small, ordered, each linked to spec IDs).
5. **Implement:** one task at a time.
6. **Verify:** every acceptance criterion has an automated test that names its ID.

Rules:
- **No code without an approved spec.** If something isn't in a spec, ask; don't guess.
- **Spec first, code second.** If a requirement changes, update the spec before the code.
- Every spec item has an ID (e.g. `WF-08`). Tasks, commits and tests reference these IDs.

## Feature specs: manual SDD (decided 2026-10-21)
The owner chose to **work without GitHub Spec Kit for now** (not installed on the office PC). We follow the
same method by hand; the file layout matches Spec Kit's, so it can be adopted later on any machine.

**Features, in build order** (one folder each under `specs/`):
| Folder | Modules | Discussion input |
|---|---|---|
| `001-platform-foundation` | IAM + PLT | [IAM](docs/discussions/identity-and-access.md), [PLT](docs/discussions/platform.md) |
| `002-org-hierarchy` | ORG | [ORG](docs/discussions/org-hierarchy.md) |
| `003-expression-language` | EXP | [EXP](docs/discussions/expression-language.md) |
| `004-apps-and-form-builder` | APP + FRM | [APP](docs/discussions/apps-and-versions.md), [FRM](docs/discussions/form-builder.md) |
| `005-workflow-engine` | WF | [WF](docs/discussions/workflow-engine.md) |
| `006-inbox-notifications` | INB + NTF | [INB](docs/discussions/inbox-and-requests.md), [NTF](docs/discussions/notifications.md) |

**Per feature:**
1. `spec.md`: written by Claude from the discussion files: summary, user stories with acceptance scenarios,
   requirements table (our IDs + **Milestone** column M1/M2/M3, see `specs/v0.1-scope.md` section 8),
   acceptance criteria, edge cases, key entities, out of scope, clarifications. **What and why only; no technology.**
   Use `specs/001-platform-foundation/spec.md` as the pattern.
2. Clarify: Claude lists open questions in the spec; the owner answers; the answers go into the spec. The owner approves the spec.
3. `plan.md`: the how (tech from this file, the "design notes" of the discussions, patterns, data, APIs, events, libraries). The owner approves.
4. `tasks.md`: small ordered tasks linked to IDs; M1 tasks first.
5. Analyze: Claude checks spec ↔ plan ↔ tasks for gaps before building.
6. Implement one task at a time; tests named by IDs; verify every acceptance criterion.

## Way of working with agents (AG1–AG3, decided 2026-10-21)
- `AGENTS.md` is a short pointer to this file for other AI tools; **this file stays the single source of truth**.
- Subagents in `.claude/agents/`: **code-reviewer** (read-only review against spec, plan, constitution; after each
  phase and before merging a stream), **spec-writer** (writes `specs/<feature>/spec.md` from discussion files;
  only in `specs/`), **test-verifier** (build + tests + traceability check).
- **Hybrid flow:** feature 001 tasks T002–T009 run **sequentially** in the main session (they share files). After
  T009, up to three **parallel streams**, each a separate session in its **own git worktree and branch**:
  (1) Identity backend T010–T019, (2) web app shell T023–T025, (3) specs 002–006 via spec-writer. A stream is
  merged only when it builds with 0 warnings and its tests pass; decisions not covered by spec/plan go to the owner.
- **Check-ins with the owner after each phase**, not after each task.

## Current phase
- 2026-10-16 and 2026-10-21: all 9 modules discussed; v0.1 scope **approved** with three milestones (M1 core engine, M2 real-world forms, M3 administration; about 20–25 build days in total).
- From 2026-10-21: **build on the office PC** (`D:\Projects\Workflow`). The owner reviews on weekdays, full days.
- This file and `docs/` + `specs/` are the source of truth; Claude memory does not move between machines or sessions reliably.

## Decisions (accepted 2026-10-16)
| Topic | Decision |
|---|---|
| Backend | ASP.NET Core on .NET 10 (LTS), C# |
| Data access | EF Core 10; Dapper allowed for dynamic/reporting queries |
| Database | SQL Server; **must stay compatible with SQL Server 2019** (EF Core compatibility level 150; store JSON in `nvarchar(max)`; no 2025-only features) |
| Frontend | React 19 + TypeScript (strict) + Vite |
| UI library | Ant Design |
| Process designer | React Flow |
| Architecture | Microservices, coarse-grained start (5 services + web app), monorepo |
| Workflow engine | Custom engine; small BPMN-like subset (see the v0.1 scope) |
| Authentication | ASP.NET Core Identity + OpenIddict (local users). AD/LDAP and Entra ID later |
| Messaging | Wolverine + transactional outbox. **Three transports coded in v0.1**, chosen by `Messaging:Transport` (same value for all services): `SqlServer` (**default**, on-premises without RabbitMQ), `RabbitMq` (on-premises; used on the dev PC), `AzureServiceBus` (Azure; code now, tested later). No automatic fallback. Focus now: on-premises. See [ADR-0004](docs/decisions/0004-messaging-transports.md), [ADR-0005](docs/decisions/0005-message-transport-selection.md) |
| IDE | The solution opens, builds and debugs in **Visual Studio 2026** (`.slnx`; F5 on the AppHost) |
| Patterns | **CQRS-lite** in every service (commands vs queries, same DB); full CQRS (read copy) later. The **workflow engine is the process manager** for requests (one DB, one transaction per step). **Wolverine sagas** only for technical multi-step jobs (bulk restart, later timers and connectors). No event sourcing, no distributed transactions (AR1) |
| Solution layout | One `.slnx`; four layers for Identity, Design, Runtime; Gateway and Worker single projects; Central Package Management; time-ordered GUID IDs (SQL Server order, T3 D3); built-in analyzers + `.editorconfig`, ESLint + Prettier; one React app (tech discussion T1) |
| Local orchestration | .NET Aspire AppHost; must run on Windows **without Docker** |
| Deployment | Azure **and** on-premises. No Azure-only dependency without an abstraction |
| Tenancy | Single tenant (one organization per installation) |
| Templates | Fluid (Liquid) for email templates |
| Email sending | **MailKit** (MIT; approved by the owner, N8) behind an `IEmailSender` interface |
| Expression numbers | Decimal arithmetic on both sides: C# `decimal`, TypeScript **decimal.js** (MIT; approved by the owner, E3). Hand-written parser/evaluator in C# and TypeScript, one shared conformance suite |
| Validation | FluentValidation on the server |
| Frontend data | TanStack Query (server state), Zustand (designer state), React Hook Form + Zod (forms) |
| API client | Generated from OpenAPI (orval) |
| Logging | Serilog + OpenTelemetry; correlation ID across services |
| Tests | xUnit v3 + Shouldly + NSubstitute (hand-written fakes first); integration tests via `WebApplicationFactory` on the real `flowza_test_*` databases with Respawn; NetArchTest architecture rules; coverlet + ReportGenerator (80% gate); Vitest + React Testing Library + MSW; Playwright (TypeScript) with axe checks. **No FluentAssertions** (v8 is commercially licensed) |
| Observability (T7) | Info/Warning/Error levels with structured templates; privacy guard = IDs only + masking policy + log-scan test; OpenTelemetry metrics; error reference = first 8 characters of the trace ID; production log files per service per day, kept 30 days |
| Dev and CI (T9) | AppHost connects to local SQL Server and RabbitMQ by connection string (no containers); smtp4dev and Vite as programs; secrets in AppHost user secrets; CI on GitHub Linux runners with **SQL Server 2019** and RabbitMQ containers; `main` protected by CI; Dependabot + licence check; npm |
| Frontend (T10) | React Router; feature folders + `shared/`; react-i18next; Ant Design theme tokens; orval-generated TanStack Query hooks with one fetch function (anti-forgery header, ProblemDetails); own runtime form renderer on React Hook Form; Day.js + `Intl`; jsx-a11y lint + axe |
| API docs viewer | **Scalar** (MIT; approved by the owner, PL5), development only |
| CI | Private GitHub repository **https://github.com/mohshacopilot/flowza** (remote `origin`, free plan: 2,000 Actions minutes/month on Linux) + GitHub Actions (build, tests, 80% coverage gate) (PL7). **Push only when the owner says so.** Free private repos cannot protect `main`, so **DV4 = rule instead of setting** (owner, 2026-10-21): a change counts as done only when CI is green |
| Form data | **Original in SQL Server** (Runtime; JSON column; one transaction with the workflow). Later an optional **read copy** in a Datalist service, **SQL Server or MongoDB** chosen by DI, synced by `FormDataSaved` events. See [ADR-0002](docs/decisions/0002-form-data-original-and-read-copy.md). **Three layers** (JSON original v0.1, Searchable-field index v0.2, reporting tables per form later) and **step-wise saving**: see [ADR-0003](docs/decisions/0003-form-data-storage-layers.md) |
| Drag and drop | **dnd-kit** (MIT; approved by the owner, F10) for the form designer |
| Audit log (from v0.4) | Separate **Audit service** with a pluggable store: **SQL Server** (default) or **MongoDB**, chosen by **dependency injection** from configuration (`Audit:Store`). See [ADR-0001](docs/decisions/0001-audit-service-pluggable-store.md) |

**Licensing rule:** use only free, permissively licensed libraries. Do **not** use MediatR, AutoMapper,
MassTransit v9+, NServiceBus or other commercially licensed packages. Use an in-house dispatcher (or a
source-generated mediator) and manual mapping (or Mapster).

## Services (v0.1)
| Service | Owns (its own database) | Main jobs |
|---|---|---|
| **Gateway** | none | Single entry point (YARP); routes `/api/<service>/...`; serves the built web app in production |
| **Identity** | users, passwords, system roles, unit types, org units, business roles, reporting lines | Login and tokens (OpenIddict); directory admin; **approver-rule resolution API** |
| **Design** | apps, form definitions, process definitions, versions | Designers' backend; design validation; publish (emits **`AppVersionPublished`** with all form and process definitions of the version, plus `ActiveVersionChanged` and `AppStatusChanged`; approved by the owner, see [APP](docs/discussions/apps-and-versions.md)) |
| **Runtime** | copies of published definitions, requests + form data, process instances, tasks, history, uploaded files | Workflow engine; inbox; renders notification text and asks Worker to send it |
| **Worker** | email delivery log | Sends emails with retries. Later: timers, connectors |
| **Datalist** *(later, when needed; not in v0.1)* | read copy of form data (SQL Server **or** MongoDB) | Subscribes to `FormDataSaved`; serves datalists, search, filters and dashboards. Rebuilds its copy through Runtime's API, never Runtime's database |
| **Audit** *(v0.4, not in v0.1)* | platform audit log (SQL Server **or** MongoDB) | Subscribes to integration events from all services and appends audit entries; query API for admins. Request history (WF-11) is **not** here; it stays in Runtime |

- Services never read another service's database. They talk by **events** (async, preferred) or **HTTP APIs** (sync, queries only).
- Runtime keeps its own copy of published definitions, so it never needs Design at run time.
- v0.1: Runtime calls Identity's API to resolve approvers when it creates a task. A future decision may replace this with a local copy of org data.
- One extra database, `flowza_messaging`, is used **only** by the SQL Server message transport. It holds no business data. With RabbitMQ it is not used.
- **Audit service rules (ADR-0001):** all code depends only on the `IAuditStore` interface. The SQL Server and MongoDB implementations live in Infrastructure and are registered by DI from `Audit:Store` (`SqlServer` | `MongoDb`), one per installation. The store is append-only; the event ID is the unique key (idempotent); no form values or personal data. One shared test suite must pass for **both** stores.
- **Form data rules (ADR-0002):** Runtime's SQL Server is the source of truth. Every change to form data publishes `FormDataSaved` (record ID, form, app version, **data version** that increases by 1 per change, **full snapshot** of values) in the same transaction. Read rule: **if you will change the data, read the original (Runtime); if you only look, read the copy (Datalist service)**. The copy ignores events with an older data version. Until the Datalist service exists, lists read from Runtime.
- **Form data layers (ADR-0003):** a field key is a name inside the record's JSON, **never** a database column in Runtime. Each workflow step saves **only the fields it may edit** (merged onto the latest record); parallel branches never share an editable field (publish check). Real per-form tables exist only in the rebuildable reporting copy (Layer 3).
- **v0.1 prepares for Audit:** every important action publishes an integration event that carries an event ID, actor, time (UTC) and correlation ID.

## Planned repository layout
```
CLAUDE.md
docs/        constitution.md, product-vision.md, decisions/ (ADRs), discussions/ (module discussions), architecture/
specs/       v0.1-scope.md, <module>/spec.md, plan.md, tasks.md
src/
  AppHost/            .NET Aspire orchestration
  ServiceDefaults/    shared health checks, logging, telemetry
  Gateway/
  Services/<Name>/    <Name>.Api, <Name>.Application, <Name>.Domain, <Name>.Infrastructure
  BuildingBlocks/     Contracts (events, versioned), Messaging, Expressions, ...
  Web/                React app
tests/
  <Name>.UnitTests, <Name>.IntegrationTests, EndToEnd/, conformance/ (shared expression test cases)
```

## Conventions
- **C#:** nullable enabled, warnings as errors, file-scoped namespaces, async all the way, records for DTOs and events.
- **Layers:** Domain → Application → Infrastructure → Api. Dependencies point inward. Domain has no EF Core or ASP.NET references.
- **Inside a service (T2):** Wolverine is also the in-process mediator (plain handler classes); Minimal APIs grouped per feature; **vertical slices** (one folder per use case: command/query, handler, validator, DTOs); expected errors as an own `Result` type mapped to ProblemDetails, exceptions only for the unexpected; DDD aggregates/value objects/domain events where rules live; domain events stay inside, integration events leave via the outbox; paging = page + size (25) + total.
- **Data (T3):** soft delete + audit columns set automatically (EF interceptor and query filter); `TimeProvider` for "now"; time-ordered GUIDs generated in code in **SQL Server's sort order**; EF no-tracking projections first, Dapper only for dynamic lists/reports; `nvarchar` + case-insensitive collation; form-data JSON handled with System.Text.Json by our code; READ_COMMITTED_SNAPSHOT on every database.
- **APIs:** REST under `/api/v1/...`, errors as ProblemDetails, OpenAPI document per service.
- **Events:** past-tense names (`ProcessPublished`), in `BuildingBlocks/Contracts`. A released event never changes; add a new version instead. Handlers must be idempotent.
- **Messaging (T4):** contracts in one `Flowza.Contracts` project, versioned namespaces (`…Events.V1`); every integration event carries EventId, OccurredAt (UTC), ActorId, CorrelationId; Wolverine durable inbox + idempotent handlers; 3 quick retries, then 5 s / 30 s / 5 min, then dead-letter queue; Wolverine RabbitMQ conventions (exchange per event, queue per subscriber; commands point-to-point); JSON; warn above 5 MB.
- **Security (T5):** Gateway is the **BFF** (browser holds only a secure HTTP-only SameSite=Strict cookie; Gateway runs code flow + PKCE and keeps tokens in its encrypted cookie; no Gateway database); login page served by Identity; services get their own tokens (client credentials) and validate tokens locally; role policies + record-level checks in handlers; anti-forgery header on every change; dev certs/user secrets, production certificate store or Key Vault; HTTPS everywhere with security headers at the Gateway.
- **Gateway (T6):** routes `/api/{identity|design|runtime|worker}/…` to services, login/token endpoints to Identity, the rest to the web app (built files in production, Vite in development); rate limits (login per IP, per user) from configuration; strips incoming identity headers, forwards `traceparent`; one file per upload request (field limit, default 10 MB), other requests ≤ 1 MB; Aspire service discovery in development, configured addresses in production.
- **Data:** times in UTC; date-only values as `date`; optimistic concurrency (`rowversion`) on aggregates; published definitions are immutable.
- **TypeScript:** strict mode, no `any`, API types come from the generated client.
- **Tests:** a test for an acceptance criterion carries its ID in the name, e.g. `WF_08_Reject_ends_request_as_rejected`.
- **Commits:** Conventional Commits, referencing spec IDs, e.g. `feat(runtime): reject action ends request (WF-08)`. Claude commits **locally** after each finished task; **never pushes** without the owner's explicit OK (TS7).
- **No business-domain words** (leave, loan, patient, …) in platform code. They may appear only in seed data, demo apps and end-to-end tests.
- **Ask first:** no new third-party library, API/event contract change or database design change without the owner's approval in the plan (constitution 1.6).
- **Privacy:** never log form values or personal data; log IDs only (constitution 11.2).
- **No hard delete** of business records; use soft delete (constitution 8.6).
- **Saved designs** carry a format version and are upgraded automatically (constitution 8.7).
- **UI text** comes from translation files, never hard-coded; every form field has a label and works with the keyboard (constitution 12).
- **Coverage:** at least 80% line coverage for Domain and Application code (constitution 5.6).

## Development environment
- .NET 10 SDK, Node.js 24 LTS, git, SQL Server 2019+ (Developer or Express edition is fine for dev). The office build PC has SQL Server 2025 Express: keep EF Core compatibility level 150 so nothing 2025-only is used
- Email in development: **smtp4dev** (`dotnet tool install -g Rnwood.Smtp4dev`), so no real emails are sent
- Docker is optional, not required
- **Databases** are created **manually by the owner**; the app connects with a **SQL login** whose password lives only in .NET user secrets (never in git or chat). The login needs `db_owner` on each database. Ten databases: `flowza_identity`, `flowza_design`, `flowza_runtime`, `flowza_worker`, `flowza_messaging` and the same five with `flowza_test_` for integration tests (script: `docs/setup/create-databases.sql`)
- **RabbitMQ** (native Windows install with Erlang) for the default message transport; management UI on `http://localhost:15672`; use a dedicated RabbitMQ user (password in user secrets), not `guest`
- MongoDB Community Server (native Windows install) only when working on the Audit service's MongoDB store (v0.4)
