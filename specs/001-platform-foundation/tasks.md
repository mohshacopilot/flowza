# Tasks 001: Platform foundation (IAM + PLT)

| | |
|---|---|
| **Status** | Ready to implement (spec and plan approved 2026-10-21) |
| **Spec / Plan** | [spec.md](spec.md) · [plan.md](plan.md) |
| **Rules** | One task at a time, in order. Each task ends with: builds without warnings, its tests pass, a local commit (`feat|chore|test(scope): … (IDs)`). `[P]` = can run in parallel with the previous task. Nothing is pushed without the owner's OK |

## Before coding: owner's one-time setup
**Status 2026-10-21:** O1 ✅ (10 databases, compatibility 150, read-committed snapshot, `flowza_app` db_owner; verified)
· O3 ✅ (RabbitMQ 4.4.0 / Erlang 29, vhosts `flowza` + `flowza_test`, user `flowza_app`; verified) · O2 and O4 open (done in T006).

| # | Step | Needed from |
|---|---|---|
| O1 | Allow SQL logins, create the SQL login (e.g. `flowza_app`) and run [docs/setup/create-databases.sql](../../docs/setup/create-databases.sql) (steps: [local-setup.md](../../docs/setup/local-setup.md) §1) | T007 |
| O2 | Store secrets on the PC (commands will be given in T006): SQL login password, RabbitMQ password | T006 |
| O3 | Install Erlang + RabbitMQ, enable the management plugin, create virtual hosts `flowza` and `flowza_test` and the user `flowza_app` ([local-setup.md](../../docs/setup/local-setup.md) §2). Until then the SQL Server transport is used | T008 (RabbitMQ part) |
| O4 | Install smtp4dev: `dotnet tool install -g Rnwood.Smtp4dev` | T006 |

## Phase 0: Foundation (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T001 | **Solution skeleton:** `Flowza.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json`, `.gitignore` update; all projects from plan §4 created empty and referenced correctly | PLT-01 | Builds with 0 warnings; opens in Visual Studio 2026 |
| T002 | **Architecture tests** (NetArchTest): Domain has no EF/ASP.NET; no cross-service references; Contracts depends on nothing | Constitution 3, 4 | Tests pass on the skeleton |
| T003 | **Flowza.Common:** `Result`/`Error`, paging types, SQL Server-ordered time-based GUID generator | (T2 S4, S7; T3 D3) | Unit tests incl. ordering test against SQL Server sort order |
| T004 | **Flowza.Persistence:** audit-column interceptor (`TimeProvider` + current user), soft-delete filter, `RowVersion`, UTC converters | Constitution 8 | Unit + integration tests on `flowza_test_*` |
| T005 | **Flowza.ServiceDefaults:** OpenTelemetry, Serilog + masking policy, `/health/live` + `/health/ready`, ProblemDetails with reference code, token validation hook | PLT-05 | Unit tests for masking and reference code |
| T006 | **AppHost:** Identity, Design, Runtime, Worker, Gateway, Web (Vite), smtp4dev; SQL and RabbitMQ connection strings from user secrets; secret-setting commands documented | PLT-02 | F5 in VS 2026 starts everything; Aspire dashboard shows all resources |
| T007 | **Service skeletons:** each service starts, has its DbContext + initial migration, auto-migrates in development, health ready checks DB; anonymous API calls get 401 | PLT-03, PLT-05, IAM-05 | `PLT_03_AC1_*`, `PLT_05_AC1_*`, `IAM_05_AC1_*` tests pass |
| T008 | **Flowza.Messaging + Flowza.Contracts:** transport switch with **three transports** (`SqlServer` default, `RabbitMq`, `AzureServiceBus`; ADR-0005), EF outbox, durable inbox, retry and dead-letter policy, transport shown in health details, base event fields (EventId, OccurredAt, ActorId, CorrelationId) | PLT-04, MS1–MS7 | SqlServer and RabbitMq start from configuration; AzureServiceBus compiles and is configured (unit tests of the selection) |
| T009 | **Platform integration tests:** rollback → no message; receiver down → delivered later; duplicate → once; one correlation ID across services; log scan finds no personal data; production migration script runs twice. Messaging tests run on **SqlServer and RabbitMq**; the **AzureServiceBus** run is written but skipped until Azure access (category `RequiresAzure`) | PLT-03-AC2, PLT-04-AC1/AC2, PLT-05-AC2/AC3 | All named tests pass on SqlServer and RabbitMq |

