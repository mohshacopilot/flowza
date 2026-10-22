# Tasks 001: Platform foundation (IAM + PLT)

| | |
|---|---|
| **Status** | **Phase 0 done (T001–T009), 2026-10-21**; **Phase 1 (T010–T019) done in the Identity stream, 2026-10-21**, waiting for review and merge |
| **Spec / Plan** | [spec.md](spec.md) · [plan.md](plan.md) |
| **Rules** | One task at a time, in order. Each task ends with: builds without warnings, its tests pass, a local commit (`feat|chore|test(scope): … (IDs)`). `[P]` = can run in parallel with the previous task. Nothing is pushed without the owner's OK |

## Before coding: owner's one-time setup
**Status 2026-10-21:** O1 ✅ (10 databases, compatibility 150, read-committed snapshot, `flowza_app` db_owner; verified)
· O3 ✅ (RabbitMQ 4.4.0 / Erlang 29, vhosts `flowza` + `flowza_test`, user `flowza_app`; verified) · O2 ✅ (passwords in the `flowza-dev` user secrets via `docs/setup/set-secrets.ps1`) · O4 ✅ (smtp4dev 3.15). Still open for the owner: trust the HTTPS development certificate (`dotnet dev-certs https --trust`).
**2026-10-22:** the owner **removed RabbitMQ** from the PC (O3 no longer needed); development uses the SQL Server
transport only. Then decided: **on-premises = SQL Server only, cloud = Azure Service Bus, RabbitMQ dropped**, and the
RabbitMQ code, package and tests were removed ([ADR-0005](../../docs/decisions/0005-message-transport-selection.md) amendments 1–3).

| # | Step | Needed from |
|---|---|---|
| O1 | Allow SQL logins, create the SQL login (e.g. `flowza_app`) and run [docs/setup/create-databases.sql](../../docs/setup/create-databases.sql) (steps: [local-setup.md](../../docs/setup/local-setup.md) §1) | T007 |
| O2 | Store secrets on the PC (commands will be given in T006): SQL login password | T006 |
| O3 | ~~Install Erlang + RabbitMQ~~ **Dropped 2026-10-22** (RabbitMQ removed from the platform) | — |
| O4 | Install smtp4dev: `dotnet tool install -g Rnwood.Smtp4dev` | T006 |

