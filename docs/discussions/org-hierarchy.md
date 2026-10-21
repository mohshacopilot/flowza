# Module discussion: ORG (Organization hierarchy)

| | |
|---|---|
| **Status** | Decided by the owner (O1–O14, P1–P10), 2026-10-16 |
| **Feeds** | `/speckit-specify 002-org-hierarchy` (sections 1–6) and `/speckit-plan` (section 7) |
| **Scope IDs** | ORG-01 to ORG-07 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) |
| **Owning service** | Identity |

## What this file is
The result of the detailed discussion of the ORG module. Sections 1–6 are **what and why**, the input for
`spec.md`. Section 7 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
Workflows never name people. They say *"the branch head approves"* or *"a credit officer in the
requester's zone approves"*. The ORG module stores **who is who**, so the workflow engine can turn such a
description into real users when a task is created. This keeps the platform generic: each organization
builds its own tree and roles, and one engine serves all of them.

**Users:** the Platform Admin builds and maintains it; App Designers use its approver rules in processes;
the workflow engine resolves the rules at run time.

## 2. Main concepts
| Concept | Meaning | Example |
|---|---|---|
| **Unit type** | A level name defined by the admin | Head Office, Zone, Branch; or Hospital, Department |
| **Org unit** | One box in the tree: code, name, type, parent, optional head, active flag | `BR-TNAGAR` "T Nagar Branch" |
| **Head** | The user who leads a unit | Arun heads T Nagar Branch |
| **Primary unit** | The one unit a user belongs to | Ravi works in T Nagar Branch |
| **Reporting manager** | A user-to-user line, separate from the unit tree | Ravi reports to Arun |
| **Business role** | A job function given to users | Credit Officer, HR Officer, Duty Doctor |
| **Approver rule** | A description of who should act, stored in a process design | "Head of the nearest unit of type ZONE" |

There are **two hierarchies** on purpose: the unit tree and the manager lines. In real organizations they
often differ, and approver rules can use either.

### Example used in this file
```
Head Office (HO)                         head: Lakshmi
├── South Zone (ZONE)                    head: Priya
│   ├── Chennai Region (REGION)          head: Suresh
│   │   ├── T Nagar Branch (BRANCH)      head: Arun   staff: Ravi (manager: Arun)
│   │   └── Adyar Branch (BRANCH)        head: (vacant)
│   └── South Credit Dept (DEPT)         Credit Officers: Meena, Kumar
└── North Zone (ZONE)                    head: Vikram
```

## 3. Owner's decisions
| # | Question | Decision |
|---|---|---|
| O1 | Can any unit type sit under any other type? | **Yes, free structure.** "Allowed parent types" may come later |
| O2 | Deactivating a unit that still has active child units or active users | **Blocked** until they are moved or deactivated |
| O3 | Rule d or e finds a unit with no head (vacant) | **Walk up** to the nearest unit above that has an active head |
| O4 | Rule c (manager N levels up), but the chain is shorter than N | **Unassigned queue**; the admin decides |
| O5 | Self-approval: the rule resolves to the requester | **Skip the requester and go one level up.** For rule f the requester is removed from the list. Rule b is exempt |
| O6 | Rule e: does the requester's own unit count if it is of type T? | **Yes**, the search starts at the requester's own unit |
| O7 | Is a business role tied to a place? | **No.** A role belongs to the user; "where" means the user's primary unit |
| O8 | Rule f scopes | **All four:** anywhere · requester's unit · requester's unit and below · nearest unit of type T and everything below it |
| O9 | How rules refer to units, unit types and roles | **By code.** A code cannot change after creation |
| O10 | Can one user head more than one unit? | **Yes** |
| O11 | Must a unit's head belong to that unit? | **No** |
| O12 | Dated history of org changes | **Not in v0.1.** Each task records the resolved approver and the explanation |
| O13 | Deactivating a user who is a manager or a unit head | **Allowed, with a warning** that lists their direct reports and the units they head. Rules skip inactive users |
| O14 | Bulk import of units and users (Excel/CSV) | **v0.2.** v0.1 uses the demo-data script (PLT-07) |