## Phase 1: Identity service (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T010 | **Domain:** `User` aggregate, `Username` and `Email` value objects, last-active-admin rule, User role always present, re-activation | IAM-02, IAM-04 (C3, C4, C6, L3) | Unit tests `IAM_02_AC2_*`, `IAM_04_AC4_*` |
| T011 | **Infrastructure:** DbContext with ASP.NET Identity (customised user), roles seed, `PasswordHistory`, OpenIddict tables, DataProtection keys, Wolverine tables; migration | PA3 | Migration applies on `flowza_test_identity` |
| T012 | **Password and lockout rules:** min 10, not the username, common-password list (embedded), lockout 5 / 15 min | IAM-06, IAM-07 (PA7) | `IAM_06_AC1_*`, `IAM_07_AC1_*` |
| T013 | **OpenIddict server:** code flow + PKCE for the Gateway client, client credentials for services, development signing keys, discovery endpoint | SE1, SE3 | Integration test obtains tokens for both flows |
| T014 | **Login pages (Razor):** login, forced change password, locked message; texts from resource files; keyboard and labels | IAM-01, IAM-03, IAM-07 | `IAM_01_AC2_*` (general error message) |
| T015 | **Users API** (vertical slices): list, create (temporary password once), get, update (rowversion), roles, activate, deactivate (token revocation), reset password, unlock | IAM-02, IAM-04 | `IAM_02_AC1_*`, `IAM_04_AC1/AC2/AC5_*`, 409 on conflict |
| T016 | **Me API:** profile, change own password | IAM-06-AC3 | `IAM_06_AC3_*` |
| T017 | **Integration events** published through the outbox (11 events, IDs only); unknown-username failures only as a metric | IAM-L5 (PA2) | `IAM_L5_AC1_*` |
| T018 | **Deny list** in ServiceDefaults, fed by `UserDeactivated` / `UserActivated` and loaded at start-up | IAM-04-AC3 (PA4) | Unit tests; full check in T022 |
| T019 | **Flowza.Cli `init-admin`:** creates the Platform Admin if none, random password printed once to the console, must change at first login | IAM-03 (C2, PA5) | `IAM_03_AC1_*` |

## Phase 2: Gateway (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T020 | **YARP + BFF:** routes (GW1), cookie + OIDC handlers, token forwarding and refresh, `/bff/user`, 30-min sliding / 12-h absolute session | IAM-01, IAM-08, SE1 | Login round-trip works through the Gateway |
| T021 | **Protection:** anti-forgery header, incoming identity-header stripping, security headers, rate limits (login per IP, per user), 1 MB body limit | IAM-07-AC2, SE5, SE7, GW3–GW5 | Unit/integration tests per rule |
| T022 | **Gateway integration tests:** anonymous → login / 401; deactivated user refused within 1 s; session expiry; logout | IAM-01-AC1, IAM-04-AC3, IAM-05-AC1, IAM-08-AC1 | Named tests pass |

## Phase 3: Web app (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T023 | **Web scaffold:** Vite + React + TS strict, ESLint + Prettier + jsx-a11y + no-hard-coded-text rule, Ant Design theme tokens, React Router, react-i18next, Vitest + RTL + MSW | NFR-10, NFR-11 | Lint and a sample test pass |
| T024 | **Shell:** layout, navigation by role, logout, error boundary with reference code, 403 / 404 pages | IAM-02, PLT-05 | Component tests |
| T025 | **API client:** orval from Identity's OpenAPI; fetch function (anti-forgery header, ProblemDetails → messages/field errors) | (T10 FE5) | Generated client compiles; fetch tests |
| T026 | **Users admin screens:** list (search, status, paging), create/edit, roles, activate/deactivate, reset password (show once + copy), unlock | IAM-02, IAM-04, IAM-07 | Component tests with MSW |
| T027 | **My profile + change password** | IAM-06-AC3 | Component tests |