## Progress
| Task | Status | Commit |
|---|---|---|
| T001 Solution skeleton | ✅ | 9c2c9fb |
| T002 Architecture tests | ✅ | 9113257 |
| T003 Flowza.Common | ✅ | 8018d8f |
| T004 Flowza.Persistence | ✅ | 7022788, 12055b8 |
| T005 Flowza.ServiceDefaults | ✅ | cc241d5 |
| T006 + T007 AppHost and service skeletons | ✅ | 0cc7dfa |
| T008 Messaging and Contracts | ✅ | e75101f |
| T009 Platform integration tests | ✅ | 5f88701 |
| Phase 0 code review fixes | ✅ | a269a44 |
| T010 Identity domain | ✅ | faa28b8 |
| T011 Identity database | ✅ | dd1ddd0 |
| T012 Password and lockout rules | ✅ | 305977d |
| T013 OpenIddict server | ✅ | df9e6b7 |
| T014 Login pages | ✅ | 0a0bb5d |
| T015 Users API (+ T035) | ✅ | bf22e83 |
| T016 Me API | ✅ | 4a4c89d |
| T017 Integration events | ✅ | f8d0f72 |
| T018 Deny list (+ T036 current user) | ✅ | 6a836f8 |
| T019 Flowza.Cli init-admin | ✅ | 077b3d2 |
| T023 Web scaffold (web stream, merged 2026-10-22) | ✅ | 0e1526d |
| T024 Shell | ✅ | 0e87e65 |
| T025 API client set-up | ✅ (generation from Identity's real document still to do) | dfe38a6 |
| T020 Gateway YARP + BFF | ✅ | 1bf4cc3 |
| T031 CI workflow + Dependabot | ✅ first GitHub run green 2026-10-22 | 7db9946 |

**Notes from Phase 0 (decisions taken while building, within the approved plan):**
- Tests run on **Microsoft.Testing.Platform** (xUnit v3 4.x); coverage via **coverlet.MTP** (the coverlet extension for that platform).
- Integration-event deduplication: senders put the EventId in the `flowza-event-id` header; every integration-event
  handler is deduplicated on it **by policy** (no per-handler attribute). RabbitMQ does not carry Wolverine's own
  deduplication id, so the header approach is the one that works on every transport.
- The SQL Server transport keeps its queues in `flowza_messaging` as Wolverine's "ancillary" store; each service's
  outbox/inbox stays in its own database (`wolverine` schema, excluded from EF migrations; Wolverine creates it).
- `System.Security.Cryptography.Pkcs` is pinned to 10.0.13 because SqlClient brings a vulnerable 9.0.11.
- `WolverineFx.RuntimeCompilation` (MIT, same family) is needed for Wolverine's runtime code generation.
- Fixed development ports: Gateway 5100/7100, Identity 5101, Design 5102, Runtime 5103, Worker 5104, smtp4dev web 5000 / SMTP 2525.

**Phase 0 code review (independent reviewer, 2026-10-21) — fixed:**
1. Trace reference did not cross services: Wolverine's activity source and meter were not registered. Fixed; the
   trace test now uses the services' own telemetry set-up and was shown to fail without the fix.
2. Log lines had no trace ID, so the reference code could not be found in files. Fixed (output template with
   `{TraceId}`); the log-scan test now also checks the reference code.
3. PLT-03 tests ran on already-migrated databases. Now they drop the service tables first; the production script
   is run twice on an empty database.
4. The "cancelled change" test never saved. Now the save really fails (duplicate key) and nothing is delivered.
5. Soft delete could hard-delete loaded children and clear owned values. Fixed (cascades run at save time, after
   the interceptor); new test with a child table and an owned value, shown to fail without the fix.
6. Azure Service Bus: readiness check added; a test runs the delivery and duplicate scenarios as soon as
   `FLOWZA_ConnectionStrings__azure-servicebus` is set (skipped until then).
7. AppHost: connection strings are built with escaping; only the selected transport's connection is created, so
   SQL Server mode needs no RabbitMQ settings.
8. A numeric `Messaging:Transport` value (e.g. "5") is now refused.
Also: name properties (UserName, DisplayName, …) are masked in logs; log folder is relative to the service's own
folder; topic names are safe for nested/generic types. (The T008 commit message mentions a `[Deduplicated]`
architecture test that was later replaced by the policy; the code is right, the message is outdated.)

**Notes from Phase 1 (Identity stream, decisions taken while building, within the approved plan):**
- T010: usernames accept **ASCII** letters and digits only (plus `.` `_` `-`), so look-alike letters from other
  scripts cannot imitate another user's name (C4 reading; to confirm with the owner). Mobile: optional, at most 20
  characters of digits, spaces, `+ - ( )`. Display name: required, at most 100. `AggregateRoot` + `IDomainEvent`
  added to Flowza.Common (shared by later services).
- T011: credentials (password hash, security stamp, lockout counters) are EF shadow columns of `Users`, so the
  domain never sees a hash. Data Protection keys use a small own `IXmlRepository` over `DataProtectionKeys` (no extra
  package). OpenIddict pulls a vulnerable `System.Security.Cryptography.Xml` 10.0.11: pinned to 10.0.13. Integration
  tests that reset `flowza_test_identity` take a SQL application lock (`TestDatabaseLock`), because test projects run
  in parallel processes; Respawn clears data between tests (roles, OpenIddict clients and keys kept).
- T012: the password rules live in the domain (`PasswordPolicy`) and ASP.NET Core Identity runs them through an
  adapter; Identity's own character-class rules and user validators are switched off (K2; C3/C4 checked by the
  domain). The common list is SecLists `10k-most-common.txt` (MIT), embedded unchanged, checked ignoring case.
  A custom user store over the domain `User` never saves by itself; each use case saves once (one transaction with
  its events). An inactive user's login gets the same general answer as a wrong password (IAM-01-AC2).
