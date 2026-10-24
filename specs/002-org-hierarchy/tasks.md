# Tasks 002: Organization hierarchy (ORG)

| | |
|---|---|
| **Status** | **Building** (written and analyzed 2026-10-24); T001–T002 done; next: T003 |
| **Spec / Plan** | [spec.md](spec.md) (approved 2026-10-21, C1–C9) · [plan.md](plan.md) (approved 2026-10-24, PA1–PA10) · [spec 001 amendment 1](../001-platform-foundation/spec.md) (section 12, primary unit) |
| **Rules** | One task at a time, in order. Each task ends with: builds without warnings, lint clean, its tests pass (and all earlier tests still pass), a local commit (`feat|test|docs(identity|web|cli): … (IDs)`). Nothing is pushed without the owner's OK. Every task is Milestone 1 |

## Before coding: owner's steps
| # | Step | Needed from |
|---|---|---|
| O1 | Allow a **push** to GitHub, so CI can make the screenshots of the three new screens (PA9) | T018 |
| O2 | **Look at the new screenshots** before they are committed (plan 007 PA5 flow) | T018 |
| O3 | *(Optional)* To see the example organization on the dev PC: empty the dev databases and run `flowza demo-data` (it refuses databases that already hold data, PLT-07) | After T017 |

Nothing else: no new database, secret, library or program. The new migration runs by itself in development and
puts every existing user into the root unit (plan §5.2).

## Progress
| Task | Status | Commit |
|---|---|---|
| T001 Codes, unit types and business roles (domain) | ✅ | 01301f8 |
| T002 Org units, paths and the org structure version (domain) | ✅ | a0b1686 |
| T003 User's place in the organization, manager chain, head walk (domain) | ⬜ | |
| T004 Contracts: org events and the approver rule contract | ⬜ | |
| T005 Database: tables, sequence, root seed, back-fill | ⬜ | |
| T006 Unit types API | ⬜ | |
| T007 Org units API: create, rename, head, tree, details | ⬜ | |
| T008 Org units API: move, deactivate, activate | ⬜ | |
| T009 Business roles API and members | ⬜ | |
| T010 Users' org details, org impact and the 001 changes | ⬜ | |
| T011 Lookups and the two new policies | ⬜ | |
| T012 Resolve API and rules a, b, c, g | ⬜ | |
| T013 Rules d, e, f, timing and privacy | ⬜ | |
| T014 Web: client, pickers, explanation, unit types and roles screens | ⬜ | |
| T015 Web: the users screens' organization parts | ⬜ | |
| T016 Web: org chart screen | ⬜ | |
| T017 Demo data: the example organization | ⬜ | |
| T018 End-to-end, keyboard and look check | ⬜ | |
| T019 Verify 002 | ⬜ | |

**Progress notes**
- **T001 (2026-10-24):** decisions inside plan §5.1:
  - **Codes are ASCII only** (letters A–Z, digits, `-`, `_`), the same rule as usernames (001 C4): P7 says "letters",
    and letters from other scripts could imitate another code (a Greek `Ζ` looks like `Z`; test
    `ORG_01_AC5_codes_outside_the_format_are_rejected`). Upper case is stored, so equal codes are equal records.
  - **`OrgName`** (1–100 characters, trimmed) is one small shared check for unit types, units and roles, so the three
    aggregates do not each repeat it; no shared base class (composition over inheritance).
  - Deactivating or activating twice is a no-op without an event (as for users in 001). The P9 message does not
    quote the number of units; the API (T006) can add the list if the owner wants it.
  - Tests: 21 new unit tests (`OrgCodeTests`, `UnitTypeTests`, `BusinessRoleTests`); breaking the upper-casing and
    the P9 check for a moment made 8 of them fail. Identity unit tests 122/122, architecture tests 29/29, build 0
    warnings.