## Phase 4: Tools, end-to-end, guide, CI (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T028 | **Flowza.Cli `demo-data`:** framework + demo users with the documented demo password (C5); refuses non-empty databases. Org data and reference apps are added by features 002–005 | PLT-07 (partly, see analysis A1) | `PLT_07_AC2_*`; demo users load |
| T029 | **Playwright end-to-end:** login, forced change, admin creates user, reset, lockout, deactivation; axe accessibility checks | IAM-01, IAM-03, IAM-04, IAM-07, PLT-02, NFR-11 | e2e suite green |
| T030 | **Setup guide** (README) + manual checklist `checklists/setup.md` (lists databases to back up) | PLT-06 | Owner signs the checklist |
| T031 | **CI:** `.github/workflows/ci.yml` (SQL Server 2019 + RabbitMQ containers, both transports, coverage gate, web tests, e2e, licence check) + Dependabot | PLT-09 | Runs green once the owner creates the repository and allows the push |
| T032 | **Verify 001:** every M1 acceptance criterion has a passing named test (or signed checklist); coverage ≥ 80%; update this file's status | All M1 IDs | Traceability table below all ✅ |

## Milestone 3 tasks (later, not now)
| ID | Task | Spec IDs |
|---|---|---|
| T040 | Password policy settings: minimum length, expiry, reuse (uses `PasswordHistory`) | IAM-06-AC2 |
| T041 | Session timeouts as settings | IAM-08 (M3) |
| T042 | Settings screen and branding; `PlatformSettingsChanged` event; logo in header and emails | PLT-08-AC1/AC2 |

## Analysis (spec ↔ plan ↔ tasks)
| # | Finding | Resolution |
|---|---|---|
| A1 | **PLT-07-AC1** ("loads demo org, users and both reference apps") cannot be fully met in 001: org data, apps and processes come in features 002–005 | T028 builds the tool and demo users; each later feature adds its demo data; PLT-07-AC1 is verified at the end of M1 (feature 005/006) |
| A2 | **PLT-08-AC2** (logo in emails) depends on emails (feature 006) | Verified in M3 after 006 |
| A3 | **PLT-06-AC1** is manual | Checklist in T030, signed by the owner (constitution 1.4) |
| A4 | **PLT-09-AC1** needs the private GitHub repository and the owner's OK to push | T031 prepares the workflow; it runs on the first allowed push |
| A5 | All other M1 criteria of spec 001 map to tasks (table below) | — |

### Traceability (M1 acceptance criteria → tasks)
| Criterion | Task(s) | Status |
|---|---|---|
| IAM-01-AC1 | T020, T022, T029 | ⬜ |
| IAM-01-AC2 | T014 | ⬜ |
| IAM-02-AC1 | T015, T024, T026 | ⬜ |
| IAM-02-AC2 | T010, T015 | ⬜ |
| IAM-03-AC1 | T019, T014, T029 | ⬜ |
| IAM-04-AC1 | T015, T026 | ⬜ |
| IAM-04-AC2 | T015, T026 | ⬜ |
| IAM-04-AC3 | T015, T018, T022 | ⬜ |
| IAM-04-AC4 | T010, T015 | ⬜ |
| IAM-04-AC5 | T010, T015 | ⬜ |
| IAM-05-AC1 | T007, T022 | ⬜ |
| IAM-06-AC1 | T012 | ⬜ |
| IAM-06-AC3 | T016, T027 | ⬜ |
| IAM-07-AC1 | T012, T029 | ⬜ |
| IAM-07-AC2 | T021 | ⬜ |
| IAM-08-AC1 | T020, T022 | ⬜ |
| IAM-L5-AC1 | T017 | ⬜ |
| PLT-02-AC1 | T006, T029 | ⬜ |
| PLT-03-AC1 | T007 | ⬜ |
| PLT-03-AC2 | T009 | ⬜ |
| PLT-04-AC1 | T009 | ⬜ |
| PLT-04-AC2 | T009 | ⬜ |
| PLT-05-AC1 | T007 | ⬜ |
| PLT-05-AC2 | T009, T024 | ⬜ |
| PLT-05-AC3 | T009 | ⬜ |
| PLT-06-AC1 | T030 (manual) | ⬜ |
| PLT-07-AC1 | T028 + features 002–005 (A1) | ⬜ |
| PLT-07-AC2 | T028 | ⬜ |
| PLT-09-AC1 | T031 (A4) | ⬜ |
