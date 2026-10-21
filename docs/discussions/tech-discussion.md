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
| T2 | Inside a service: layers, request handling, validation, errors | 001 | ✅ Decided (S1–S7 a) |
| T3 | Data access: EF Core, migrations, concurrency, soft delete, JSON, time | 001 | ✅ Decided (D1–D7 a) |
| T4 | Messaging: Wolverine, outbox, idempotency, event contracts and versions | 001 | ✅ Decided ([ADR-0004](../decisions/0004-messaging-transports.md), AR1, MS1–MS7 a) |
| T5 | Security: login flow, Gateway as BFF, tokens, service-to-service calls, authorization | 001 | ✅ Decided (SE1–SE7 a) |
| T6 | Gateway: routing, rate limiting, serving the web app | 001 | ✅ Decided (GW1–GW6 a) |
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

## T2: Inside a service (decided 2026-10-21: all "a")
| # | Question | Options (⭐ = Claude's recommendation) |
|---|---|---|
| S1 | How a request reaches its handler | **a) Wolverine as the in-process mediator too (`InvokeAsync`); handlers are plain classes, no extra library ⭐** · b) Own small dispatcher |
| S2 | HTTP endpoints | **a) ASP.NET Core Minimal APIs, grouped per feature ⭐** · b) MVC controllers |
| S3 | Code organisation in Application | **a) Vertical slices: one folder per use case (command or query, handler, validator, DTOs) ⭐** · b) Technical folders (Commands/, Queries/, Validators/) |
| S4 | Expected errors (validation, not found, conflict, forbidden) | **a) A small own `Result` type, mapped to ProblemDetails at the edge; exceptions only for unexpected failures ⭐** · b) Exceptions for everything |
| S5 | Domain model | **a) DDD building blocks where rules live (aggregates, value objects, domain events); plain records for simple lookups ⭐** · b) Plain data classes with logic in handlers |
| S6 | Domain events vs integration events | **a) Domain events stay inside a service (same transaction); integration events (in Contracts, versioned) leave through the outbox ⭐** · b) One kind of event for both |
| S7 | List paging | **a) Page number + page size (25) + total count; keyset paging later only where needed ⭐** · b) Keyset (cursor) paging everywhere |

## T4: Messaging details (decided 2026-10-21: all "a")
| # | Question | Options (⭐ = Claude's recommendation) |
|---|---|---|
| MS1 | Event contracts | **a) One `Flowza.Contracts` project; records in versioned namespaces (`…Events.V1`); a released event never changes ⭐** · b) Each service defines its own copies |
| MS2 | Standard event fields | **a) Every integration event carries EventId, OccurredAt (UTC), ActorId, CorrelationId, plus its own data (prepares the Audit service) ⭐** · b) Only the business data |
| MS3 | Duplicates | **a) Wolverine durable inbox (drops repeated message IDs) **and** handlers written to be safe twice (e.g. data-version checks) ⭐** · b) Handlers only |
| MS4 | Failures | **a) 3 quick retries, then delayed retries (5 s, 30 s, 5 min), then the dead-letter queue; admin view of dead letters in v0.2 ⭐** · b) Other |
| MS5 | RabbitMQ routing | **a) Wolverine conventions: one exchange per event type, one queue per subscribing service; commands such as `SendEmailRequested` go point-to-point to one queue ⭐** · b) Hand-made topology |
| MS6 | Message format | **a) JSON (System.Text.Json) ⭐** · b) A binary format |
| MS7 | Large messages (`AppVersionPublished` carries whole definitions) | **a) Send in full; warn in logs above 5 MB; "claim check" (send a reference, fetch the body) only if real apps exceed it ⭐** · b) Claim check from day one |