- **T002 (2026-10-24):** decisions inside plan §§5.1, 5.4, 8:
  - **Rule refusals are `Error.Failure` (HTTP 422)**, as plan §5.4 asks for refused deactivations; 409 stays only for
    "changed by someone else", so the web app can treat 409 as "reload". T001's `UnitTypeInUse` moved from `Conflict`
    to `Failure` for the same reason (no API used it yet).
  - **`Deactivate` takes counts, not lists:** `Error` carries a code, a message and field errors, no payload. The
    handler (T008) has already queried the blocking units and users, so it names them in the 422 answer; the domain
    rule stays "both must be 0".
  - **Depth 50 counts the root as level 1.** The path keeps a trailing `/`, so `/1/4/` is never a prefix of `/1/42/`.
    A move works out every new path first, so a subtree that would end too deep changes nothing. Children keep their
    `ParentId`; only their paths change, and only the moved unit raises `OrgUnitMoved`.
  - **Order of the move checks:** root, own subtree, inactive target (only for an active unit: an inactive unit may
    move under an inactive one, C4 speaks of active units), same parent (no-op), depth.
  - **`OrgStructure.Touch`** increments a counter as well as the time, so two touches in the same instant still write
    the row and its row version always changes.
  - **`SetHead(User?)`** takes the user aggregate (same domain), so "active" is checked on the real user.
  - Tests: 40 new (`UnitPathTests`, `OrgUnitTests`) on part of the spec's example tree; removing the own-subtree check
    and the inactive-parent check for a moment made 2 of them fail. Identity unit tests 162/162, architecture 29/29,
    build 0 warnings.