### Details proposed by Claude, accepted by the owner ("P ok", 2026-10-16)
| # | Case | Proposal |
|---|---|---|
| P1 | Rule a (specific user) names the requester, or that user is inactive | Unassigned queue (there is no "level up" from a named user) |
| P2 | Rule g (user picker) holds the requester, or an inactive user | Unassigned queue. The form may also block choosing yourself, but that is a form setting, not an ORG rule |
| P3 | Rule c: the manager found at level N is inactive | Unassigned queue (same as O4), the explanation says why |
| P4 | Self-approval with rule d or e (O5) | Keep walking up from that unit to the nearest unit above with an active head who is **not** the requester |
| P5 | Rule f finds nobody after removing the requester and inactive users | Unassigned queue |
| P6 | Rule e or f-with-type: no unit of type T at or above the requester | Unassigned queue |
| P7 | Code format | Letters, digits, `-` and `_`; up to 50 characters; unique per kind (unit types, units, roles); case-insensitive; stored in upper case |
| P8 | Usernames used by rule a | A username cannot change after creation (same reason as O9) |
| P9 | A deactivated unit type | Cannot be chosen for new units; existing units keep it. Blocked while active units use it |
| P10 | A deactivated business role | Removed from rule f results; cannot be given to users. Process designs that use it show a warning on publish |

