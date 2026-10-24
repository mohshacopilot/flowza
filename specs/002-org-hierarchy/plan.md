# Plan 002: Organization hierarchy (ORG)

| | |
|---|---|
| **Status** | **Approved by the owner, 2026-10-24**, including PA1–PA10 as proposed (section 14) |
| **Date** | 2026-10-24 |
| **Spec** | [spec.md](spec.md) (approved 2026-10-21, C1–C9 all "a") |
| **Inputs** | [ORG discussion](../../docs/discussions/org-hierarchy.md) (O1–O14, P1–P10, design notes in section 7), [IAM discussion](../../docs/discussions/identity-and-access.md) (L1), [WF discussion](../../docs/discussions/workflow-engine.md) (W8), [plan 001](../001-platform-foundation/plan.md), [plan 007](../007-visual-design/plan.md), [CLAUDE.md](../../CLAUDE.md) |
| **Next** | [tasks.md](tasks.md) (written 2026-10-24); spec 001 amended for C2 the same day (PA8, spec 001 amendment 1) |

> **This is the "how".** The spec says what and why; this plan chooses data, APIs, events, patterns and tests.
> Anything here that changes a contract or the database needs the owner's approval (constitution 1.6); section 14
> lists exactly those items. This feature adds **no new library**.

## 1. Approach in one paragraph
All org data lives in the **Identity service** (it already owns users), in the same database `flowza_identity`.
Feature 002 adds three small aggregates (**unit type**, **org unit**, **business role**), gives the existing **User**
aggregate a primary unit, a reporting manager and business roles, and adds an **approver-rule resolver**: seven
strategy classes, one per rule a–g, that share the "walk up", "skip the requester" and "skip inactive users" steps
and return users **plus a structured explanation**. The tree is stored as an **adjacency list plus a materialized
path** of small unit numbers, so "everything below a unit" is one index seek and "everything above" is read from
the unit's own path. Runtime (feature 005) will call the resolver over HTTP with its service token; App Designers
get read-only **lookups**. The web app gains three admin screens (org chart, unit types, business roles) and an
"Organization" section on the existing user screens. Demo data gets the example organization of the spec.

## 2. Technical context
| Item | Choice |
|---|---|
| Service | Identity (four layers, existing); database `flowza_identity`; one new EF Core migration |
| Data | EF Core 10; SQL Server 2019 compatible (level 150); no `hierarchyid`, no JSON columns needed |
| Messaging | Existing Wolverine outbox; 16 new integration events, **no subscriber in v0.1** (Audit in v0.4) |
| Security | Existing policies `PlatformAdmin`, `AppDesigner`; one new policy for service clients (PA6) |
| Web | React 19, Ant Design `Tree`, `TreeSelect`, `Select` (search), `Modal`, `Descriptions`; orval client regenerated; react-i18next |
| Tests | xUnit v3 + Shouldly (unit, integration on `flowza_test_identity` with Respawn), Vitest + RTL + MSW, Playwright + axe |
| Constraints | Windows without containers, SQL Server 2019+, no cloud-only service (NFR-04); finding approvers fits inside a sub-second task action (NFR-02) |

## 3. Constitution check
| Article | How this plan complies |
|---|---|
| 1 Spec first | Every task names ORG IDs; every acceptance criterion has a test carrying its ID (section 10) |
| 2 Generic core | Unit types, units and roles are **data**; no level names in code. The spec's example (ZONE, BRANCH, Credit Officer) appears only in tests and demo data |
| 3 Service boundaries | Org data stays inside Identity; Runtime asks through an HTTP query API (resolve) and never reads Identity's database; events carry IDs only |
| 4 Clean design | Rules in Domain, use cases as vertical slices, EF queries behind ports; Strategy + registry for the rules (section 7); DSA costs in section 8 |
| 5 Testing | Unit tests for domain rules and algorithms; resolver tests on the real database with the spec's example org; end-to-end for the screens and the keyboard (ORG-02-AC6) |
| 6 Portability | Plain tables, a sequence and `LIKE 'prefix%'` index seeks; works on SQL Server 2019 and Azure SQL |
| 7 Security | Org changes need Platform Admin; lookups need App Designer or Platform Admin (C9); resolve only for the Runtime client (PA6); record checks in handlers |
| 8 Data | `rowversion` on every aggregate; one "org structure version" row against conflicting tree changes (section 5.3); deactivate, never delete (NFR-08); UTC |
| 9 Simplicity | No cache (measure first, NFR-02); no closure table; no new library; the reference check and start-permission check wait for plan 005 (PA10) |
| 10 Observability | Resolution logs rule type, IDs and the number of users found, with the correlation ID; a metric for empty results |
| 11 Privacy | Explanations and names are never logged (NFR-07); events carry IDs only; rule g receives a user ID, never form values |
| 12 Usable by everyone | All texts, including the fixed words of explanations and reasons, from translation files (NFR-10); the tree works with the keyboard and has "Move to…" besides dragging (constitution 12.2) |