- T013: ServiceDefaults now validates tokens in **every** service (OpenIddict validation, audience `flowza`, scope
  `flowza_api`; role policies `PlatformAdmin`, `AppDesigner`); the Identity service validates against its local
  server and also refuses revoked tokens. Services find Identity at `Flowza:Identity:Authority` (default in
  Development: `http://localhost:5101/`). Clients come from `Identity:Clients` (Gateway: secret, redirect and
  post-logout addresses; Design/Runtime/Worker: secret); a client without a secret is not registered. **AppHost
  wiring of these secrets is left to the main session** (outside this stream). Access tokens 15 min, refresh tokens
  12 h absolute (no sliding). Development signing/encryption certificates; production certificates by thumbprint
  from the local machine store. The full login → code → token round trip is tested in T014 (needs the login page).
- T014: Razor pages `/account/login` and `/account/change-password` (texts in `Resources/AccountTexts.resx`, CSS under
  `/account/assets` so the Gateway's `/account/**` route serves it). The login cookie of Identity is `SameSite=Lax`
  (it must survive the redirects from the Gateway) and its security stamp is checked on every request. A user who
  must change the password cannot get a code until they do (IAM-03, IAM-04-AC2). Wolverine resolves the ports of a
  use case by service location, so all of them share one DI scope and one DbContext (Wolverine's own default refuses
  internal implementation classes).
- T015: Users API under `/api/v1/users` (Platform Admin policy), one Wolverine command/query per endpoint; JSON enums
  as names in every service (`JsonStringEnumConverter` in ServiceDefaults). Responses with a temporary password carry
  `Cache-Control: no-store`. The row version travels as base64 `version`. Validation stays in the domain value objects
  and the use cases (role names, version); FluentValidation is not used yet, so its package entry was removed until a
  rule needs it. Identity publishes its OpenAPI document at `/openapi/v1.json` in Development (package
  `Microsoft.AspNetCore.OpenApi`, first-party, for the approved "OpenAPI document per service" convention and orval in
  T025). Known gap: two admins deactivating each other at the very same moment could both pass the last-admin check
  (no lock across users yet).
- T035: every log sink gets warnings, errors and events with an exception through `ExceptionMaskingSink`: exception
  texts and text properties have quoted SQL values and email addresses masked (EF Core also writes the exception text
  into its message as a property). The duplicate-email log-scan test failed before this fix.
- T016: `GET /api/v1/me` and `POST /api/v1/me/password` for any logged-in user; a service token (client credentials)
  gets 403 `me.not_a_user`. The API and the forced-change page share the `ChangeOwnPassword` use case.
- T017: **10** events in `Flowza.Contracts.Identity.Events.V1` — exactly the rows of plan §7 (PA2 says "11 events";
  the table lists 10; to confirm with the owner). The unit of work turns the User aggregate's domain events into
  integration events, adds explicit ones (logins) and saves them with the change through Wolverine's EF Core outbox
  (`IDbContextOutbox`). Standard fields are stamped at save time; the actor of login events is the user. Unknown
  usernames only count in the `flowza.identity.login.unknown_username` metric (meter `Flowza.Identity`). No event has a
  subscriber yet except the deny-list events (T018); the others wait for the Audit service (v0.4).
- T018: `UserDenyList` in ServiceDefaults (concurrent dictionary, O(1) check; the newer change wins when events
  arrive out of order), fed by `UserDeactivated` / `UserActivated` (routed to all four services in
  `MessageRoutes.Default`) and refused inside OpenIddict validation, so a denied user's token gives 401. Start-up
  load through `IUserDenyListSource`: Identity reads its inactive users. **Other services have no start-up source
  yet**: it needs an Identity endpoint listing inactive user IDs, which is not in the approved API (PA1); to decide
  with the owner. **Found: with the SQL Server transport (the default), Wolverine creates every service's queue tables
  in that service's own database (`queues` schema), not in `flowza_messaging`, so events do not reach other services
  and the listener fails on `wolverine_<service>.wolverine_incoming_envelopes`.** The Phase 0 messaging tests use one
  database for sender and receiver and do not see it. The deny-list delivery test therefore runs on RabbitMQ; the
  SQL Server topology (ADR-0005) needs a decision by the main session / owner.
  **Fixed 2026-10-22** (ADR-0005 amendment): with `SqlServer`, `flowza_messaging` is Wolverine's main store (shared
  queues) and each service's database an ancillary store enrolled with its DbContext (outbox still in the business
  transaction). The messaging tests now give sender and receiver different databases (they failed before the fix),
  and the deny-list delivery test runs on SQL Server. Old `queues` tables left in the service databases by the bug are
  unused and harmless. **Later the same day the RabbitMQ transport was removed** (owner; ADR-0005 amendment 3): the
  T008/T009 rows below mention RabbitMQ as it was built then.