## T5: Security (decided 2026-10-21: all "a"; confirms the Gateway as BFF)
| # | Question | Options (⭐ = Claude's recommendation) |
|---|---|---|
| SE1 | Gateway as BFF (from IAM design notes) | **a) Yes: the browser holds only a secure, HTTP-only, SameSite cookie; the Gateway logs in with the standard code flow + PKCE and keeps the tokens inside its encrypted cookie (Gateway stays stateless, no database) ⭐** · b) The React app holds tokens itself |
| SE2 | Login page | **a) Served by the Identity service (server-rendered page in Flowza's look, texts from translation files); passwords never pass through the React app; ready for SSO later ⭐** · b) A React login page sending the password to an API (an outdated, weaker pattern) |
| SE3 | Service-to-service calls (e.g. Runtime → Identity resolve API) | **a) The calling service gets its own token (client credentials); each service validates tokens locally with Identity's signing keys ⭐** · b) No authentication inside the network |
| SE4 | Authorization | **a) Role policies (Admin, Designer, User) on endpoints **plus** record-level checks in handlers (task assignee, who may view a request, NFR-01) ⭐** · b) Roles only |
| SE5 | Cross-site request forgery | **a) SameSite=Strict cookie plus an anti-forgery header on every change request ⭐** · b) Cookie setting only |
| SE6 | Keys and secrets | **a) Development: .NET development certificates and user secrets. Production: certificates from the Windows certificate store or Azure Key Vault, secrets from environment variables or Key Vault, all behind configuration ⭐** · b) Other |
| SE7 | HTTPS | **a) HTTPS everywhere, also between services; HSTS and security headers (CSP, no framing, no sniffing) added by the Gateway ⭐** · b) HTTPS only at the Gateway |

## T6: Gateway (decided 2026-10-21: all "a")
| # | Question | Options (⭐ = Claude's recommendation) |
|---|---|---|
| GW1 | Routes | **a) `/api/identity/…`, `/api/design/…`, `/api/runtime/…`, `/api/worker/…` to the services; login pages and token endpoints to Identity; everything else = the web app ⭐** · b) Other |
| GW2 | Serving the web app | **a) Production: the Gateway serves the built React files. Development: it forwards to the Vite dev server (hot reload) ⭐** · b) A separate web server |
| GW3 | Rate limits | **a) At the Gateway: login per IP (L6) and a general per-user limit; numbers in configuration ⭐** · b) In each service |
| GW4 | Trust | **a) The Gateway removes any incoming identity headers from the browser and forwards the trace reference (`traceparent`) ⭐** · b) Pass headers through |
| GW5 | Upload size | **a) Files upload one per request, up to the field's limit (default 10 MB); other requests limited to 1 MB ⭐** · b) One big request with the form and all files |
| GW6 | Finding services | **a) Aspire service discovery in development; addresses from configuration in production ⭐** · b) A discovery server (e.g. Consul) |

## T3: Data access (decided 2026-10-21: all "a"; D7 approves READ_COMMITTED_SNAPSHOT in the database script)
| # | Question | Options (⭐ = Claude's recommendation) |
|---|---|---|
| D1 | Soft delete and audit columns | **a) `IsDeleted`/`DeletedAt`/`DeletedBy` with an automatic EF filter; `CreatedAt/By`, `UpdatedAt/By` filled automatically on save ⭐** · b) By hand in each handler |
| D2 | Time | **a) .NET `TimeProvider` everywhere (testable "now"); `datetime2` in UTC; `date` for date-only values ⭐** · b) `DateTime.UtcNow` directly |
| D3 | GUID ordering (TS5 follow-up): SQL Server sorts GUIDs by different bytes than GUID v7 is ordered by, so plain v7 GUIDs still scatter in SQL Server indexes | **a) Time-ordered GUIDs generated in code **in SQL Server's sort order** (keeps TS5's intent: made in code, ordered in the index) ⭐** · b) Plain GUID v7 (accept some index fragmentation) |
| D4 | Queries | **a) EF Core no-tracking projections first; Dapper only where EF is not enough (dynamic lists, reports) ⭐** · b) Dapper for all reads |
| D5 | Text and collation | **a) `nvarchar` for all text (Indian languages); case-insensitive collation (matches C3/C4) ⭐** · b) Other |
| D6 | Form data JSON | **a) Stored as raw JSON text and read/written with System.Text.Json by our own code (fields are dynamic); typed JSON (definitions) mapped by EF ⭐** · b) EF JSON mapping for everything |
| D7 | Locking behaviour | **a) READ_COMMITTED_SNAPSHOT ON for every database (readers never block writers; set in the database script) ⭐** · b) SQL Server default locking |
