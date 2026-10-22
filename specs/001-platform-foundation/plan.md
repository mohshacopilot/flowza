# Plan 001: Platform foundation (IAM + PLT)

| | |
|---|---|
| **Status** | **Approved by the owner, 2026-10-21**, including PA1–PA7 (section 14) |
| **Date** | 2026-10-21 |
| **Spec** | [spec.md](spec.md) (approved 2026-10-21) |
| **Inputs** | [Tech discussion T1–T10](../../docs/discussions/tech-discussion.md), [IAM](../../docs/discussions/identity-and-access.md), [PLT](../../docs/discussions/platform.md), [ADR-0004](../../docs/decisions/0004-messaging-transports.md), [CLAUDE.md](../../CLAUDE.md) |
| **Next** | `tasks.md` after this plan is approved |

> **This is the "how".** The spec says what and why; this plan chooses projects, data, APIs, events,
> libraries and patterns. Anything here that changes a contract or the database needs the owner's approval
> (constitution 1.6); section 14 lists exactly those items.

## 1. Approach in one paragraph
Feature 001 builds the **skeleton of all five services** and the web app, wired together end to end, and
fills in **one complete service: Identity** (login, users, roles, passwords, lockout, sessions). Design,
Runtime and Worker get only their foundations in 001 (start-up, database, messaging, health, logs), so that
later features add business logic to a structure that already builds, runs, is tested and is traced. The
web app gets its shell (layout, routing, translations, error pages) plus the user-administration and
"my profile / change password" screens. CI runs everything on every push.