- T036 (current-user part): `HttpCurrentUser` in ServiceDefaults replaces the system user, so `CreatedBy`/`UpdatedBy`
  and event actors are the logged-in user (tested). The RabbitMQ health-check part of T036 is not done here.
- T019: `flowza init-admin --email <address> [--username admin] [--display-name ...]` runs the Identity service's own
  use case (`InitializeAdmin`) in a short-lived host: refused when any Platform Admin exists; prints the 16-character
  one-time password to the console only. Connection strings as for the services, or built from the `flowza-dev` user
  secrets on a developer PC. Wolverine runs `ServerlessWithOutbox` with listeners off, so the tool keeps the outbox
  but never takes the services' messages or joins their cluster.
- **Phase 1 verification (2026-10-21):** clean build 0 warnings; `dotnet test --solution`: 288 tests, 287 passed, 1 skipped
  (Azure Service Bus), 0 failed (exit code 8 only because `Flowza.Gateway.IntegrationTests` has no tests yet). Line
  coverage (unit + integration): Identity.Domain 97.1 %, Identity.Application 84.0 % (gate 80 %, constitution 5.6).

**Notes from T031 (CI, 2026-10-22, decisions within the approved plan):**
- `.github/workflows/ci.yml` on `ubuntu-24.04`: .NET from `global.json` → **SQL Server 2019** container
  (`.github/scripts/start-sql-server.sh`: random SA and `flowza_app` passwords per run, masked, never stored, so **no
  GitHub secret is needed**; databases from the same `docs/setup/create-databases.sql`) → restore → build (warnings are
  errors) → all tests with coverlet (Domain + Application only) → ReportGenerator merge → gate
  (`.github/scripts/check-coverage.ps1`, ≥ 80 % total line coverage; also fails when nothing was measured) → coverage
  report uploaded as a build artifact and shown in the run summary.
- ReportGenerator runs as a local .NET tool (`dotnet-tools.json`, 5.5.11; approved with the tests decision).
  Its own `minimumCoverageThresholds` setting is not available in this version, hence the small gate script.
- `--ignore-exit-code 8` because `Flowza.Gateway.IntegrationTests` has no tests until T022; remove it then.
- Dependabot: NuGet and GitHub Actions weekly, related packages grouped (Wolverine, OpenIddict, OpenTelemetry, …);
  npm joins with the web app (T023).
- **Not in this step:** web lint/tests and Playwright (no web app yet, T023–T025, T030) and the licence check (needs a
  licence-scanning tool, which is a new library for the owner to approve).
- Checked locally: same test and coverage commands (283 tests, 282 passed, 1 skipped; Domain + Application 89.9 %),
  gate passes at 80 % and fails at 95 % or without a report, scripts and YAML parse.
- **First GitHub run (2026-10-22, commit 243950f):** green in 2 min 40 s on ubuntu-24.04 with SQL Server 2019
  (SQL start 29 s, build 21 s, tests 1 min 21 s, gate passed, coverage artifact uploaded). Flowza builds and passes
  its tests on Linux too.
- **Red-run check (2026-10-22):** branch `ci/red-run-demo` with one deliberately failing test, opened as pull request #5:
  CI run 39724745438 failed at "Test with coverage" (gate skipped), shown red on the pull request. The pull request was
  closed without merging and the branch deleted. **PLT-09-AC1 ✅.**