## Phase 1: Domain (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T001 | **Codes, unit types, business roles:** `Organization/OrgCode` (trim, 1–50, letters, digits, `-`, `_`, upper case invariant, P7); `UnitType` and `BusinessRole` aggregates (`Create`, `Rename` 1–100, `Deactivate(activeUnitsOfType)` for unit types (P9), `Deactivate`/`Activate` for both, `IAuditable`, `IVersioned`); `OrgErrors`; domain events for create, rename, deactivate, activate | ORG-01, ORG-05, P7, P9, C3 | Unit tests `ORG_01_AC5_*` (format, length, lower case stored upper case), `ORG_01_AC2_*` (no way to change the code, rename works), `ORG_01_AC3_*` (refused while active units use it), `ORG_01_AC6_*`, `ORG_05_AC3_*`, `ORG_05_AC5_*` (re-activation) |
| T002 | **Org units:** `UnitPath` (parse, append, starts-with, depth, rewrite prefix; limit 50 levels); `OrgUnit` aggregate (`CreateChild` with active parent C4 and active type ORG-01-AC4, any type under any type O1; `Rename`; `SetHead` active user or vacant; `MoveTo` refusing self, own subtree, inactive parent, the root, too deep, and rewriting the subtree's paths; `Deactivate(activeChildren, activeUsers)` with both lists in the error; `Activate(parent)` only under an active parent); `OrgStructure` with `Touch(now)` | ORG-02, ORG-03, O1, O2, C3, C4 | Unit tests `ORG_02_AC1_*`, `ORG_02_AC2_*`, `ORG_02_AC4_*`, `ORG_02_AC8_*`, `ORG_02_AC9_*`, `ORG_03_AC2_*`, `ORG_03_AC3_*`, `ORG_01_AC4_*` (inactive type refused); path edge cases (root only, one unit, depth 50 and 51, move to the current parent, move of the root) |
| T003 | **The user's place:** `User` gains `PrimaryUnitId` (`Create` takes it; `PlaceInUnit` active unit), `ManagerId` (`SetManager`: not self, active, no loop through the given chain; null allowed), business roles (`SetBusinessRoles`, `AddBusinessRole`, `RemoveBusinessRole`; role active, user active); `Activate` needs an active primary unit (C2); `ManagerChain` (visited set, step limit); `HeadWalk` (first unit with an active head who is not the requester, one explanation step per skipped unit); new domain events | ORG-04, ORG-05, ORG-06 shared rules, C2, C5, C7, O3, O13, P4 | Unit tests `ORG_04_AC1_*`, `ORG_04_AC2_*` (direct and through a chain), `ORG_04_AC3_*`, `ORG_04_AC5_*`, `ORG_04_AC6_*` (activation refused), `ORG_04_AC7_*`, `ORG_05_AC2_*` (deactivated role not givable), `ORG_05_AC5_*` (inactive user refused); `ManagerChain` and `HeadWalk` edge cases (empty chain, damaged chain with a loop stops, all vacant, head is the requester at every level); the 001 `UserTests` updated and green |

## Phase 2: Contracts and data (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T004 | **Contracts:** `Flowza.Contracts.Identity.Events.V1/OrgEvents.cs` (the 16 events of plan §6, IDs only); `Flowza.Contracts.Identity.ApproverRules.V1` (`ApproverRule` with `[JsonPolymorphic]` on `type` and the seven rule records, `RoleScope`, `ResolveRequest`, `ResolveResult`, `ExplanationStep`, `OrgRef`, PA2); `IdentityEventMapper` maps the new domain events; `UnitOfWork` takes events from **every** changed aggregate root and maps unique indexes to errors through a table (index name → error) | ORG-01–ORG-06 (PA2, PA3) | Unit tests: every rule shape of plan §7 round-trips through JSON; unknown `type` fails; every event has IDs only (reflection test: no `string` property on any org event); integration: a unit-type change publishes its event through the outbox; the existing `IdentityEventsTests` still pass |
| T005 | **Database:** `OrgConfigurations` (plan §5.2: `UnitTypes`, `OrgUnits`, `BusinessRoles`, `UserBusinessRoles`, `OrgStructure`, two `Users` columns, indexes, filtered unique index for the one root, sequence `UnitNumbers` from 2); seed of the root unit type and root unit (`ROOT`/"Organization", fixed IDs, number 1, path `/1/`, PA5) and the `OrgStructure` row; migration `OrgHierarchy` that back-fills existing users to the root unit; repositories, `UnitNumberSource`; production database script still generated and idempotent | ORG-02-AC5, ORG-02-AC7, C1, C2 (PA4, PA5) | Integration on `flowza_test_identity`: `ORG_02_AC7_*` (fresh database: one root unit and one unit type with the fixed codes); `ORG_02_AC5_*` (a second root is refused by the database); migration on a database with 001 users gives each the root unit; the PLT-03 migration and script tests pass |

## Phase 3: Admin APIs (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T006 | **Unit types API:** slices `UnitTypes/{Create,Rename,ChangeStatus,List}`; endpoints of plan §5.4 (Platform Admin); list with the number of active units; version on every change | ORG-01 | Integration `ORG_01_AC1_*` (create; duplicate in another case → validation error, also from a race through the index map), `ORG_01_AC2_*`, `ORG_01_AC3_*`, `ORG_01_AC5_*`, `ORG_01_AC6_*`; stale version → 409; non-admin → 403 |
| T007 | **Org units API, part 1:** slices `Units/{Create,Rename,SetHead,GetTree,GetUnit,ListUnitUsers}`; create takes the next unit number and touches `OrgStructure`; tree as a flat list ordered by path | ORG-02, ORG-03 | Integration `ORG_02_AC1_*` (five levels, types in any order), `ORG_02_AC8_*` (create under an inactive unit refused), `ORG_03_AC1_*` (unique, no way to change), `ORG_03_AC2_*` (inactive head refused; head of another unit's staff; head of two units), `ORG_03_AC3_*`, `ORG_03_AC4_*`, `ORG_01_AC4_*` (inactive type refused, old units keep it); two admins renaming the same unit → second 409 |
| T008 | **Org units API, part 2:** slices `Units/{Move,ChangeStatus}`; move loads the subtree and rewrites paths in one save; deactivate answers **422 with the active child units and active users** (first 20 of each and totals); activate under an active parent only; every structural change touches `OrgStructure` | ORG-02, O2, C3, C4 | Integration `ORG_02_AC2_*` (under itself, under a grandchild), `ORG_02_AC3_*` (the subtree's paths follow the move; the rule half is in T013), `ORG_02_AC4_*` (both lists named), `ORG_02_AC8_*` (move under an inactive unit), `ORG_02_AC9_*`; root: no move, deactivation blocked while it has active children or users; **two structural changes at the same moment → the second gets 409** (e.g. A under B and B under A started together) |
| T009 | **Business roles API:** slices `BusinessRoles/{Create,Rename,ChangeStatus,List,GetMembers,AddMember,RemoveMember}`; members change through the `User` aggregate (`UserBusinessRolesChanged`) | ORG-05, C3, C7, P10 | Integration `ORG_05_AC1_*` (unique unchangeable code, add and remove members), `ORG_05_AC2_*` (deactivated role cannot be given), `ORG_05_AC3_*`, `ORG_05_AC4_*` (list and paged members), `ORG_05_AC5_*` (inactive user refused; re-activation) |
| T010 | **Users' org details and the 001 changes (spec 001 amendment 1):** `CreateUser` requires `primaryUnitId` (active); `InitializeAdmin` uses the root unit; `ActivateUser` refuses an inactive primary unit; `LoadDemoUsers` takes a unit per user (root for today's demo users until T017); `GetUser` returns primary unit, manager and business roles; slices `Users/SetOrgDetails` (`PUT /users/{id}/org`, touches `OrgStructure` when unit or manager changes) and `Users/GetOrgImpact`; all 001 integration and CLI tests updated to give users a primary unit | ORG-04, C2, C7, O13 (PA1, PA8) | Integration `ORG_04_AC1_*`, `ORG_04_AC2_*`, `ORG_04_AC3_*`, `ORG_04_AC4_*` (impact lists direct reports and headed units; deactivation then succeeds), `ORG_04_AC5_*`, `ORG_04_AC6_*` (create without unit refused; `init-admin` gives the root unit; activation refused until an active unit is set), `ORG_04_AC7_*`; **every 001 test still passes** |
| T011 | **Lookups and policies:** `ApiSecurityExtensions` gains `OrgReader` (App Designer or Platform Admin) and `RuntimeService` (token of the `flowza-runtime` client only, PA6); slices `Lookups/{UnitTypes,Units,BusinessRoles,Users}` (users: id, name, username, active only); endpoints under `/api/v1/org-lookups` | ORG-06 (C9; PA6) | Integration `ORG_06_AC_x4_*`: an App Designer reads every lookup and sees only name and username of users; every change endpoint of T006–T010 answers 403 to them; a User without either role gets 403 on lookups; policy tests for `RuntimeService` (Runtime client allowed; Design client, admin user and designer refused) |

## Phase 4: Finding approvers (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T012 | **Resolve API and the first rules:** `IOrgReadModel` (no-tracking queries: requester, ancestor chain from the path, manager by ID, role members by scope); `IApproverRuleResolver` + registry keyed by rule type; `CandidateFilter`; explanation builder (keys + `OrgRef` captured now); resolvers **a** `SpecificUser`, **b** `Requester`, **c** `ManagerLevelsUp`, **g** `UserPickerField` (empty field → empty with reason); `POST /api/v1/approver-rules/resolve` (`RuntimeService`; unknown rule type or missing requester → 400); test fixture **`ExampleOrganization`** (spec section 3, loaded once per class through the real use cases) | ORG-06 a, b, c, g, x1, x3 (P1, P2, P3, C5, C6) | Integration `ORG_06_AC_a1_*`, `ORG_06_AC_a2_*`, `ORG_06_AC_b1_*`, `ORG_06_AC_b2_*`, `ORG_06_AC_c1_*` … `ORG_06_AC_c4_*`, `ORG_06_AC_g1_*`, `ORG_06_AC_g2_*`; `ORG_06_AC_x1_*` and `ORG_06_AC_x3_*` for these rules (explanation steps present; every empty result has a reason key) |
| T013 | **The unit rules, timing and privacy:** resolvers **d** `RequesterUnitHead`, **e** `NearestUnitOfTypeHead` (both through `HeadWalk`), **f** `RoleInScope` with the four scopes (deactivated role → empty with reason); log line per resolution (rule type, IDs, count, never names); empty-result metric | ORG-06 d, e, f, x1–x3; ORG-02-AC3 (O3, O6, O8, P4, P5, P6, P10, NFR-02, NFR-07) | Integration `ORG_06_AC_d1_*` … `ORG_06_AC_d5_*`, `ORG_06_AC_e1_*` … `ORG_06_AC_e5_*`, `ORG_06_AC_f1_*` … `ORG_06_AC_f6_*`, `ORG_05_AC2_*` (rule f ignores a deactivated role), `ORG_05_AC5_*` (and uses it again after re-activation); `ORG_06_AC_x1_*`, `ORG_06_AC_x3_*` for d, e, f; `ORG_06_AC_x2_*` and `ORG_02_AC3_*` (rule half): resolve, move Chennai Region under North Zone / change a head, resolve again → the new result; NFR-02: generated tree of 5,000 units and 10,000 users, each rule under 100 ms; a move of 1,000 units timed; NFR-07 log scan: no seeded names in the logs of resolutions and org changes |

## Phase 5: Web app (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T014 | **Client, shared parts, two list screens:** `npm run api:update`; navigation entries Org chart, Unit types, Business roles (Administration, Platform Admin); routes and page titles; `shared/org/UserPicker` (server search), `UnitPicker` (`TreeSelect` from the lookups), `Explanation` renderer (key + args → text); texts in `locales/en` for screens, errors, explanation and reason keys; **Unit types** screen (table, add, rename, deactivate/activate with the P9 message); **Business roles** screen and **Role members** (add with the user picker, remove, paged) | ORG-01, ORG-05, ORG-06-AC-x1, ORG-06-AC-x3 (NFR-10, NFR-11) | Vitest + RTL + MSW: `ORG_06_AC_x1_*` (the renderer turns the spec's example into "Ravi → T Nagar Branch → nearest ZONE: South Zone → head: Priya"), `ORG_06_AC_x3_*` ("No unit of type ZONE above T Nagar Branch"); `ORG_01_AC1_*`, `ORG_01_AC3_*` (message shown), `ORG_05_AC1_*`, `ORG_05_AC4_*` screen tests; every field labelled; translations only from `locales` (lint) |
| T015 | **Users screens:** Create user with a required **Primary unit**; User details **Organization** section (primary unit, reporting manager, business roles; own save with version; loop, inactive manager and inactive unit errors on the fields); **Deactivate** dialog lists direct reports and headed units before confirming (O13); **Activate** shows the C2 refusal | ORG-04 (spec 001 amendment 1) | Vitest `ORG_04_AC4_*` (warning lists both), `ORG_04_AC5_*`, `ORG_04_AC6_*` (create refused without a unit; activation message), `ORG_04_AC2_*` and `ORG_04_AC7_*` (server errors on the manager field); the existing users screen tests pass |
| T016 | **Org chart screen:** **first** a keyboard check of Ant Design `Tree` (arrows, expand, Enter, focus visible, screen-reader names); if it falls short, a small own tree view on the WAI-ARIA tree pattern (plan §15). Then: tree from the flat list (dictionary by ID, O(n)), detail card (code, name, type, head, active users), **Add child unit**, **Rename**, **Set head**, **Move to…** (`TreeSelect` without the own subtree and inactive units), dragging onto a node opens the same confirm, **Deactivate**/**Activate** with the O2 lists, C4 and loop messages; detail card stacks under the tree on phones | ORG-02, ORG-03 (constitution 12.2, UI-05) | Vitest `ORG_02_AC6_*` (each action reachable and done with the keyboard only in the component), `ORG_02_AC2_*` (Move-to options leave out the subtree), `ORG_02_AC4_*` (both lists shown), `ORG_03_AC4_*`; the keyboard check's outcome recorded in the progress notes |

## Phase 6: Demo data and verification (M1)
| ID | Task | Spec IDs | Done when |
|---|---|---|---|
| T017 | **Demo data:** `flowza demo-data` loads the spec's example organization through Identity's use cases: root renamed "Head Office", unit types (HO = root type renamed, ZONE, REGION, BRANCH, DEPT), the seven units, heads, demo users for Lakshmi, Priya, Suresh, Arun, Ravi, Vikram, Meena, Kumar (fictional, `demo.example` addresses) with primary units and managers, role `CREDIT_OFFICER`; Adyar Branch vacant; today's demo admin and designer stay in Head Office; setup guide lists the example org | PLT-07 (carried from 001, analysis A1 of 001) | Integration `PLT_07_AC1_*` extended: on empty databases the example org is loaded and **rule results match the spec's examples** (Ravi + rule d → Arun; rule e ZONE → Priya); refusal on non-empty databases unchanged |
| T018 | **End-to-end and look check:** `05-org-hierarchy.spec.ts`: admin builds a small tree, sets a head, moves a unit, is refused moving it into its own subtree, deactivation shows the O2 list; creates a user with a primary unit; deactivating a manager shows the O13 warning; **keyboard only** through every tree action; axe on every new screen. `07-visual-design.spec.ts` gains org chart, unit types and business roles at three widths (no sideways scrolling, axe, screenshots). After O1: run "Update screenshots", show the new images to the owner (O2), commit them on a branch first (setup guide §5.3) | ORG-02-AC6, ORG-04-AC4, UI-05, UI-06, UI-09 (PA9) | `ORG_02_AC6_*` end-to-end passes with the keyboard only; the other flows pass; axe clean; the full earlier end-to-end suite passes; CI green with the owner-approved screenshots |
| T019 | **Verify 002:** `run-and-verify` (platform ready, all .NET, web and end-to-end tests); coverage ≥ 80 % for Identity Domain + Application; traceability table all ✅; code review by the `code-reviewer` agent and its findings handled; progress log updated | All ORG IDs | Traceability below all ✅; review findings closed; status of this file "done" |

## Carried to later features
| Item | Where |
|---|---|
| Runtime calls `POST /approver-rules/resolve` when it creates a task; the "existing task keeps its assignee" half of ORG-06-AC-x2 | Feature 005 (WF-07-AC4, APP-06-AC1) |
| Publish-time reference check of codes and usernames (C8) and the start-permission check (WF-06): their Identity endpoints | Plan 005 (PA10) |
| Unassigned queue showing the reason, reassign | Feature 006 (INB Z2) |
| Request history showing the explanation (uses the T014 renderer) | Features 005/006 (WF-11) |
| "Test a rule" admin screen | ORG-07, v0.2 |
| The two reference apps' roles and processes on top of the example org | Features 004/005 (PLT-07) |

## Analysis (spec ↔ plan ↔ tasks)
| # | Finding | Resolution |
|---|---|---|
| A1 | **ORG-06-AC-x2** has two halves: rules use the org data of the moment (002) and existing tasks keep their assignee (005, no tasks exist yet) | T013 tests the first half; the second is carried to 005 (WF-07-AC4, one test may carry both IDs, as APP-06-AC1 allows) |
| A2 | **ORG-02-AC3** ("rule results afterwards reflect the new position") needs the resolver, which comes after the move | T008 tests the subtree's paths; T013 adds the rule half with the same ID |
| A3 | **ORG-04-AC4** "shows a warning": the server gives the lists, the screen shows them | T010 (impact endpoint + deactivation succeeds), T015 (dialog), T018 (end-to-end) |
| A4 | **ORG-06** mentions the publish check (C8) and start permissions; neither has a 002 acceptance criterion and their callers come in 005 | PA10: designed in plan 005; the lookups for start permissions (C9) are built now (T011) |
| A5 | **ORG-02-AC6** depends on the tree component's keyboard support (plan risk) | T016 starts with the keyboard check and has the own-tree fallback; T018 proves it end to end |
| A6 | Spec edge cases without their own criterion: concurrent edits, root deactivation, lower-case codes, empty user-picker field, a user heading several units | Tested in T006–T008 (409, root), T001/T006 (case), T012 (empty field), T007/T013 (several units, rules start from the primary unit) |
| A7 | **ORG-04-AC1** for users created before 002 | T005 back-fills them to the root unit in the migration |
| A8 | Spec 001 amendment 1 changes 001 behaviour (create, activate, init-admin) | One task (T010) with every 001 test updated and green; the IAM IDs keep their tests |
| A9 | "Readable explanation" (ORG-06-AC-x1/x3) is structured on the server (PA2) | Server tests check steps and keys (T012, T013); the web renderer test checks the spec's exact sentences (T014) |
| A10 | NFR-02, NFR-07, NFR-10 have no ORG acceptance criterion | Timing and log-scan tests in T013; translation keys and lint in T014 |
| A11 | Every M1 acceptance criterion of spec 002 maps to at least one task (table below); ORG-07 is v0.2 and has no task | — |

### Traceability (acceptance criteria → tasks)
| Criterion | Task(s) | Status |
|---|---|---|
| ORG-01-AC1 | T001, T006, T014 | 🟡 domain: created active with code and name, name required (`ORG_01_AC1_*`, T001); duplicate rejection in T006, screen in T014 |
| ORG-01-AC2 | T001, T006 | 🟡 domain: rename keeps the code, no public way to change it (`ORG_01_AC2_*`, T001); API in T006 |
| ORG-01-AC3 | T001, T006, T014 | 🟡 domain: refused while active units use it (`ORG_01_AC3_*`, T001); the count from the database in T006 |
| ORG-01-AC4 | T002, T007 | 🟡 domain: inactive type refused for a new unit, existing units keep it (`ORG_01_AC4_*`, T002); API in T007 |
| ORG-01-AC5 | T001, T006 | 🟡 domain: format, length, lower case stored upper case and equal (`ORG_01_AC5_*`, T001); the clash in the database in T006 |
| ORG-01-AC6 | T001, T006 | 🟡 domain: re-activation (`ORG_01_AC6_*`, T001); "choosable again" in T006/T007 |
| ORG-02-AC1 | T002, T007 | 🟡 domain: any type under any type down to level 50 (`ORG_02_AC1_*`, T002); API in T007 |
| ORG-02-AC2 | T002, T008, T016, T018 | 🟡 domain: under itself, a child, a grandchild refused, nothing changed (`ORG_02_AC2_*`, T002); API T008, screen T016/T018 |
| ORG-02-AC3 | T002, T008, T013 (A2) | 🟡 domain: the whole subtree's paths follow the move (`ORG_02_AC3_*`, T002); saved in T008, rule results in T013 |
| ORG-02-AC4 | T002, T008, T016, T018 | 🟡 domain: refused with active units or users (`ORG_02_AC4_*`, T002); the names in the answer T008, screen T016/T018 |
| ORG-02-AC5 | T005, T008 | ⬜ |
| ORG-02-AC6 | T016, T018 (A5) | ⬜ |
| ORG-02-AC7 | T005 | ⬜ |
| ORG-02-AC8 | T002, T007, T008 | 🟡 domain: create and move under an inactive unit refused (`ORG_02_AC8_*`, T002); API T007/T008 |
| ORG-02-AC9 | T002, T008 | 🟡 domain: re-activation under an active parent, refused under an inactive one (`ORG_02_AC9_*`, T002); API T008 |
| ORG-03-AC1 | T007 | ⬜ |
| ORG-03-AC2 | T002, T007 | 🟡 domain: inactive head refused, one user heads two units (`ORG_03_AC2_*`, T002); API T007 |
| ORG-03-AC3 | T002, T007 | 🟡 domain: format and case (`ORG_03_AC3_*`, T002); the database clash in T007 |
| ORG-03-AC4 | T002, T007, T016 | 🟡 domain: rename keeps the code, head set, changed and cleared (`ORG_03_AC4_*`, T002); API T007, screen T016 |
| ORG-04-AC1 | T003, T005 (A7), T010 | ⬜ |
| ORG-04-AC2 | T003, T010, T015 | ⬜ |
| ORG-04-AC3 | T003, T010 | ⬜ |
| ORG-04-AC4 | T010, T015, T018 (A3) | ⬜ |
| ORG-04-AC5 | T003, T010, T015 | ⬜ |
| ORG-04-AC6 | T003, T010, T015 (A8) | ⬜ |
| ORG-04-AC7 | T003, T010, T015 | ⬜ |
| ORG-05-AC1 | T009, T014 | ⬜ |
| ORG-05-AC2 | T003, T009, T013 | ⬜ |
| ORG-05-AC3 | T001, T009 | 🟡 domain: format and case (`ORG_05_AC3_*`, T001); API in T009 |
| ORG-05-AC4 | T009, T014 | ⬜ |
| ORG-05-AC5 | T001, T003, T009, T013 | 🟡 domain: role re-activation (`ORG_05_AC5_*`, T001); inactive member T003, API T009, rule f T013 |
| ORG-06-AC-a1, a2 | T012 | ⬜ |
| ORG-06-AC-b1, b2 | T012 | ⬜ |
| ORG-06-AC-c1 … c4 | T003, T012 | ⬜ |
| ORG-06-AC-d1 … d5 | T003, T013 | ⬜ |
| ORG-06-AC-e1 … e5 | T003, T013 | ⬜ |
| ORG-06-AC-f1 … f6 | T013 | ⬜ |
| ORG-06-AC-g1, g2 | T012 | ⬜ |
| ORG-06-AC-x1 | T012, T013, T014 (A9) | ⬜ |
| ORG-06-AC-x2 | T013; second half in 005 (A1) | ⬜ |
| ORG-06-AC-x3 | T012, T013, T014 (A9) | ⬜ |
| ORG-06-AC-x4 | T011 | ⬜ |
| PLT-07-AC1 (carried) | T017 | ⬜ |
