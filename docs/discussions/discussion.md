# Module discussions: progress log and hand-off

| | |
|---|---|
| **Last session** | 2026-10-22, office PC: Phase 2 (Gateway, T020–T022) done and reviewed (fixes in 5b6fc9f); T026 (users admin screens) started |
| **Build** | From 2026-10-21 on the **office PC** (`D:\Projects\Workflow`); the owner reviews on weekdays, full days |
| **Git** | Local commits per task on `main`; public repository https://github.com/mohshacopilot/flowza, pushed only when the owner says so |

## What to do next
Spec Kit is postponed; we follow the same method by hand (see [CLAUDE.md](../../CLAUDE.md), "Feature specs: manual SDD").
1. Implement feature 001 task by task from [tasks.md](../../specs/001-platform-foundation/tasks.md), one local commit per task.
   Done: Phase 0, Phase 1 (Identity), Phase 2 (Gateway, T020–T022 + review fixes), T023–T025 (web shell), T031 (CI).
   Done also: T026–T027 (web screens), T028 (demo-data), T029 (end-to-end, green in CI), T030 (setup guide; checklist to sign), T033 (production database scripts), T034 (start while the database is down). Next: **T032 (verify 001)**, the last
   task of feature 001.
2. ~~Write the specs for 002 to 006~~ **Done:** all five specs approved on 2026-10-21 (row 21).
3. Per feature: plan → tasks → analyze → implement, building **Milestone 1** first. Next plan: **002** (`specs/002-org-hierarchy/plan.md`, skill `write-plan`). When 002 is planned, amend spec 001 for 002 C2 (a new user needs a primary unit; the first admin gets the root unit).

In a new session, say:
> Read `docs/discussions/discussion.md` and continue from "What to do next".

## How each discussion works
For every module: purpose → main concepts → screens → business rules and edge cases → open questions
(owner answers like "O1 - a") → acceptance criteria → design notes. The answers are saved in one file per
module. Sections marked "design notes" go into `plan.md`, never into `spec.md`.