**Notes from T020 (Gateway YARP + BFF, 2026-10-22, decisions within the approved plan):**
- Packages: `Yarp.ReverseProxy` 2.3.0 (PA6), `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.13 (first-party,
  plan §9 "OpenID Connect handlers") and `Microsoft.Extensions.ServiceDiscovery.Yarp` 10.11.0 (Aspire service discovery
  for YARP, GW6; same family and version as the approved `Microsoft.Extensions.ServiceDiscovery`).
- Routes live in the `ReverseProxy` section of `appsettings.json` (destinations `http://identity` etc., resolved by
  service discovery in development, set as real addresses in production); the web app's catch-all route to Vite is in
  `appsettings.Development.json` only. **Serving the built web files in production (GW2) is not done yet**: it comes with
  the production setup (T030).
- **The browser reaches Identity only through the Gateway** (`/connect/**`, `/account/**`, original Host kept), so the app
  has one address. The OpenID Connect handler rewrites Identity's authorize and logout addresses to the Gateway's origin;
  discovery, code exchange and refresh go directly to Identity (back channel). For this, Identity gets a **fixed issuer**
  (`Identity:Issuer` = its own address, where services check tokens); the AppHost sets it.
- The AppHost generates the Gateway's client secret at every start (`gateway-client-secret`, secret parameter) and passes
  it to both sides, plus Identity's redirect addresses for the Gateway, so no developer keeps a secret.
- Session cookie `flowza-session` (production `__Host-flowza-session`): HTTP-only, SameSite=Strict, Secure outside
  Development, browser-session (not persistent), 30 minutes sliding; the 12-hour limit counts from the login time stored
  in the cookie. It is large (it holds the tokens), so ASP.NET Core splits it into chunks, as plan §11 expected.
- No session → 401 (never a redirect): the web app goes to `/bff/login` itself. API routes require a session at the
  Gateway; they get the session's access token as `Authorization: Bearer` and **no cookies**; any Authorization header
  from the browser is dropped. Identity's pages and the web app get the browser's cookies **without** the session cookie.
- The access token is renewed when less than 2 minutes are left. Identity refusing the refresh (deactivated user,
  revoked token) ends the session; Identity unreachable keeps it, and the service then refuses the expired token.
- `/bff/login?returnUrl=` accepts only paths on the Gateway (no open redirect).
- Not in T020: roles changed after login show in `/bff/user` only after the next login (access tokens get the new roles
  at the next refresh); the deny-list check in the Gateway itself (services already refuse, T018; full check in T022);
  forwarded-proto handling in Identity for HTTPS behind the Gateway (production setup).
- CI no longer ignores exit code 8: the Gateway test project now has tests.