## 2. Technical context
| Item | Choice |
|---|---|
| Runtime | .NET 10 (C#), ASP.NET Core Minimal APIs |
| Orchestration (dev) | Aspire AppHost, no containers; local SQL Server 2025 Express (compatibility level 150) and local RabbitMQ |
| Data | EF Core 10 per service; SQL Server; READ_COMMITTED_SNAPSHOT |
| Messaging | Wolverine: in-process mediator + **SQL Server (default) / RabbitMQ / Azure Service Bus** transports ([ADR-0005](../../docs/decisions/0005-message-transport-selection.md)) + EF Core outbox and durable inbox |
| Auth | ASP.NET Core Identity + OpenIddict (Identity service); Gateway = BFF (cookie, code flow + PKCE) |
| Gateway | YARP |
| Logs/traces | Serilog + OpenTelemetry; Aspire dashboard in dev; daily files in production |
| Web | React 19 + TypeScript (strict) + Vite, Ant Design, React Router, TanStack Query, React Hook Form + Zod, react-i18next, orval, Day.js |
| Tests | xUnit v3, Shouldly, NSubstitute, Respawn, NetArchTest, coverlet + ReportGenerator, Vitest + RTL + MSW, Playwright (TS) + axe |
| IDE | Visual Studio 2026 (`Flowza.slnx`, F5 on the AppHost) |
| Constraints | Runs on SQL Server 2019+, Windows without Docker, no cloud-only service (NFR-04) |

Library versions: latest stable at build time, pinned in `Directory.Packages.props` (and `package.json`).

## 3. Constitution check
| Article | How this plan complies |
|---|---|
| 1 Spec first | Every task in `tasks.md` names a spec ID; tests carry the IDs |
| 2 Generic core | No business-domain words; only users, roles, settings |
| 3 Service boundaries | Five services, own databases, events through the outbox, idempotent handlers, versioned contracts |
| 4 Clean design | Four layers in Identity/Design/Runtime; NetArchTest enforces dependencies |
| 5 Testing | Unit + integration (real SQL Server) + end-to-end; 80% gate on Domain/Application |
| 6 Portability | No containers needed locally; no Azure-only service; CI proves SQL Server 2019 |
| 7 Security | All APIs authenticated except login and health; server-side role and record checks; hashed passwords; secrets outside git |
| 8 Data | UTC, rowversion, soft delete, no hard delete |
| 9 Simplicity | Design/Runtime/Worker are skeletons only; only approved libraries |
| 10 Observability | Correlation via `traceparent`, health per service, reference codes on errors |
| 11 Privacy | IDs-only logging, masking policy, log-scan test; events carry IDs only |
| 12 Usable by everyone | All UI text from translation files; keyboard-operable screens; labels on every field |

## 4. Solution structure
```
Flowza.slnx
Directory.Build.props            .NET 10, nullable, warnings as errors, analyzers (recommended), implicit usings
Directory.Packages.props         all package versions (Central Package Management)
.editorconfig  global.json  .github/workflows/ci.yml  .github/dependabot.yml
src/
  AppHost/Flowza.AppHost                      Aspire: services, Gateway, Web (Vite), smtp4dev; connection strings
  ServiceDefaults/Flowza.ServiceDefaults      OpenTelemetry, Serilog, health, ProblemDetails, token validation, Wolverine defaults
  Gateway/Flowza.Gateway                      YARP + BFF (cookie, OIDC client), rate limits, security headers, static web files
  Services/Identity/
    Flowza.Identity.Domain                    User rules, value objects (Username, Email), domain events
    Flowza.Identity.Application               Vertical slices: Users/*, Me/*, Account/*; Result; validators
    Flowza.Identity.Infrastructure            EF Core DbContext, ASP.NET Identity stores, OpenIddict, migrations, Wolverine outbox
    Flowza.Identity.Api                       Minimal API endpoints, Razor Pages (login, change password), OIDC server endpoints
  Services/Design/  (4 projects, skeleton)    start-up, DbContext with no business tables yet, health, messaging
  Services/Runtime/ (4 projects, skeleton)    same as Design
  Services/Worker/Flowza.Worker               single project: start-up, DbContext, health, messaging
  BuildingBlocks/
    Flowza.Contracts                          integration events (V1), standard event fields
    Flowza.Common                             Result/Error, SQL Server-ordered GUID generator, clock helpers, paging types
    Flowza.Persistence                        EF conventions: audit columns, soft-delete filter, rowversion, UTC converters
    Flowza.Messaging                          Wolverine set-up: transport switch, outbox, inbox, retry/dead-letter policy
  Web/                                        React app (shell + IAM screens)
tools/Flowza.Cli                              `init-admin` (C2) and `demo-data` (PLT-07) commands
tests/
  Flowza.Identity.UnitTests  Flowza.Identity.IntegrationTests
  Flowza.Platform.IntegrationTests            messaging (both transports), health, correlation, log privacy
  Flowza.ArchitectureTests                    NetArchTest rules
  Flowza.Gateway.IntegrationTests
  Web tests live next to the code (Vitest);  e2e/ (Playwright)
docs/setup/create-databases.sql              (exists)
```

## 5. Identity service design
### 5.1 Domain
- **User** aggregate (wraps the ASP.NET Identity user): `Username` (value object: 3–50 chars, letters, digits,
  `.` `_` `-`; never changes, C4), `DisplayName`, `Email` (value object, unique ignoring case, C3), `Mobile`
  (optional), `Status` (Active / Inactive), `MustChangePassword`, `PasswordChangedAt`, system roles.
- Rules in the domain: cannot remove or deactivate the **last active Platform Admin** (L3); every active user
  has **User**; re-activation allowed (C6).
- Domain events: `UserCreated`, `UserDeactivated`, … translated to integration events (section 7).

### 5.2 Data (database `flowza_identity`)
| Table / group | Contents | Notes |
|---|---|---|
| `Users` (ASP.NET Identity user, customised) | Id (time-ordered GUID), UserName, NormalizedUserName, DisplayName, Email, NormalizedEmail, Mobile, IsActive, MustChangePassword, PasswordChangedAt, PasswordHash, SecurityStamp, lockout columns, audit columns, `RowVersion` | Unique indexes on NormalizedUserName and NormalizedEmail |
| `Roles`, `UserRoles` | Platform Admin, App Designer, User (seeded) | |
| `PasswordHistory` | UserId, PasswordHash, ChangedAt | Used from M3 (IAM-06 reuse); table created in 001 to avoid a later migration on a hot table |
| OpenIddict tables | Applications (Gateway, Design, Runtime, Worker clients), Authorizations, Scopes, Tokens | Standard OpenIddict EF Core model |
| `DataProtectionKeys` | Keys for cookies and tokens | So restarts and several instances work |
| Wolverine tables (`wolverine` schema) | Outbox / inbox envelopes | Created by Wolverine |
| `PlatformSettings` | M3 only (PLT-08) | Not in 001 |

Soft delete is not used for users: users are **deactivated**, never deleted (constitution 8.6).

### 5.3 API (Identity, behind the Gateway at `/api/identity/…`)
| Method and path | Purpose | Who |
|---|---|---|
| `GET /api/v1/users?page&size&search&status` | List users (paged, 25) | Platform Admin |
| `POST /api/v1/users` | Create user (returns a temporary password once) | Platform Admin |
| `GET /api/v1/users/{id}` | User details | Platform Admin |
| `PUT /api/v1/users/{id}` | Edit display name, email, mobile (rowversion check) | Platform Admin |
| `PUT /api/v1/users/{id}/roles` | Set system roles | Platform Admin |
| `POST /api/v1/users/{id}/deactivate` · `/activate` | Status changes | Platform Admin |
| `POST /api/v1/users/{id}/reset-password` | Temporary password, shown once (K6) | Platform Admin |
| `POST /api/v1/users/{id}/unlock` | End lockout early (K3) | Platform Admin |
| `GET /api/v1/me` | My profile and roles | Any user |
| `POST /api/v1/me/password` | Change my password (current + new) | Any user |
| `/connect/authorize`, `/connect/token`, `/connect/logout`, `/.well-known/openid-configuration` | OIDC server (OpenIddict) | Gateway / services |
| `/account/login`, `/account/change-password` | Server-rendered pages (login, forced change) | Anonymous / logging-in user |

Errors are ProblemDetails; validation errors list fields; 409 for rowversion conflicts.

### 5.4 Key flows
**Login (BFF, SE1/SE2):** browser → Gateway (no cookie) → redirect to `/connect/authorize` (code + PKCE) →
Identity login page → credentials checked, lockout counted (K3) → if `MustChangePassword`, the forced
change page first (IAM-03, K6) → code back to the Gateway → Gateway exchanges it for tokens, stores them in
its **encrypted, HTTP-only, SameSite=Strict cookie** → browser sees only the cookie.

**Calling an API:** browser → Gateway with cookie + anti-forgery header → Gateway checks the session, attaches
the access token → service validates the token locally (Identity's signing keys) and checks roles/records.

**Session lifetime (IAM-08, C1):** Gateway cookie: 30-minute sliding expiry, 12-hour absolute limit; refresh
token lifetime capped at 12 hours.

**Deactivation takes effect at once (K7):** Identity revokes the user's tokens, updates the security stamp and
publishes `UserDeactivated`. Every service and the Gateway keep a small in-memory **deny list** of inactive
user IDs, filled from these events (and from Identity at start-up); requests from those users are refused
at once. `UserActivated` removes the entry.

**First admin (IAM-03, C2):** `flowza init-admin` (tools/Flowza.Cli) creates the Platform Admin if none
exists, generates a strong random password, prints it **once to the console** (never to logs) and sets
`MustChangePassword`.

**Rate limiting (L6):** at the Gateway, per IP on the login endpoints; per user on the APIs; values in configuration.

**Password rules (IAM-06, M1 defaults):** ASP.NET Identity validators + custom validators: minimum 10,
not equal to the username, not in a **common-password list** (embedded resource, top 10,000 list).

## 6. Skeleton services (Design, Runtime, Worker)
Each starts, connects to its database, applies migrations in development, joins messaging, exposes
`/health/live` and `/health/ready`, logs with correlation, validates tokens and returns 401 for anonymous
calls (IAM-05). No business endpoints yet. Worker additionally receives no messages until feature 006.

## 7. Integration events (Flowza.Contracts, `Flowza.Contracts.Identity.Events.V1`)
All carry the standard fields **EventId, OccurredAt (UTC), ActorId, CorrelationId** (MS2) and IDs only, never
names, emails or passwords (constitution 11).
| Event | Extra data | Used by |
|---|---|---|
| `UserCreated` | UserId | Audit (v0.4); Runtime/others later |
| `UserUpdated` | UserId | same |
| `UserRolesChanged` | UserId, roles | Services' role-aware caches |
| `UserDeactivated` / `UserActivated` | UserId | **Deny lists (K7)** in Gateway and all services |
| `UserLoggedIn` / `UserLoginFailed` / `UserLockedOut` | UserId (only for existing users) | Audit (IAM-L5) |
| `UserPasswordChanged` / `UserPasswordReset` | UserId | Audit |

Failed logins for **unknown** usernames publish nothing (no personal data); they are counted in a metric.

## 8. Shared building blocks
- **Flowza.Messaging:** `AddFlowzaMessaging()` reads `Messaging:Transport` (`SqlServer` default | `RabbitMq` |
  `AzureServiceBus`, ADR-0005; one strategy class per transport, chosen once at start-up, no automatic
  fallback; an unreachable broker makes the service "not ready"), sets up
  Wolverine with the EF Core outbox, durable inbox, the MS4 retry policy and dead-letter queues, and RabbitMQ
  conventional routing (MS5). **SQL Server topology (ADR-0005 amendment, 2026-10-22):** Wolverine's main store is
  `flowza_messaging` (shared queue tables in schema `queues`, the service's envelope tables in `wolverine_<service>`);
  the service's own database is an ancillary store enrolled with its DbContext, so outbox and inbox entries still
  commit with the business change. Other transports keep the service's database as the main store.
- **Flowza.Persistence:** a base `DbContext` convention set: audit columns (`CreatedAt/By`, `UpdatedAt/By`)
  via a SaveChanges interceptor using `TimeProvider` and the current user; soft-delete query filter; `RowVersion`
  on aggregates; time-ordered GUID value generator in SQL Server order (D3); UTC converters.
- **Flowza.Common:** `Result`/`Error` (S4), paging types (S7), GUID generator, clock helpers.
- **Flowza.ServiceDefaults:** OpenTelemetry (traces, metrics, logs), Serilog with the **masking policy** (OB2),
  health checks, ProblemDetails with the **reference code** (first 8 characters of the trace ID, OB4),
  token validation, the deny list, Wolverine defaults.

## 9. Gateway
- YARP routes (GW1): `/api/identity/{**}` → Identity `/api/{**}`; same for design, runtime, worker; `/connect/**`
  and `/account/**` → Identity; everything else → web app (Vite in development, built files in production).
- BFF: ASP.NET Core cookie + OpenID Connect handlers; a YARP transform attaches the access token; token
  refresh before expiry. (Not Duende BFF: it is commercially licensed.)
- `GET /bff/user` returns the logged-in user's display name and roles for the web app.
- Anti-forgery: required header on POST/PUT/DELETE (SE5); strips incoming identity headers (GW4).
- Security headers (SE7): HSTS (production), CSP, `X-Content-Type-Options`, `frame-ancestors 'none'`.
- Limits (GW5): request body 1 MB except upload routes (feature 004).

## 10. Web app (feature 001 part)
- **Shell:** Ant Design layout (header with installation name, user menu, logout; side navigation by role),
  React Router routes, lazy-loaded feature areas, error boundary showing the **reference code**, 403/404 pages.
- **i18n:** react-i18next, `locales/en/*.json`; lint rule against hard-coded strings in JSX.
- **API:** orval generates hooks from Identity's OpenAPI; one fetch function adds the anti-forgery header and
  turns ProblemDetails into form errors or messages.
- **Screens:** Users list (search, status filter, paging), Create/Edit user (React Hook Form + Zod), roles,
  activate/deactivate (with last-admin message), reset password (temporary password shown once with copy
  button), unlock; My profile; Change password.
- **Login page** lives in Identity (Razor Pages) with Flowza's look and its own resource files.

## 11. Local development and configuration
- AppHost reads from its **user secrets**: `ConnectionStrings:Sql` parts (server `.`, SQL login, password),
  `RabbitMq` (host, user, password; only when `Messaging:Transport=RabbitMq`); passes each service its own
  connection string (`flowza_<service>`). The development PC uses `SqlServer` (no RabbitMQ since 2026-10-22).
- smtp4dev and Vite started as programs; Aspire dashboard shows logs, traces, metrics, health.
- First run: owner runs `docs/setup/create-databases.sql`, sets user secrets, F5 on the AppHost, then
  `flowza init-admin` (or `flowza demo-data` on empty databases).

## 12. Testing plan
| Spec IDs | Test type | How |
|---|---|---|
| IAM-01 to IAM-08, IAM-L5 | Unit (domain rules, validators) + integration (Identity in memory on `flowza_test_identity`) + end-to-end (login, forced change, admin flows) | Respawn resets data per test class |
| IAM-05 | Integration per service | Anonymous calls → 401, except health |
| IAM-04-AC3 (K7) | Integration across Gateway + one service | Deactivate → next call refused within 1 second |
| PLT-02 | End-to-end smoke | AppHost starts; Gateway serves the shell |
| PLT-03 | Integration | Migrations on empty DB; second run no changes; idempotent script generated and run twice |
| PLT-04 | Integration, **both transports** | Rolled-back change → no message; receiver down → delivered later; duplicate → handled once |
| PLT-05 | Integration | Ready fails without DB; one correlation ID across Gateway → service → handler; **log-scan** finds no seeded personal values |
| PLT-06 | Manual checklist (`specs/001-platform-foundation/checklists/setup.md`) | Owner signs off |
| PLT-07 | Integration | Demo data on empty DBs; refusal on non-empty |
| PLT-09 | CI itself | Workflow runs build, tests, coverage gate |
| Architecture | NetArchTest | Domain has no EF/ASP.NET; no project references across services; Contracts has no dependencies |

## 13. CI (`.github/workflows/ci.yml`)
Linux runner; service container **SQL Server 2019** (no RabbitMQ: on-premises = SQL Server only, cloud = Azure
Service Bus, ADR-0005 amendment 2, 2026-10-22); steps: restore → build (warnings as
errors) → create test databases → unit + integration tests (SQL Server transport) → coverage gate (80% Domain +
Application) → web lint + Vitest → Playwright end-to-end (headless) → licence check of NuGet and npm
dependencies. Dependabot weekly. Runs only after the owner allows the push to the public repository (created 2026-10-21).

## 14. Items approved by the owner (constitution 1.6, approved 2026-10-21)
| # | Item | Proposal |
|---|---|---|
| PA1 | **Identity API contract** | Section 5.3 |
| PA2 | **Event contracts** | Section 7 (11 events, IDs only, standard fields) |
| PA3 | **Identity database design** | Section 5.2, including `PasswordHistory` created now for M3 |
| PA4 | **Deny list for instant deactivation** (K7) | Section 5.4: event-fed in-memory list in Gateway and every service |
| PA5 | **Command-line tool** `tools/Flowza.Cli` for `init-admin` and `demo-data` | Section 5.4 / PLT-07 |
| PA6 | **Extra libraries** not yet approved | Health checks for SQL Server and RabbitMQ: `AspNetCore.HealthChecks.SqlServer` and `AspNetCore.HealthChecks.Rabbitmq` (Apache 2.0); `Serilog.AspNetCore`, `Serilog.Sinks.File`, `Serilog.Sinks.OpenTelemetry` (Apache 2.0); `Yarp.ReverseProxy` (MIT); `OpenIddict` packages (Apache 2.0); `FluentValidation` (Apache 2.0); `Scalar.AspNetCore` (MIT, approved); Aspire hosting packages (MIT) |
| PA7 | **Common-password list** | Embed a public top-10,000 list as a resource (no external call) |

## 15. Risks
| Risk | Mitigation |
|---|---|
| Tokens make the Gateway cookie large | Chunked cookies (built in); keep claims minimal (IDs and roles) |
| Aspire hosting without containers needs care for SQL/RabbitMQ | Use connection-string resources; documented in the setup guide |
| Wolverine outbox with EF Core in several DbContexts | One DbContext per service; integration tests prove rollback/delivery (PLT-04) |
| RabbitMQ dropped (owner, 2026-10-22: on-premises = SQL Server only, cloud = Azure Service Bus) | SQL Server transport everywhere on-premises, including development and CI; RabbitMQ tests skipped unless configured |
| Visual Studio 2026 + Aspire + Vite debugging | Verified in the first task (solution skeleton) before features |

## 16. Estimate
About **2.5–3 build days** (Foundation 1.5–2 + IAM core 1, from the scope check).