## 4. Structure (changed or new)
```
src/BuildingBlocks/Flowza.Contracts/
  Identity/Events/V1/OrgEvents.cs                 16 events (section 6)
  Identity/ApproverRules/V1/                      ApproverRule (polymorphic JSON), ResolveRequest, ResolveResult,
                                                  Explanation, ExplanationStep, OrgRef (PA2), shared with 004/005
src/Services/Identity/
  Flowza.Identity.Domain/
    Organization/OrgCode.cs                       value object (P7): letters, digits, - _, 1–50, upper case
    Organization/UnitType.cs  OrgUnit.cs  BusinessRole.cs  OrgStructure.cs   aggregates (section 5.1)
    Organization/UnitPath.cs                      materialized path value object (section 8)
    Organization/HeadWalk.cs                      shared "walk up to an active head who is not the requester"
    Organization/OrgErrors.cs  OrgDomainEvents.cs
    Users/User.cs                                 + PrimaryUnitId, ManagerId, business roles (section 5.1)
  Flowza.Identity.Application/
    Organization/UnitTypes/{Create,Rename,ChangeStatus,List}/
    Organization/Units/{Create,Rename,SetHead,Move,ChangeStatus,GetTree,GetUnit}/
    Organization/BusinessRoles/{Create,Rename,ChangeStatus,List,GetMembers,AddMember,RemoveMember}/
    Organization/Lookups/{UnitTypes,Units,BusinessRoles,Users}/       read-only for App Designers (C9)
    Users/{SetOrgDetails,GetOrgImpact}/            + changes to CreateUser, ActivateUser, InitializeAdmin, LoadDemoUsers
    ApproverRules/Resolve/                         ResolveApproverRuleHandler, registry, seven resolvers (section 7)
    Abstractions/OrgPorts.cs                       repositories, IOrgReadModel, IUnitNumberSource
  Flowza.Identity.Infrastructure/
    Persistence/OrgConfigurations.cs               tables, indexes, sequence, seed of root unit and type (C1)
    Persistence/OrgRepositories.cs  OrgReadModel.cs  UnitNumberSource.cs
    Persistence/UnitOfWork.cs                      events from every changed aggregate; duplicate-code mapping
    Migrations/<date>_OrgHierarchy.cs
  Flowza.Identity.Api/
    Organization/UnitTypeEndpoints.cs  OrgUnitEndpoints.cs  BusinessRoleEndpoints.cs  OrgLookupEndpoints.cs
    ApproverRules/ApproverRuleEndpoints.cs
    Users/UserEndpoints.cs                         + org details, org impact (section 5.4)
src/ServiceDefaults/.../Security/ApiSecurityExtensions.cs   + policies OrgReader, RuntimeService (PA6)
src/Web/src/features/admin/org/                    org chart screen, unit panel, Move-to dialog, pickers
src/Web/src/features/admin/unitTypes/              unit types screen
src/Web/src/features/admin/businessRoles/          roles list, role members
src/Web/src/features/admin/users/                  + primary unit on Create user, Organization section, warnings
src/Web/src/shared/org/                            UnitPicker, UserPicker, explanation renderer
tools/Flowza.Cli/DemoData/                          + the example organization (PLT-07, carried from 001)
tests/Flowza.Identity.UnitTests/Organization/      domain and algorithm tests
tests/Flowza.Identity.IntegrationTests/Organization/  API, database and resolver tests (example org fixture)
tests/EndToEnd/specs/05-org-hierarchy.spec.ts      admin flows, keyboard-only tree, axe; screenshots (PA9)
```

## 5. Identity service design
### 5.1 Domain
- **`OrgCode`** (value object, P7): trimmed; 1–50 characters; letters, digits, `-`, `_`; stored in upper case
  (invariant). Equality ignores case because it is always upper case. Used by unit types, units and roles.