**Web app stream (Phase 3, parallel worktree, 2026-10-21):**
| Task | Status | Commit |
|---|---|---|
| T023 Web scaffold (+ AppHost `web` resource) | ✅ | 0e1526d |
| T024 Shell | ✅ | 0e87e65 |
| T025 API client set-up | ✅ (generation from Identity's real document waits for its OpenAPI endpoint) | dfe38a6 |

Decisions taken while building (within the approved plan; see [src/Web/README.md](../../src/Web/README.md)):
- **Versions** (exact, `save-exact`): React 19.3, Vite 8.3, antd 6.6, React Router 8.5, TanStack Query 5.104,
  i18next 26 / react-i18next 17, orval 8.43, Vitest 5, MSW 3, Prettier 3.9. **TypeScript 6.0** (not 7.0:
  typescript-eslint supports < 6.1) and **ESLint 9.39** (not 10: eslint-plugin-jsx-a11y supports ≤ 9; npm marks
  ESLint 9 as no longer supported). **jsdom 29.1** (jsdom 30 needs Node 24.15; the PC has 24.14).
- **Tooling packages that come with the approved tools** (dev only, MIT unless noted): `@vitejs/plugin-react`,
  `typescript-eslint`, `@eslint/js`, `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh`,
  `eslint-config-prettier`, `globals`, `@testing-library/user-event`, `@testing-library/jest-dom`,
  `@testing-library/dom`, `jsdom`, `@types/*`; runtime: `@ant-design/icons` (Ant Design's icon set, MIT).
  NuGet: `Aspire.Hosting.JavaScript` 13.6.2 (MIT, an Aspire hosting package, PA6).
- **No hard-coded text** is enforced with ESLint's built-in `no-restricted-syntax` (no extra plugin); tested in
  `tooling/eslint-rules.test.ts`.
- **Contract assumptions for the Gateway (T020/T021)**, all in one file each so they are easy to change:
  `GET /bff/user` → `{ displayName, roles }` with role names `PlatformAdmin`, `AppDesigner`, `User`
  (`shared/auth/systemRoles.ts`); login `GET /bff/login?returnUrl=…`, logout `GET /bff/logout` as full-page
  navigations (`shared/auth/session.ts`); anti-forgery header **`X-CSRF: 1`** on POST/PUT/PATCH/DELETE
  (`shared/api/apiFetch.ts`); web app dev server on **port 5105** (AppHost resource `web`, referenced by the Gateway).
- **API documents:** `openapi/<service>.json` saved from `/openapi/v1.json` of each running service
  (`npm run api:fetch`, development only) and committed together with the generated hooks; service paths
  `/api/v1/…` become the Gateway's `/api/<service>/v1/…`. Services must expose the document anonymously in Development.
- Arrow keys and Enter inside Ant Design menus need real browser layout, so jsdom tests cover opening, focus and
  Escape; the arrow keys were checked by hand in a browser and belong in the T029 end-to-end tests.

## Phase 0: Foundation (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T001 | **Solution skeleton:** `Flowza.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json`, `.gitignore` update; all projects from plan §4 created empty and referenced correctly | PLT-01 | Builds with 0 warnings; opens in Visual Studio 2026 |
| T002 | **Architecture tests** (NetArchTest): Domain has no EF/ASP.NET; no cross-service references; Contracts depends on nothing | Constitution 3, 4 | Tests pass on the skeleton |
| T003 | **Flowza.Common:** `Result`/`Error`, paging types, SQL Server-ordered time-based GUID generator | (T2 S4, S7; T3 D3) | Unit tests incl. ordering test against SQL Server sort order |
| T004 | **Flowza.Persistence:** audit-column interceptor (`TimeProvider` + current user), soft-delete filter, `RowVersion`, UTC converters | Constitution 8 | Unit + integration tests on `flowza_test_*` |
| T005 | **Flowza.ServiceDefaults:** OpenTelemetry, Serilog + masking policy, `/health/live` + `/health/ready`, ProblemDetails with reference code, token validation hook | PLT-05 | Unit tests for masking and reference code |
| T006 | **AppHost:** Identity, Design, Runtime, Worker, Gateway, smtp4dev (the web app joins in T023); SQL and transport connection strings from user secrets; secret-setting commands documented | PLT-02 | F5 in VS 2026 starts everything; Aspire dashboard shows all resources |
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
| T031 | **CI:** `.github/workflows/ci.yml` (SQL Server 2019 container, SQL Server transport; no RabbitMQ since 2026-10-22, coverage gate, web tests, e2e, licence check) + Dependabot | PLT-09 | Runs green once the owner creates the repository and allows the push |
| T032 | **Verify 001:** every M1 acceptance criterion has a passing named test (or signed checklist); coverage ≥ 80%; update this file's status | All M1 IDs | Traceability table below all ✅ |

## Follow-ups from the Phase 0 review (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T033 | **Production database scripts include Wolverine's tables** (outbox, inbox, deduplication; exported with Wolverine's own tooling) and Wolverine's automatic table creation is switched off outside Development | PLT-03-AC2 (PL4) | The DBA's script creates everything; a service starts in Production mode on a database prepared only by scripts |
| T034 | **Start while the database or broker is unreachable:** the service starts, reports "not ready" and recovers by itself (today start-up fails) | PLT-05, spec 001 edge cases | Integration test: unreachable database → `/health/ready` 503, then healthy once reachable |
| T035 | **No personal values in exception text in logs** (e.g. a duplicate-key error quoting an email): map known database errors to `Result` errors before they are logged; mask exception messages | PLT-05-AC3, constitution 11.2 | Do with T015; log-scan test covers a duplicate email |
| T036 | **Housekeeping:** register the HTTP current user so audit columns record the real user, not the system (with T018); ~~reuse one RabbitMQ connection in its health check~~ (obsolete: RabbitMQ removed 2026-10-22) | T3 D1, PLT-05 | Audit test with a logged-in user |

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
| A4 | **PLT-09-AC1** needs the public GitHub repository (exists since 2026-10-21) and the owner's OK to push | T031 prepares the workflow; it runs on the first allowed push |
| A5 | All other M1 criteria of spec 001 map to tasks (table below) | — |

### Traceability (M1 acceptance criteria → tasks)
| Criterion | Task(s) | Status |
|---|---|---|
| IAM-01-AC1 | T020, T022, T029 | 🟡 login, logout and refusal afterwards through the Gateway (T020, `IAM_01_AC1_*`); end-to-end in T029 |
| IAM-01-AC2 | T014 | ✅ |
| IAM-02-AC1 | T015, T024, T026 | 🟡 Users API needs Platform Admin (T015); web navigation by role and 403 page (T024, `IAM_02_AC1_*`); designer APIs arrive with feature 004; screens in T026 |
| IAM-02-AC2 | T010, T015 | ✅ |
| IAM-03-AC1 | T019, T014, T029 | 🟡 first admin + forced change tested (T019, T014); end-to-end in T029 |
| IAM-04-AC1 | T015, T026 | 🟡 API (T015); screens in T026 |
| IAM-04-AC2 | T015, T026 | 🟡 API + forced change (T015, T014); screens in T026 |
| IAM-04-AC3 | T015, T018, T022 | 🟡 Identity refuses at once (T015); deny list in every service, tested on the SQL Server transport (T018, fixed 2026-10-22); Gateway T022 |
| IAM-04-AC4 | T010, T015 | ✅ |
| IAM-04-AC5 | T010, T015 | ✅ |
| IAM-05-AC1 | T007, T022 | ✅ services (T007); Gateway refuses anonymous API calls (T020, `IAM_05_AC1_*`) |
| IAM-06-AC1 | T012 | ✅ |
| IAM-06-AC3 | T016, T027 | 🟡 API (T016); screen in T027 |
| IAM-07-AC1 | T012, T029 | 🟡 service + database (T012); end-to-end in T029 |
| IAM-07-AC2 | T021 | ⬜ |
| IAM-08-AC1 | T020, T022 | ✅ 30 minutes idle and 12 hours absolute, with a test clock (T020, `IAM_08_AC1_*`) |
| IAM-L5-AC1 | T017 | ✅ |
| PLT-02-AC1 | T006, T029 | 🟡 AppHost start verified by hand; end-to-end test in T029 |
| PLT-03-AC1 | T007 | ✅ |
| PLT-03-AC2 | T009 | ✅ |
| PLT-04-AC1 | T009 | ✅ SqlServer (RabbitMq removed 2026-10-22) |
| PLT-04-AC2 | T009 | ✅ SqlServer (RabbitMq removed 2026-10-22) |
| PLT-05-AC1 | T007 | ✅ |
| PLT-05-AC2 | T009, T024 | ✅ across services via messaging (T009); error screens show the reference code (T024, `PLT_05_AC2_*`) |
| PLT-05-AC3 | T009 | ✅ |
| PLT-06-AC1 | T030 (manual) | ⬜ |
| PLT-07-AC1 | T028 + features 002–005 (A1) | ⬜ |
| PLT-07-AC2 | T028 | ⬜ |
| PLT-09-AC1 | T031 (A4) | ✅ Green run on GitHub (run 39723977514: build, tests, 80 % gate); a deliberately failing test in pull request #5 was reported red (run 39724745438) and not merged |