## 1. Progress
| # | Module | Status | File |
|---|---|---|---|
| 1 | ORG: Organization hierarchy | ✅ Decided (O1–O14, P1–P10) | [org-hierarchy.md](org-hierarchy.md) |
| 2 | EXP: Expression language | ✅ Decided (E1–E12, D1–D7, CV6) | [expression-language.md](expression-language.md) |
| 3 | APP: Apps and versions | ✅ Decided (A1–A10, Q1–Q6, events ok) | [apps-and-versions.md](apps-and-versions.md) |
| 4 | FRM: Form builder | ✅ Decided (F1–F10, G1, G3, S1–S3, tabs, DT1–DT3, CV1–CV7) | [form-builder.md](form-builder.md) |
| 5 | WF: Workflow engine | ✅ Decided (W1–W10, Y1–Y8 confirmed 2026-10-21) | [workflow-engine.md](workflow-engine.md) |
| 6 | INB: Inbox and requests | ✅ Decided (I1–I6, Z1–Z5), 2026-10-21 | [inbox-and-requests.md](inbox-and-requests.md) |
| 7 | NTF: Notifications | ✅ Decided (N1–N8, M1–M7), 2026-10-21 | [notifications.md](notifications.md) |
| 8 | IAM: Identity and access | ✅ Decided (K1–K8, L1–L7), 2026-10-21 | [identity-and-access.md](identity-and-access.md) |
| 9 | PLT: Platform | ✅ Decided (PL1–PL7, Q-P1–Q-P6), 2026-10-21 | [platform.md](platform.md) |
| 10 | Scope check (size of v0.1) | ✅ Accepted: three milestones (SC1), build from 2026-10-21 on the office PC (SC2, SC3) | [scope-check.md](scope-check.md) |
| 11 | Approve the v0.1 scope | ✅ Approved 2026-10-21 | [specs/v0.1-scope.md](../../specs/v0.1-scope.md) |
| 12 | Spec Kit | ⏸ Postponed by the owner (2026-10-21): manual SDD instead, same file layout | [CLAUDE.md](../../CLAUDE.md) "Feature specs: manual SDD" |
| 13 | Spec 001 Platform foundation (IAM + PLT) | ✅ Approved 2026-10-21 (C1–C6 all "a") | [spec.md](../../specs/001-platform-foundation/spec.md) |
| 14 | Tech discussion (owner's request, before plans) | ✅ T1–T10 decided 2026-10-21 (T11–T13 before features 003–005); code name **Flowza**; ADR-0004 | [tech-discussion.md](tech-discussion.md) |
| 15 | Plan 001 | ✅ Approved 2026-10-21 (PA1–PA7) | [plan.md](../../specs/001-platform-foundation/plan.md) |
| 16 | Tasks 001 | ✅ Written and analyzed | [tasks.md](../../specs/001-platform-foundation/tasks.md) |
| 17 | Build 001 Phase 0 (T001–T009) | ✅ Done 2026-10-21: solution, persistence, service defaults, AppHost, services, messaging on SQL Server + RabbitMQ; 143 tests pass; code review at phase end | [tasks.md](../../specs/001-platform-foundation/tasks.md) "Progress" |
| 18 | Build 001 Phase 1 (Identity, T010–T019) | ✅ Done 2026-10-21/22, plus T031 CI (green on GitHub) and the web shell T023–T025 (merged 2026-10-22); 283 .NET + 74 web tests pass | [tasks.md](../../specs/001-platform-foundation/tasks.md) "Progress" |
| 18a | Build 001 Phase 2 (Gateway, T020–T022) | ✅ Done 2026-10-22: YARP + BFF, protection, integration tests; code review at phase end, fixes in 5b6fc9f | [tasks.md](../../specs/001-platform-foundation/tasks.md) "Progress" |
| 18b | Build 001 web screens (T026–T027) | ✅ Done 2026-10-22: users admin screens (731f130, d79e680), my profile + change password (d61e8da); both checked live | [tasks.md](../../specs/001-platform-foundation/tasks.md) "Progress" |
| 19 | RabbitMQ removed from the development PC (owner, 2026-10-22) | ✅ SQL Server transport only on the PC; SQL Server topology fixed so events reach other services | [ADR-0005](../decisions/0005-message-transport-selection.md) amendment 1 |
| 20 | Transport per deployment (owner, 2026-10-22) | ✅ **On-premises = SQL Server only, cloud = Azure Service Bus; RabbitMQ dropped and its code removed** | [ADR-0005](../decisions/0005-message-transport-selection.md) amendments 2 and 3 |
| 21 | Specs 002–006 (ORG, EXP, APP + FRM, WF, INB + NTF) | ✅ Approved 2026-10-21; all clarifications answered "a" (002 C1–C9, 003 C1–C11, 004 C1–C13, 005 C1–C12, 006 C1–C18) | [002](../../specs/002-org-hierarchy/spec.md), [003](../../specs/003-expression-language/spec.md), [004](../../specs/004-apps-and-form-builder/spec.md), [005](../../specs/005-workflow-engine/spec.md), [006](../../specs/006-inbox-notifications/spec.md) |

## 2. Key decisions made today (summary)
Details and reasons are in each module file and ADR.

**Architecture records**
- [ADR-0001](../decisions/0001-audit-service-pluggable-store.md): Audit service (v0.4) with a pluggable store, SQL Server or MongoDB, chosen by DI.
- [ADR-0002](../decisions/0002-form-data-original-and-read-copy.md): form data original in SQL Server (Runtime); later read copy (SQL Server or MongoDB) synced by `FormDataSaved`.
- [ADR-0003](../decisions/0003-form-data-storage-layers.md): three layers (JSON original v0.1, Searchable-field index v0.2, reporting tables later) and step-wise saving; Layer 3 column types come from declared data types.

**ORG**
- Free tree structure.
- Vacant head → walk up. Self-approval → go one level up.
- Codes are fixed after creation.
- Every rule result has an explanation.
- No dated org history in v0.1.

**EXP**
- Excel-like, case-insensitive, bare field keys.
- **Decimal arithmetic** (C# `decimal` + **decimal.js**, approved).
- Round half away from zero. Blank values pass through (EXP-07).
- `TODAY()` uses the installation time zone.
- Added `CONTAINS`, `SUM`/`COUNT` over a grid, and `YEARS_BETWEEN`.

**APP**
- One draft at a time; any designer edits any app.
- Publish needs a note and asks "make active?".
- An app can be disabled, never deleted (APP-07).
- Restart only onto the active version.
- **Events approved:** `AppVersionPublished` (all definitions), `ActiveVersionChanged`, `AppStatusChanged`.

**FRM**
- 1–4 fields per row, label left or above; **tabs**; **grid** (add, edit, delete rows; view-only per step = the "Datalist view" in v0.1).
- Keys typed by hand.
- **Explicit data type per control**; a key never changes type family.
- Multiple files per upload field.
- "Required when"; rules limited to steps.
- Reject and Send back don't validate or save.
- **dnd-kit** approved.

**WF**
- **Approval mode per node: Any one / All must approve** (WF-14).
- **Maker-checker** on by default (WF-15).
- Send back in a parallel section → back to the split.
- Claim and release.
- Identity down → the action fails cleanly.
- Statuses: In progress, Sent back, Waiting for resubmission, Approved, Rejected, Rejected (final), Completed.

**Scope changes**
- New IDs: EXP-07, APP-07, FRM-12/13/14, WF-14/15.
- **Moved to v0.2:** ORG-07, INB-06, WF-10, NTF-03, to make room for the grid.
- Cut order if late: tabs, then file upload.

## 3. Open items to continue with

### 3.1 Done on 2026-10-21
- WF details Y1–Y8 confirmed ("Y ok").
- INB decided (I1–I6 all "a", Z1–Z5 ok): [inbox-and-requests.md](inbox-and-requests.md); new scope ID INB-07 (home counters).
- NTF decided (N1–N8 all "a", M1–M7 ok): [notifications.md](notifications.md); new scope IDs NTF-06 (more automatic emails) and NTF-07 (SMTP test); **MailKit** approved.
- IAM decided (K1–K8 all "a", L1–L7 ok): [identity-and-access.md](identity-and-access.md); new scope IDs IAM-06 (password policy), IAM-07 (lockout), IAM-08 (session lifetime). Gateway as BFF to be confirmed in the plan.
- PLT decided (PL1–PL7 all "a", Q-P ok): [platform.md](platform.md); new scope IDs PLT-08 (Settings screen, branding) and PLT-09 (CI); **Scalar** approved. **All 9 modules are now discussed.**

### 3.2 Modules still to discuss
| Module | Topics to cover |
|---|---|
| **NTF** | Template editor and placeholders (Liquid). Recipients (NTF-02). The end-of-request email (NTF-04). Retries and the failure log for admins (NTF-05). SMTP settings per installation. Email for "Waiting for resubmission" (APP-05). Privacy of form values in emails (constitution 11) |
| **IAM** | Login and logout. First admin and forced password change. Password rules and lockout. Session and token lifetime. Admin user management (create, reset, activate/deactivate). The roles Platform Admin, App Designer and User. What a deactivated user's sessions do |
| **PLT** | Aspire one-command start without Docker. Database per service and migrations. Wolverine over SQL Server and the outbox. Health checks, correlation ID, logging without personal data. Installation settings (time zone, currency, file storage path). Demo-data script (PLT-07). README setup in under 30 minutes |

## 4. Scope check (do this after PLT)
Since the first scope we added:
- the grid and tabs
- explicit data types
- "Required when" and step rules
- `YEARS_BETWEEN`
- "All must approve"
- maker-checker

Together this is **more than 4 days** of clean, tested work. Claude must then:
1. Give a realistic estimate per feature.
2. Propose a clear **v0.1 / v0.1.1** split, never cutting quality (tests, constitution).
3. Get the owner's approval and set [specs/v0.1-scope.md](../../specs/v0.1-scope.md) to "Approved".