- **`UnitType`** aggregate: `Id`, `Code` (never changes), `Name` (1–100), `IsActive`, `RowVersion`. `Rename`,
  `Deactivate(activeUnitsOfType)` refused while the count is above 0 (P9), `Activate` (C3).
- **`OrgUnit`** aggregate: `Id`, `Number` (int, from a sequence; used only inside the path), `Code` (never
  changes), `Name`, `UnitTypeId`, `ParentId` (null only for the root), `Path` (`UnitPath`, e.g. `/1/4/9/`),
  `HeadUserId?`, `IsActive`, `RowVersion`. Rules:
  - `CreateChild(parent, type, …)`: parent must be active (C4); type must be active (ORG-01-AC4); any type under
    any type (O1). The root is never created by a use case; it comes from the migration (C1).
  - `MoveTo(newParent, subtree)`: refused when the new parent is the unit itself or inside its subtree (the new
    parent's path starts with this unit's path, O(1)), when the new parent is inactive (C4), and for the root;
    rewrites the path prefix of every unit of the subtree (section 8).
  - `SetHead(user)`: the user must be active (ORG-03-AC2); may be null (vacant); may head other units and work
    elsewhere (O10, O11).
  - `Deactivate(activeChildren, activeUsers)`: refused while either list is not empty; the error carries both
    lists (O2, ORG-02-AC4). `Activate(parent)`: only while the parent is active (C3); the root has no parent.