Every result that ends in the Unassigned queue carries a **reason** (e.g. "No unit of type ZONE above
T Nagar Branch"), shown to the admin who reassigns the task.

## 4. Approver rules (final)
| Rule | Resolves to | Empty / special cases |
|---|---|---|
| **a. Specific user** (by username) | That user | Requester or inactive → Unassigned (P1) |
| **b. The requester** | The requester | Self-approval check does not apply |
| **c. Manager, N levels up** (N ≥ 1) | The user N steps up the manager chain | Chain too short → Unassigned (O4); inactive → Unassigned (P3) |
| **d. Head of the requester's unit** | That unit's head | No active head, or head is the requester → walk up (O3, P4) |
| **e. Head of the nearest unit of type T** | Search starts at the requester's own unit (O6) and goes up; the first unit of type T; its head | No active head, or the requester → walk up (O3, P4). No unit of type T → Unassigned (P6) |
| **f. Users with business role R, in scope S** | All active users with role R whose primary unit is in scope S, minus the requester | Nobody → Unassigned (P5) |
| **g. User in a user-picker field** | The picked user | Requester or inactive → Unassigned (P2) |

**Rule f scopes (O8):**
1. **Anywhere** in the organization
2. **Requester's unit** only
3. **Requester's unit and below**
4. **Nearest unit of type T** (at or above the requester) **and everything below it**

**Result of every resolution:** a list of user IDs (0, 1 or many) **plus a readable explanation**, e.g.
> Ravi → T Nagar Branch → nearest ZONE: South Zone → head: Priya

The explanation is saved in the request history (supports O12) and shown by "Test a rule" (ORG-07).
One user → assigned; several → offered, first to claim owns it; none → Unassigned queue (WF-07).

## 5. Admin screens
1. **Unit types:** list; add, rename, deactivate.
2. **Org tree:** expandable tree plus a detail panel; add child unit, rename, set head, move ("Move to…"
   and drag), deactivate. Keyboard-operable (constitution 12.2).
3. **User organization details:** primary unit, reporting manager, business roles (users themselves are
   created in IAM). Deactivation shows the O13 warning.
4. **Business roles:** list of roles; members of each role.
5. **Test a rule (ORG-07):** choose a requester and a rule; see the users and the explanation.

## 6. Acceptance criteria (draft for spec.md)
IDs are `<feature>-AC<n>`; tests carry them in their names (e.g. `ORG_02_AC2_...`).

**ORG-01 Unit types**
- ORG-01-AC1: The admin can create a unit type with a unique code and a name; a duplicate code is rejected.
- ORG-01-AC2: A unit type's code cannot be changed after creation; its name can.
- ORG-01-AC3: A unit type used by active units cannot be deactivated (P9).

**ORG-02 Org tree**
- ORG-02-AC1: The admin can create units at any depth; any unit type may sit under any other (O1).
- ORG-02-AC2: Moving a unit under itself or under one of its descendants is rejected with a clear message.
- ORG-02-AC3: Moving a unit moves its whole subtree; rule results afterwards reflect the new position.
- ORG-02-AC4: Deactivating a unit with active child units or active users is rejected, and the message names them (O2).
- ORG-02-AC5: There is exactly one root unit.

**ORG-03 Unit details**
- ORG-03-AC1: A unit code is unique and cannot be changed after creation (O9).
- ORG-03-AC2: A head must be an active user; the head need not belong to the unit (O11) and may head several units (O10).

**ORG-04 Users in the organization**
- ORG-04-AC1: Every active user has exactly one primary unit, which must be active.
- ORG-04-AC2: Setting a manager that would create a loop (directly or through a chain) is rejected.
- ORG-04-AC3: A user cannot be their own manager.
- ORG-04-AC4: Deactivating a user who is a manager or a head succeeds and shows a warning listing their direct reports and units (O13).

**ORG-05 Business roles**
- ORG-05-AC1: The admin can create roles with a unique, unchangeable code, and add or remove members.
- ORG-05-AC2: A deactivated role cannot be given to users and is ignored by rule f (P10).

**ORG-06 Approver rules**: one or more criteria per rule and special case in section 4, for example:
- ORG-06-AC-d1: Requester Ravi, rule d → Arun.
- ORG-06-AC-d2: Requester in Adyar Branch (vacant), rule d → Suresh (walk up, O3).
- ORG-06-AC-d3: Requester Arun (head of own unit), rule d → Suresh (self-approval, O5/P4).
- ORG-06-AC-e1: Requester Ravi, rule e (ZONE) → Priya.
- ORG-06-AC-e2: Requester working in South Zone itself, rule e (ZONE) → Priya (own unit counts, O6).
- ORG-06-AC-e3: Requester Priya, rule e (ZONE) → Lakshmi (self-approval walk up).
- ORG-06-AC-e4: No unit of type T at or above the requester → empty result with reason (P6).
- ORG-06-AC-c1: Rule c, N=2, chain shorter than 2 → empty result with reason (O4).
- ORG-06-AC-f1: Requester Ravi, rule f (Credit Officer, nearest ZONE and below) → Meena and Kumar.
- ORG-06-AC-f2: Rule f never returns the requester or inactive users.
- ORG-06-AC-x1: Every result includes an explanation.
- ORG-06-AC-x2: Changes to org data affect only rules resolved afterwards; existing tasks keep their assignee (APP-06).

**ORG-07 Test a rule**
- ORG-07-AC1: The admin chooses a requester and a rule and sees the resolved users and the explanation, without creating any task.

## 7. Design notes (input for plan.md, not for spec.md)
- **Tree storage:** adjacency list (`ParentId`) **plus a materialized path** (e.g. `/1/4/9/23/`), indexed.
  - Descendants (rule f scopes 3 and 4): `Path LIKE '/1/4/%'`, an index seek.
  - Ancestors (rules d, e, walk-up): the IDs are in the unit's own path, O(depth).
  - Loop check on move: reject if the target's path starts with the moved unit's path, O(1).
  - Move: rewrite the path prefix of the subtree, O(subtree size), in one transaction; moves are rare.
  - Rejected: recursive CTE only (slower reads), closure table (more rows and writes), `hierarchyid`
    (extra EF package needing approval under constitution 1.6, and more SQL Server-specific).
- **Manager loop check:** walk up from the proposed manager; if the walk reaches the user, reject. O(chain length).
- **Rule resolution:** Strategy pattern. One `IApproverRuleResolver` per rule type, chosen from a dictionary
  keyed by rule type (Open/Closed: a new rule type is a new class). The self-approval and inactive-user
  checks are shared steps, so every resolver behaves the same way.
- **Rules are data:** JSON in the process definition, using codes (O9), e.g.
  `{ "type": "AncestorHead", "unitType": "ZONE" }`, `{ "type": "RoleInScope", "role": "CREDIT_OFFICER", "scope": "AncestorOfTypeAndBelow", "unitType": "ZONE" }`.
- **API (Identity):** `POST /api/v1/approver-rules/resolve` with the requester ID and the rule; returns user
  IDs, the explanation and, when empty, the reason. For rule g, Runtime sends only the picked user ID, so
  form values never leave Runtime (constitution 11).
- **Concurrency:** `rowversion` on units and users; two admins editing the same unit → the second gets a clear message (constitution 8.5).
- **Caching:** none in v0.1; measure first (NFR-02). The tree is small (thousands of units).
- **Events (for Audit later):** `OrgUnitCreated`, `OrgUnitMoved`, `OrgUnitDeactivated`, `UserOrgDetailsChanged`,
  `BusinessRoleAssigned`, … each with event ID, actor, UTC time and correlation ID. Exact names are fixed in the plan.
- **Open for the WF discussion:** what Runtime does if Identity is unavailable when a task must be created.