- **`BusinessRole`** aggregate: `Id`, `Code`, `Name`, `IsActive`, `RowVersion`; rename, deactivate, activate (C3).
- **`User`** (existing aggregate) gains:
  - `PrimaryUnitId` (required) and `PlaceInUnit(unit)`: the unit must be active (ORG-04-AC1). `Create` takes the
    primary unit (C2).
  - `ManagerId?` and `SetManager(manager, chainOfManager)`: not the user (ORG-04-AC3), an active user (C7), and
    no loop (the user does not appear in the manager's chain, ORG-04-AC2). Null is valid (ORG-04-AC5).
  - `BusinessRoleIds` (owned rows, like system roles): `SetBusinessRoles(roles)`, `AddBusinessRole(role)`,
    `RemoveBusinessRole(id)`; a role that is given must be active (ORG-05-AC2); the user must be active (C7).
  - `Activate(primaryUnit)` now needs an active primary unit (C2, ORG-04-AC6).
- **`OrgStructure`**: one row with a `RowVersion`. Every change that the tree's or the reporting lines' rules depend
  on "touches" it in the same transaction: create, move, deactivate, activate a unit; set a user's primary unit or
  manager; activate a user. Two such changes at the same moment cannot both pass their checks on stale data (for
  example two moves that together would make a loop, or a manager loop built by two admins): the second gets the
  "changed by someone else" conflict (constitution 8.5) and simply tries again. Ordinary edits (rename, set head,
  roles) do not touch it.
- **`HeadWalk`** (domain service, pure): given the chain of units from a start unit up to the root (each with its
  head's ID and active flag) and the requester's ID, returns the first unit whose head is active and is not the
  requester, plus one explanation step per skipped unit (vacant, head inactive, head is the requester). Used by
  rules d and e (O3, P4, O13).
- **`ManagerChain`** (domain service, pure): walks a chain of manager IDs with a visited set and a step limit, so
  a damaged chain can never loop forever (defence in depth behind the `OrgStructure` check).

### 5.2 Data (database `flowza_identity`, one migration)
| Table | Columns | Keys and indexes |
|---|---|---|
| `UnitTypes` | Id, Code, Name, IsActive, audit columns, RowVersion | PK Id; unique `Code` |
| `OrgUnits` | Id, Number (int), Code, Name, UnitTypeId, ParentId?, Path (`varchar(900)`), HeadUserId?, IsActive, audit columns, RowVersion | PK Id; unique `Code`; unique `Number`; index `Path` (includes Id, UnitTypeId, HeadUserId, IsActive); index `ParentId`; index `HeadUserId`; FK UnitTypeId, ParentId, HeadUserId (restrict); **filtered unique index on ParentId where ParentId IS NULL** (exactly one root, ORG-02-AC5) |
| `BusinessRoles` | Id, Code, Name, IsActive, audit columns, RowVersion | PK Id; unique `Code` |
| `Users` (existing) | + PrimaryUnitId (not null), ManagerId? | index `PrimaryUnitId` (includes IsActive); index `ManagerId`; FK to OrgUnits and Users (restrict) |
| `UserBusinessRoles` | UserId, BusinessRoleId | PK (UserId, BusinessRoleId); index (BusinessRoleId, UserId) for rule f |
| `OrgStructure` | Id (fixed), RowVersion, ChangedAt | one row, seeded |
| Sequence `UnitNumbers` | int, starts at 2 | the root unit has number 1 |

- Codes are stored in upper case, so a plain unique index enforces "unique ignoring case" (P7) with any collation.
- **Seed (C1, PA5):** the migration inserts the root unit type and the root unit with fixed IDs, fixed codes, number 1
  and path `/1/`, and the `OrgStructure` row. **Existing users** (installations made with 001) get the root unit as
  primary unit in the same migration, so the "required" rule holds from the first start.
- **Path limit (PA4):** `varchar(900)` keeps the path indexable; with unit numbers of up to 10 digits it holds at
  least 81 levels. The domain refuses a unit deeper than **50 levels** with a clear message, far beyond real
  organizations (typically 4–8 levels).
- Removing a business-role member deletes its `UserBusinessRoles` row, the same as system roles in 001; the
  change is recorded by the `UserBusinessRolesChanged` event (for the audit log, v0.4). Unit types, units, roles
  and users are never deleted (NFR-08).
- Duplicate codes from a race end as the same validation error as the use case's own check: the unit of work maps
  each unique index to its error through a small table (index name → error), extended from the 001 version.

### 5.3 Key flows
**Create a unit:** load parent (active?), type (active?), check code free → `IUnitNumberSource.NextAsync()`
(`NEXT VALUE FOR UnitNumbers`) → path = parent path + number → save with `OrgStructure` touched and the
`OrgUnitCreated` event, one transaction.

**Move a unit (ORG-02-AC2/AC3/AC8):** load the unit, the new parent and the unit's subtree (tracked: `Path LIKE
'/1/4/%'`) → domain checks (not into own subtree, parent active, depth limit) → rewrite each subtree path
(`newParentPath + number + rest`) → save with `OrgStructure` touched and `OrgUnitMoved`. Moves are rare, so loading
the subtree (O(subtree)) keeps the whole change in one `SaveChanges` with row-version checks on every row.

**Deactivate a unit (ORG-02-AC4):** query active children and active users of the unit (both index seeks), first 20
names each plus totals → domain refuses with both lists in the error, or deactivates.

**Deactivate a user (ORG-04-AC4, O13):** unchanged from 001 (204). The confirm dialog first calls
`GET /users/{id}/org-impact` (direct reports and headed units) and shows the warning; deactivation then goes ahead.

**Resolve a rule (ORG-06):** see section 7. Read-only, no transaction, no tracking. Typical cost: 2–4 index seeks.

### 5.4 API (Identity, behind the Gateway at `/api/identity/…`)
Changes are 204 or 201; errors are ProblemDetails (400 validation, 404, 409 row-version conflict, 422 for a
refused deactivation with the blocking lists in `extensions`). Every change sends the `version` it last read.

| Method and path | Purpose | Who |
|---|---|---|
| `GET /api/v1/unit-types` | List (code, name, active, number of active units) | Platform Admin |
| `POST /api/v1/unit-types` · `PUT /api/v1/unit-types/{id}` | Create (code, name) · rename | Platform Admin |
| `POST /api/v1/unit-types/{id}/deactivate` · `/activate` | Status (P9, C3) | Platform Admin |
| `GET /api/v1/org-units/tree` | The whole tree as a flat list ordered by path (id, parentId, code, name, type, head, active, version) | Platform Admin |
| `GET /api/v1/org-units/{id}` | Details with type, head, active users count | Platform Admin |
| `GET /api/v1/org-units/{id}/users?page&size` | Users whose primary unit it is | Platform Admin |
| `POST /api/v1/org-units` | Create under a parent (parentId, code, name, unitTypeId) | Platform Admin |
| `PUT /api/v1/org-units/{id}` · `PUT /api/v1/org-units/{id}/head` | Rename · set or clear head | Platform Admin |
| `POST /api/v1/org-units/{id}/move` | Move under `newParentId` | Platform Admin |
| `POST /api/v1/org-units/{id}/deactivate` · `/activate` | Status (O2, C3) | Platform Admin |
| `GET /api/v1/business-roles` · `POST` · `PUT /{id}` · `POST /{id}/deactivate` · `/activate` | Roles (ORG-05) | Platform Admin |
| `GET /api/v1/business-roles/{id}/members?page&size` | Members of a role (ORG-05-AC4) | Platform Admin |
| `POST /api/v1/business-roles/{id}/members` · `DELETE /api/v1/business-roles/{id}/members/{userId}` | Add (active user, active role) · remove | Platform Admin |
| `POST /api/v1/users` (changed) | **+ `primaryUnitId` (required)** (C2) | Platform Admin |
| `GET /api/v1/users/{id}` (changed) | **+ primary unit, manager, business roles** (additive) | Platform Admin |
| `PUT /api/v1/users/{id}/org` | Set primary unit, manager, business roles (ORG-04-AC5) | Platform Admin |
| `GET /api/v1/users/{id}/org-impact` | Direct reports and headed units (O13 warning) | Platform Admin |
| `POST /api/v1/users/{id}/activate` (changed) | Refused while the primary unit is inactive (C2) | Platform Admin |
| `GET /api/v1/org-lookups/unit-types?search` · `/units?search&page&size` · `/business-roles?search` · `/users?search&page&size` | Read-only lookups: codes, names, active flag, unit parent; users **name and username only** (C9) | App Designer or Platform Admin |
| `POST /api/v1/approver-rules/resolve` | Resolve one rule for a requester (section 7) | **Runtime service client only** (PA6) |

The admin screens use the same lookups for their user and unit pickers.

## 6. Events (`Flowza.Contracts.Identity.Events.V1`, PA3)
Standard fields (EventId, OccurredAt, ActorId, CorrelationId) and **IDs only**. No service subscribes in v0.1, so
`MessageRoutes` does not change; the Audit service (v0.4) will subscribe to all of them.

| Event | Extra data |
|---|---|
| `UnitTypeCreated`, `UnitTypeRenamed`, `UnitTypeDeactivated`, `UnitTypeActivated` | UnitTypeId |
| `OrgUnitCreated` | UnitId, ParentId, UnitTypeId |
| `OrgUnitRenamed`, `OrgUnitDeactivated`, `OrgUnitActivated` | UnitId |
| `OrgUnitHeadChanged` | UnitId, HeadUserId? |
| `OrgUnitMoved` | UnitId, OldParentId, NewParentId |
| `BusinessRoleCreated`, `BusinessRoleRenamed`, `BusinessRoleDeactivated`, `BusinessRoleActivated` | BusinessRoleId |
| `UserOrgPlacementChanged` | UserId, PrimaryUnitId, ManagerId? |
| `UserBusinessRolesChanged` | UserId, BusinessRoleIds |

The unit of work today collects domain events from changed `User` aggregates only; it is widened to every changed
aggregate root, and `IdentityEventMapper` gains the new mappings.

## 7. Design patterns
| Pattern | Problem it solves here |
|---|---|
| **Strategy + registry** | Seven approver rules with different logic. `IApproverRuleResolver` per rule type (`SpecificUser`, `Requester`, `ManagerLevelsUp`, `RequesterUnitHead`, `NearestUnitOfTypeHead`, `RoleInScope`, `UserPickerField`), registered in DI and found in a dictionary keyed by rule type. A new rule type is a new class (open/closed); unknown types are a 400 |
| **Template steps shared by composition** | The same "walk up" (`HeadWalk`), "drop the requester and inactive users" (`CandidateFilter`) and explanation builder are used by every resolver, so all rules behave the same way (spec "shared rules") |
| **Specification-like value checks** | `OrgCode` and `UnitPath` value objects hold the format and path rules in one place |
| **Command (CQRS-lite)** | Each admin change is a command handler; tree, lists, lookups and resolve are no-tracking queries |
| **Result** | Refusals (loop, inactive parent, blocking children, …) are `Result` errors mapped to ProblemDetails |
| **Optimistic concurrency token** | `OrgStructure` row version serializes structural changes without locks (section 5.1) |

Not used: caching (no measured need, NFR-02), closure table, recursive CTE as the main read path, `hierarchyid`
(design notes, rejected).

**Approver rule contract (PA2)**, JSON with a `type` discriminator, codes and usernames (O9, P8):
```json
{ "type": "SpecificUser", "username": "priya" }
{ "type": "Requester" }
{ "type": "ManagerLevelsUp", "levels": 2 }
{ "type": "RequesterUnitHead" }
{ "type": "NearestUnitOfTypeHead", "unitType": "ZONE" }
{ "type": "RoleInScope", "role": "CREDIT_OFFICER", "scope": "NearestUnitOfTypeAndBelow", "unitType": "ZONE" }
{ "type": "UserPickerField", "field": "approver" }
```
`scope` is `Anywhere` | `RequesterUnit` | `RequesterUnitAndBelow` | `NearestUnitOfTypeAndBelow` (O8).
**Resolve request:** `{ requesterId, rule, pickedUserId? }`; for rule g Runtime reads the field and sends only the
picked user's ID (or none), so form values never leave Runtime (constitution 11).
**Resolve result:** `{ userIds: [...], explanation: [ { key, args } ], reason: { key, args } | null }`. Each `args`
value is an `OrgRef { kind, id, code?, name }` (user, unit, unit type or role) captured **at that moment**, so the
request history (WF-11) shows what was true then (O12). The web app turns `key` + `args` into text from the
translation files (NFR-10), e.g. `org.explain.nearestOfType` → "nearest {{unitType}}: {{unit}}", giving
"Ravi → T Nagar Branch → nearest ZONE: South Zone → head: Priya" (ORG-06-AC-x1). The server never logs it.

## 8. Data structures and algorithms
| Problem | Choice | Cost |
|---|---|---|
| Units above a unit (rules d, e, f-4, walk up) | Unit numbers read from the unit's own path, then one `WHERE Number IN (…)` query | O(depth), one index seek per level in one query |
| Units below a unit (rule f scopes 3 and 4, move, deactivate check) | `Path LIKE '/1/4/%'` on the indexed path | One index range seek, O(results) |
| Move under own subtree? | New parent's path starts with the moved unit's path | O(1) (string prefix, ≤ 900 characters) |
| Move a subtree | Rewrite the path prefix of every unit below | O(subtree size), one transaction; rare |
| Manager loop check (ORG-04-AC2) | Walk up from the proposed manager with a visited set; reject if the walk reaches the user | O(chain length), one index seek per step |
| Rule c (N levels up) | Same walk, stops at level N; an inactive manager below N still counts (C5) | O(N) |
| Walk up to an active head who is not the requester | Linear scan of the already loaded ancestor chain | O(depth) in memory |
| Rule f members | Join `UserBusinessRoles` (BusinessRoleId index) → `Users` (active, not requester) → `OrgUnits` path prefix by scope | Index seeks, O(members found) |
| Duplicate codes | Upper-case codes + unique index | O(log n) per check |
| Tree screen | Flat list ordered by path → tree built in the browser with a dictionary by ID | O(n) |

Edge-case tests for the algorithms: root only, a single unit, the deepest allowed level (50) and one beyond, a move
to the current parent, a move of the root, a chain of length 0, an N bigger than the chain, a damaged chain with a
loop (stops, never hangs).

## 9. Web app
- **Navigation** (registry, UI-03): a new group entry "Organization" under Administration with **Org chart**
  (`/admin/org`), **Unit types** (`/admin/unit-types`) and **Business roles** (`/admin/business-roles`), all
  Platform Admin.
- **Org chart:** Ant Design `Tree` on the left (lazy expand not needed: the whole tree is one call), detail card on
  the right (stacked on phones, UI-05): code, name, type, head, active users, actions **Add child unit**, **Rename**,
  **Set head** (user picker), **Move to…** (dialog with a `TreeSelect` that leaves out the unit's own subtree and
  inactive units), **Deactivate** / **Activate**. Dragging a node onto another opens the same confirm as "Move to…".
  Keyboard: arrows to move and expand, Enter to select, the actions are buttons in the detail card, so every action
  works without a mouse (ORG-02-AC6). Refusals (O2 lists, loop, inactive parent) appear in the dialog.
- **Unit types** and **Business roles:** a table with add, rename, deactivate/activate; a role opens its members
  list with **Add member** (user picker) and **Remove**.
- **Users (001 screens):** **Create user** gains a required **Primary unit** (unit picker; the root preselected only
  when it is the only unit); **User details** gains an **Organization** section (primary unit, reporting manager,
  business roles) with its own save and version; **Deactivate** shows the O13 warning list before confirming;
  **Activate** shows the C2 refusal with a link to set an active unit.
- **Shared:** `UnitPicker` (TreeSelect over the lookups), `UserPicker` (Select with server search, name and
  username), `Explanation` renderer (key + args → text; used now in tests and later by the request history in 006).
- **Texts** in the English translation files, including every explanation and reason key. Codes are typed in any
  case and shown upper case.
- **Look check (PA9):** the three new screens are added to the screenshot test at desktop, tablet and phone width;
  the new images are accepted only after the owner sees them (plan 007 PA5 flow).

## 10. Testing plan
"Example org" = the organization in spec section 3, loaded once per test class through the real use cases.

| Spec IDs | Test type | How |
|---|---|---|
| ORG-01-AC1–AC6 | Unit (`OrgCode`, `UnitType`) + integration (API) | Format and case (AC5), duplicate in another case, rename keeps code, P9 block, deactivated type refused for new units and kept by old ones, re-activation |
| ORG-02-AC1–AC5, AC7–AC9 | Unit (`OrgUnit`, `UnitPath`) + integration | Any depth and type order; move into own subtree; subtree moves with path rewrite and a rule result afterwards (AC3); O2 lists in the 422; one root (filtered unique index and API); fresh database has root unit + type with fixed codes (AC7); inactive parent (AC8); re-activation (AC9) |
| ORG-02-AC6 | End-to-end (Playwright, keyboard only) + axe | Expand, select, add child, rename, set head, Move to…, deactivate with the keyboard only |
| ORG-03-AC1–AC4 | Unit + integration | Unique unchangeable code, code format, head active, head elsewhere and of several units, rename and change head |
| ORG-04-AC1–AC7 | Unit (`User`, `ManagerChain`) + integration | Primary unit active; direct and indirect loops; self manager; O13 impact lists and deactivation succeeds; set org details; create without unit refused, first admin on root, activation refused with inactive unit (AC6); inactive manager refused (AC7) |
| ORG-05-AC1–AC5 | Unit + integration | Roles with unchangeable code; members add/remove; deactivated role not givable and ignored by rule f; members list; inactive user refused; re-activated role used by rule f again |
| ORG-06-AC-a1 … g2 | Integration on `flowza_test_identity` with the example org | One test per criterion through `POST /approver-rules/resolve` (users, explanation keys, reason key); `HeadWalk` and `ManagerChain` edge cases also as unit tests |
| ORG-06-AC-x1, x3 | Integration + Vitest | Every result has explanation steps; every empty one a reason; the web renderer turns the spec's examples into the exact sentences |
| ORG-06-AC-x2 | Integration | Resolve, change the org (move, new head), resolve again → the new result; nothing is cached. The "existing task keeps its assignee" half is tested in 005 (WF-07-AC4, APP-06-AC1) |
| ORG-06-AC-x4 | Integration | An App Designer token can call every lookup, gets name and username only for users, and gets 403 for every change endpoint and for resolve |
| Edge cases (spec section 6) | Integration | Concurrent edit → 409; two concurrent structural changes → second 409 (`OrgStructure`); lower-case code clash; root deactivation blocked |
| NFR-07 | Integration (log scan, existing) | Resolve and org changes with seeded names leave no names in logs |
| NFR-02 | Integration | Resolve on a generated tree of 5,000 units and 10,000 users stays under 100 ms per call |
| PLT-07 (carried) | Integration | `flowza demo-data` loads the example org with its users, heads, managers and roles on empty databases |
| Web | Vitest + RTL + MSW | Tree built from the flat list, Move-to options leave out the subtree, pickers, O13 and C2 messages, explanation renderer |

## 11. Changes to feature 001 (C2, IAM L1)
- `CreateUser` and the Create user screen: **primary unit required**. `InitializeAdmin` (`flowza init-admin`) puts
  the first admin in the root unit. `LoadDemoUsers` takes a unit per user. `ActivateUser`: active primary unit
  required. 001 tests and end-to-end tests that create users get a primary unit.
- **Spec 001 amendment (PA8):** IAM-04 gains "a new user needs a primary unit (002 C2); the first admin gets the
  root unit", with the same acceptance criterion ID as 002 (ORG-04-AC6) referenced, so 001 stays the single place
  for the user screens.

## 12. Build order (for `tasks.md`)
All of 002 is Milestone 1.
1. Domain: `OrgCode`, `UnitPath`, aggregates, `User` additions, `HeadWalk`, `ManagerChain` + unit tests.
2. Data: configurations, sequence, seed, migration with the user back-fill; unit of work widened; events.
3. Unit types and units API (create, rename, head, move, status, tree) + integration tests.
4. Business roles and user org details API, org impact, 001 changes (create, activate, init-admin) + tests.
5. Lookups, policies, resolve with the seven resolvers + example-org resolver tests.
6. Web: unit types, business roles, users' Organization section, Create user field, warnings.
7. Web: org chart screen with keyboard, Move to…, dragging.
8. Demo data (example org), end-to-end tests, screenshots for the owner, spec 001 amendment, verify (all ORG
   criteria have named passing tests; coverage ≥ 80 %).

## 13. Local development and CI
No new configuration, program or database. The Runtime client already exists in `Identity:Clients` (001); feature
005 will call resolve with it. CI is unchanged apart from the new tests and screenshots.

## 14. Items approved by the owner (constitution 1.6, approved 2026-10-24)
| # | Item | Proposal |
|---|---|---|
| PA1 | **Identity API additions and changes** | Section 5.4, including **`primaryUnitId` required on create user** (a change of the 001 contract, used only by our web app and tests), the extended user details, `org-impact`, and 422 with lists for refused deactivations |
| PA2 | **Approver rule and resolve contract** | Section 7: JSON shapes of rules a–g in `Flowza.Contracts` (shared with Design and Runtime), resolve request and result, explanation as translation keys + `OrgRef` arguments captured at that moment |
| PA3 | **Event contracts** | Section 6: 16 events, IDs only, no subscriber until the Audit service (v0.4) |
| PA4 | **Database design** | Section 5.2: four new tables, two columns on `Users`, the `UnitNumbers` sequence, the `OrgStructure` row, path `varchar(900)` with a **limit of 50 levels**, membership rows removed on "Remove member", back-fill of existing users to the root unit |
| PA5 | **Fixed codes of the installed root (C1)** | Root unit type code `ROOT`, name "Organization"; root unit code `ROOT`, name "Organization"; the admin renames both (the demo data renames them to "Head Office") |
| PA6 | **Who may call resolve** | A new policy that accepts only the `flowza-runtime` client's token (client credentials); plus `OrgReader` = App Designer or Platform Admin for lookups. "Test a rule" for admins comes with ORG-07 in v0.2 |
| PA7 | **No new library** | Ant Design `Tree` (keyboard and dragging built in), `TreeSelect`, `Select`; no dnd-kit here |
| PA8 | **Spec 001 amendment** | Section 11 wording, done after this plan is approved |
| PA9 | **New screenshots** | Org chart, unit types and business roles at three widths join the look check; accepted after the owner sees them |
| PA10 | **Left for plan 005** (confirmed) | Identity's endpoints for the publish-time reference check (C8, WF-04-AC5) and for the start-permission check (WF-06) are designed in plan 005, next to their only callers, instead of being built now without a user (constitution 9.1) |

## 15. Risks
| Risk | Mitigation |
|---|---|
| Ant Design's `Tree` keyboard support is not complete enough for ORG-02-AC6 | First web task checks it with the keyboard-only test; if it falls short, a small own tree view following the WAI-ARIA tree pattern (no new library) |
| Two admins change the structure at the same moment | `OrgStructure` row version: the second gets a clear conflict and tries again; tested |
| A large subtree move is slow | Moves are rare and the tree is small (thousands); the NFR-02 test also times a move of 1,000 units |
| Making the primary unit required breaks 001 tests and the CLI | Done in one task (build order step 4) with all 001 tests green before moving on |
| Names in explanations are personal data | Never logged (log-scan test); stored only in Runtime's request history, which already shows who acted |
| Path limit surprises an installation | 50 levels is far beyond real trees; the message says so; raising it later is a one-line change while the column fits 81 |

## 16. Estimate
The scope check gave **1–1.5 build days** for ORG. With the three admin screens, the keyboard-operable tree, the
001 changes and the example-org demo data, this plan comes to **about 2 build days**: domain + data 0.5, APIs +
resolver 0.75, web 0.5, demo data + end-to-end + verify 0.25.
