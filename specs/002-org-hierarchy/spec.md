# Feature 002: Organization hierarchy (ORG)

| | |
|---|---|
| **Status** | **Draft**, waiting for the owner's answers to clarifications C1–C9 and approval |
| **Date** | 2026-10-21 |
| **Modules** | ORG (Organization hierarchy) |
| **Scope IDs** | ORG-01 to ORG-06 ([v0.1 scope](../v0.1-scope.md)); ORG-07 moved to v0.2 (G1) |
| **Sources** | [ORG discussion](../../docs/discussions/org-hierarchy.md) (O1–O14, P1–P10), [IAM discussion](../../docs/discussions/identity-and-access.md), [v0.1 scope](../v0.1-scope.md), [constitution](../../docs/constitution.md) |
| **Next documents** | `plan.md` (how), `tasks.md` (steps); both only after this spec is approved |

> **This spec says what and why, never how.** Technology choices (frameworks, libraries, protocols) belong in
> `plan.md`. The only technical words here are **constraints the customer gives us** (e.g. "runs on SQL Server
> 2019"), because they are requirements, not design choices.

## 1. Summary
Workflows never name people. They say *"the branch head approves"* or *"a credit officer in the requester's
zone approves"*. This feature stores **who is who** in the organization: the levels (unit types), the tree of
units, the head of each unit, each user's place and manager, and the business roles people hold. From that,
**approver rules** turn a description into real users at the moment a task is created.

There are **two hierarchies on purpose**: the unit tree and the manager lines. In real organizations they often
differ, and approver rules can use either.

**Why it matters:** this is what keeps the platform generic. Each organization builds its own tree and roles
(a bank, a hospital, an HR department), and one workflow engine serves all of them without code changes
(constitution Article 2). If approvers are found wrongly, a loan or a medical approval goes to the wrong person,
or to the requester themselves.

**Boundary with other features:**
- **001 Platform foundation** already specifies users (create, edit, deactivate, re-activate), usernames that
  never change, and the system roles Platform Admin, App Designer and User. This feature only **adds** a user's
  primary unit, reporting manager and business roles (IAM L1). It does not repeat 001.
- **004 Apps and form builder** owns APP-06 (approvers are found when each task is created) and the user-picker
  field used by rule g.
- **005 Workflow engine** owns what happens with a rule's result: assigning, offering and claiming tasks, the
  Unassigned queue (WF-07), maker-checker (WF-15), start permissions (WF-06), publish checks (WF-04) and the
  request history that stores the explanation (WF-11).
- **006 Inbox and notifications** owns the Unassigned queue screen and reassign (INB Z2), and notifications
  sent to an approver rule (NTF-02).

## 2. Users
| Persona | What they need from this feature |
|---|---|
| **Platform Admin** | Define unit types; build and maintain the org tree; set unit heads; place users in units and under managers; define business roles and their members |
| **App Designer** | Describe in a process who should act (an approver rule), without naming people |
| **Workflow engine** (not a person) | Turn an approver rule into users, with an explanation, when a task is created |
| **Platform Admin handling stuck tasks** | Read why a rule found nobody (the reason), so they can reassign the task |

## 3. User stories and acceptance scenarios
Priority **P1** = needed for Milestone 1. All stories in this feature are P1.

**Example organization used in this spec** (from the ORG discussion):
```
Head Office (HO)                         head: Lakshmi
├── South Zone (ZONE)                    head: Priya
│   ├── Chennai Region (REGION)          head: Suresh
│   │   ├── T Nagar Branch (BRANCH)      head: Arun   staff: Ravi (manager: Arun)
│   │   └── Adyar Branch (BRANCH)        head: (vacant)
│   └── South Credit Dept (DEPT)         Credit Officers: Meena, Kumar
└── North Zone (ZONE)                    head: Vikram
```

### US1: Name the levels of my organization (P1, ORG-01)
As a Platform Admin, I define my own unit types, so that the tree uses the words my organization uses.
1. **When** the admin adds a unit type with code `ZONE` and name "Zone", **then** it can be chosen for new units.
2. **When** the admin adds another unit type with code `zone`, **then** it is rejected as a duplicate (codes ignore letter case).
3. **When** the admin renames "Zone" to "Zonal Office", **then** the name changes and the code stays `ZONE`.
4. **Given** active units of type ZONE, **when** the admin deactivates ZONE, **then** it is refused (P9).

### US2: Build and change the org tree (P1, ORG-02)
As a Platform Admin, I build a tree of units of any depth and keep it up to date, so that it matches how my organization really works.
1. **When** the admin adds a unit under any other unit, of any type and at any depth, **then** it is saved (free structure, O1).
2. **When** the admin moves Chennai Region under T Nagar Branch (its own child), **then** it is refused with a clear message.
3. **When** the admin moves Chennai Region under North Zone, **then** T Nagar and Adyar Branch move with it, and rules resolved afterwards use the new position.
4. **Given** South Zone still has active child units, **when** the admin deactivates it, **then** it is refused and the message names those units (O2).
5. **Then** the admin can do all of this with the keyboard, using "Move to…" as well as dragging (constitution 12.2).

### US3: Describe each unit and its head (P1, ORG-03)
As a Platform Admin, I give each unit a code, a name, a type and an optional head, so that rules can find the person who leads it.
1. **When** the admin creates a unit with code `BR-TNAGAR`, **then** the code is unique and can never be changed (O9).
2. **When** the admin sets Arun as head of T Nagar Branch, **then** Arun must be an active user; Arun need not work in that unit (O11) and may head other units too (O10).

### US4: Place each user in the organization (P1, ORG-04)
As a Platform Admin, I give each user a primary unit, an optional reporting manager and business roles, so that rules can find the right people around a requester.
1. **Then** every active user has exactly one primary unit, and it is active.
2. **Given** Ravi reports to Arun, **when** the admin sets Ravi as Arun's manager, **then** it is refused (a reporting loop).
3. **When** the admin sets a user as their own manager, **then** it is refused.
4. **Given** Arun manages Ravi and heads T Nagar Branch, **when** the admin deactivates Arun, **then** it succeeds and a warning lists Ravi and T Nagar Branch (O13).

### US5: Business roles (P1, ORG-05)
As a Platform Admin, I define job functions and give them to users, so that processes can ask for "a Credit Officer" instead of a name.
1. **When** the admin creates the role `CREDIT_OFFICER` and adds Meena and Kumar, **then** both are members.
2. **Then** a role belongs to the user, not to a place; "where" always means the user's primary unit (O7).
3. **When** the admin deactivates a role, **then** it cannot be given to users and rules no longer return its members (P10).

### US6: Processes find approvers without naming people (P1, ORG-06)
As an App Designer, I describe who should act with one of seven rules, so that the same process works when people change.
1. **Given** requester Ravi and rule d (head of the requester's unit), **then** the result is Arun.
2. **Given** requester Ravi and rule e (nearest unit of type ZONE), **then** the result is Priya.
3. **Given** requester Ravi and rule f (role Credit Officer, nearest ZONE and everything below it), **then** the result is Meena and Kumar.
4. **Given** requester Ravi and rule c with N=1, **then** the result is Arun.

### US7: Gaps and self-approval are handled safely (P1, ORG-06)
As the business, I never want a request approved by its own requester or stuck without anyone knowing why.
1. **Given** a requester in Adyar Branch (no head) and rule d, **then** the rule walks up and the result is Suresh (O3).
2. **Given** requester Arun (head of the requester's own unit) and rule d, **then** the result is Suresh, not Arun (O5, P4).
3. **Given** rule c with N=2 and a manager chain shorter than 2, **then** the result is empty, with a reason for the admin (O4).

### US8: Every result explains itself (P1, ORG-06)
As a Platform Admin or auditor, I can see why a person was chosen, so that I can trust and check the routing.
1. **Then** every result comes with a readable explanation, e.g. "Ravi → T Nagar Branch → nearest ZONE: South Zone → head: Priya".
2. **Given** an empty result, **then** it carries a reason, e.g. "No unit of type ZONE above T Nagar Branch".
3. **When** org data changes, **then** only rules resolved afterwards are affected; existing tasks keep their assignee (APP-06).

## 4. Requirements
| ID | Requirement | Milestone |
|---|---|---|
| ORG-01 | The admin defines **unit types** (code and name), renames and deactivates them. A deactivated unit type cannot be chosen for new units; existing units keep it; deactivation is blocked while active units use it (P9) | M1 |
| ORG-02 | The admin builds a **tree of org units of any depth**: create, rename, move (with "Move to…" and by dragging), deactivate. Any unit type may sit under any other (O1). There is exactly one root unit. A unit cannot be moved under itself or its own descendants; moving a unit moves its whole subtree. Deactivating a unit with active child units or active users is blocked until they are moved or deactivated (O2). The tree screen works with the keyboard (constitution 12.2) | M1 |
| ORG-03 | Each unit has a type, a unique **code**, a name and an optional **head** (a user). The head must be an active user; the head need not belong to the unit (O11); one user may head several units (O10) | M1 |
| ORG-04 | Each active user belongs to exactly one active **primary unit** and may have a **reporting manager**. No reporting loops: a user cannot be their own manager, directly or through a chain. Deactivating a user who is a manager or a unit head is allowed, with a warning that lists their direct reports and the units they head; rules skip inactive users (O13) | M1 |
| ORG-05 | The admin defines **business roles** (code and name), sees the members of each role, and adds or removes members. A role is not tied to a place (O7). A deactivated role cannot be given to users and is ignored by rule f; process designs that use it show a warning on publish (P10, publish checks in 005) | M1 |
| ORG-06 | **Approver rules** a–g (section 4.1) that the workflow engine uses to find who should act. Every result is a list of users (0, 1 or many) **plus a readable explanation**; an empty result also carries a **reason** for the admin. Rules use org data at the moment they are resolved (APP-06) | M1 |

**Codes (P7, O9, P8):** codes of unit types, units and business roles use letters, digits, `-` and `_`; up to 50
characters; unique per kind; letter case is ignored; they are stored in upper case; they **cannot change** after
creation, because process designs refer to them. Rule a refers to users by **username**, which also never changes
(P8, specified in 001 as IAM-04).

### 4.1 ORG-06 approver rules in detail
| Rule | Resolves to | Empty or special cases |
|---|---|---|
| **a. Specific user** (by username) | That user | Requester or inactive → empty (P1); there is no "level up" from a named user |
| **b. The requester** | The requester | The self-approval check does not apply |
| **c. Manager, N levels up** (N ≥ 1) | The user N steps up the manager chain | Chain too short → empty (O4); the manager found at level N is inactive → empty (P3) |
| **d. Head of the requester's unit** | That unit's head | No active head, or the head is the requester → walk up (O3, P4) |
| **e. Head of the nearest unit of type T** | The search starts at the requester's own unit (O6) and goes up; the first unit of type T; its head | No active head, or the head is the requester → walk up (O3, P4). No unit of type T at or above the requester → empty (P6) |
| **f. Users with business role R, in scope S** | All active users with role R whose primary unit is in scope S, minus the requester | Nobody → empty (P5) |
| **g. User in a user-picker field** | The picked user | Requester or inactive → empty (P2). The form may also block choosing yourself, but that is a form setting, not an ORG rule |

**Rule f scopes (O8):** (1) anywhere in the organization; (2) the requester's unit only; (3) the requester's unit
and below; (4) the nearest unit of type T (at or above the requester) and everything below it.

**Shared rules for all approver rules:**
- **Walk up (O3):** when rule d or e finds a unit with no active head, it moves to the nearest unit above that has an active head.
- **Self-approval (O5, P4):** when a rule resolves to the requester, it skips the requester and goes one level up. For rules d and e it keeps walking up to the nearest unit above with an active head who is **not** the requester. For rule f the requester is removed from the list. Rule b is exempt. Rules a and g give an empty result instead (P1, P2).
- **Inactive users (O13):** rules skip inactive users.
- **"Where" means the primary unit (O7):** the requester's unit, and a role member's place, are their primary units.
- **Empty result:** goes to the Unassigned queue (WF-07, feature 005) with its reason, shown to the admin who reassigns the task.
- **What happens with the result** (one user assigned, several offered, maker-checker filter) is specified in 005 (WF-07, WF-14, WF-15). The explanation is saved in the request history (WF-11, feature 005), which also supports O12.

## 5. Acceptance criteria
Every criterion gets at least one automated test whose name contains its ID (constitution 1.4), unless marked
*manual*. Criteria for ORG-06 use the example organization in section 3. "Empty with reason" means: no users,
an explanation and a reason.

| ID | Criterion |
|---|---|
| ORG-01-AC1 | The admin can create a unit type with a unique code and a name; a duplicate code is rejected |
| ORG-01-AC2 | A unit type's code cannot be changed after creation; its name can |
| ORG-01-AC3 | A unit type used by active units cannot be deactivated (P9) |
| ORG-01-AC4 | A deactivated unit type cannot be chosen for a new unit; existing units keep it (P9) |
| ORG-01-AC5 | A unit type code outside the format (letters, digits, `-`, `_`; up to 50 characters) is rejected; a code entered in lower case is stored in upper case and clashes with the same code in another case (P7) |
| ORG-02-AC1 | The admin can create units at any depth; any unit type may sit under any other (O1) |
| ORG-02-AC2 | Moving a unit under itself or under one of its descendants is rejected with a clear message |
| ORG-02-AC3 | Moving a unit moves its whole subtree; rule results afterwards reflect the new position |
| ORG-02-AC4 | Deactivating a unit with active child units or active users is rejected, and the message names them (O2) |
| ORG-02-AC5 | There is exactly one root unit |
| ORG-02-AC6 | Every org tree action (expand, select, add child, rename, set head, "Move to…", deactivate) can be done with the keyboard only (constitution 12.2) |
| ORG-03-AC1 | A unit code is unique and cannot be changed after creation (O9) |
| ORG-03-AC2 | A head must be an active user; the head need not belong to the unit (O11) and may head several units (O10) |
| ORG-03-AC3 | A unit code follows the same format and case rules as unit type codes (P7) |
| ORG-03-AC4 | The admin can rename a unit and set or change its head |
| ORG-04-AC1 | Every active user has exactly one primary unit, which must be active |
| ORG-04-AC2 | Setting a manager that would create a loop (directly or through a chain) is rejected |
| ORG-04-AC3 | A user cannot be their own manager |
| ORG-04-AC4 | Deactivating a user who is a manager or a head succeeds and shows a warning listing their direct reports and units (O13) |
| ORG-04-AC5 | The admin sets a user's primary unit, reporting manager and business roles; a user without a reporting manager is valid |
| ORG-05-AC1 | The admin can create roles with a unique, unchangeable code, and add or remove members |
| ORG-05-AC2 | A deactivated role cannot be given to users and is ignored by rule f (P10) |
| ORG-05-AC3 | A business role code follows the same format and case rules as unit type codes (P7) |
| ORG-05-AC4 | The admin sees the list of roles and the members of each role |
| ORG-06-AC-a1 | Rule a naming Priya's username → Priya |
| ORG-06-AC-a2 | Rule a naming the requester, or naming an inactive user → empty with reason (P1) |
| ORG-06-AC-b1 | Requester Ravi, rule b → Ravi; the self-approval check does not apply |
| ORG-06-AC-c1 | Rule c, N=2, chain shorter than 2 → empty result with reason (O4) |
| ORG-06-AC-c2 | Requester Ravi, rule c, N=1 → Arun |
| ORG-06-AC-c3 | Requester Ravi, rule c, N=1, Arun inactive → empty with a reason that says the manager is inactive (P3) |
| ORG-06-AC-d1 | Requester Ravi, rule d → Arun |
| ORG-06-AC-d2 | Requester in Adyar Branch (vacant), rule d → Suresh (walk up, O3) |
| ORG-06-AC-d3 | Requester Arun (head of own unit), rule d → Suresh (self-approval, O5/P4) |
| ORG-06-AC-d4 | Requester Ravi, rule d, Arun inactive → Suresh (rules skip inactive users, walk up; O3, O13) |
| ORG-06-AC-d5 | Rule d or e where the walk up passes the root without finding an active head who is not the requester → empty with reason |
| ORG-06-AC-e1 | Requester Ravi, rule e (ZONE) → Priya |
| ORG-06-AC-e2 | Requester working in South Zone itself, rule e (ZONE) → Priya (own unit counts, O6) |
| ORG-06-AC-e3 | Requester Priya, rule e (ZONE) → Lakshmi (self-approval walk up) |
| ORG-06-AC-e4 | No unit of type T at or above the requester → empty result with reason (P6) |
| ORG-06-AC-e5 | Requester in Adyar Branch (vacant), rule e (BRANCH) → Suresh (walk up, O3) |
| ORG-06-AC-f1 | Requester Ravi, rule f (Credit Officer, nearest ZONE and below) → Meena and Kumar |
| ORG-06-AC-f2 | Rule f never returns the requester or inactive users |
| ORG-06-AC-f3 | Rule f (Credit Officer, anywhere) → every active Credit Officer in the organization (in the example: Meena and Kumar) |
| ORG-06-AC-f4 | Requester Meena, rule f (Credit Officer, requester's unit only) → Kumar |
| ORG-06-AC-f5 | Rule f (Credit Officer, requester's unit and below): a requester in South Zone → Meena and Kumar; requester Ravi → empty with reason (P5) |
| ORG-06-AC-f6 | Requester in South Zone, rule f (Credit Officer, nearest REGION and below) → empty with reason, because no REGION is at or above South Zone (P6) |
| ORG-06-AC-g1 | Rule g with Meena picked in the field → Meena |
| ORG-06-AC-g2 | Rule g with the requester or an inactive user picked → empty with reason (P2) |
| ORG-06-AC-x1 | Every result includes an explanation |
| ORG-06-AC-x2 | Changes to org data affect only rules resolved afterwards; existing tasks keep their assignee (APP-06) |
| ORG-06-AC-x3 | Every empty result carries a readable reason, e.g. "No unit of type ZONE above T Nagar Branch" |

## 6. Edge cases
- **The head of a unit is deactivated** (O13): rules treat the unit as having no active head; rules d and e walk up (ORG-06-AC-d4).
- **A user who has open tasks is deactivated:** their open tasks move to the Unassigned queue (scope Q4); this is specified in 005.
- **A role is deactivated while a published process uses it:** rule f ignores it, so the result is empty with a reason; the next publish shows a warning (P10, publish checks in 005).
- **A unit is moved while requests are running:** existing tasks keep their assignee; the next task uses the new position (ORG-02-AC3, ORG-06-AC-x2).
- **A requester heads the unit of type T they search for** (rule e): the rule walks up to the next active head who is not the requester (ORG-06-AC-e3).
- **A user heads several units:** allowed (O10). Rules d and e still start from the requester's **primary** unit (O7).
- **A user-picker field is empty when rule g runs:** no user is found, so the result is empty with a reason.
- **A code is typed in lower case** (`br-tnagar`): it is stored as `BR-TNAGAR` and clashes with an existing `BR-TNAGAR` (P7).
- **A user is re-activated (001, C6) while their old primary unit is inactive:** ORG-04-AC1 still holds, so the user needs an active primary unit (see C2).
- **Two admins edit the same unit or user at the same moment:** the second gets a clear "changed by someone else" message (constitution 8.5).
- **The admin tries to deactivate the root unit:** it is blocked while it has active child units or active users (O2).

## 7. Non-functional requirements that apply
From the [v0.1 scope](../v0.1-scope.md) section 6: NFR-01 (directory screens need Platform Admin), NFR-02
(finding approvers is part of a task action that completes in under 1 second), NFR-03 (reliability), NFR-04
(SQL Server 2019 or later, no cloud-only services, Windows without containers), NFR-07 (privacy: explanations
and names never appear in logs, only IDs), NFR-08 (no hard delete: unit types, units and roles are deactivated,
never deleted), NFR-10 (translation files, including the fixed words of explanations and reasons), NFR-11
(keyboard and labels), NFR-12 (coverage). Also constitution 8.5 (concurrent edits detected) and the rule from
CLAUDE.md that important actions are prepared for the future audit log (details in the plan).

## 8. Key entities (business view, not database design)
| Entity | Meaning |
|---|---|
| **Unit type** | A level name defined by the admin (e.g. Head Office, Zone, Branch; or Hospital, Department): code (never changes), name, active flag |
| **Org unit** | One box in the tree: code (never changes), name, type, parent (none for the root), optional head, active flag |
| **Head** | The active user who leads a unit; may work elsewhere and may head several units |
| **Primary unit** | The one active unit an active user belongs to; it is "where" the user is for every rule |
| **Reporting manager** | A user-to-user line, separate from the unit tree; optional; never forms a loop |
| **Business role** | A job function given to users (e.g. Credit Officer, HR Officer, Duty Doctor): code (never changes), name, active flag, members. Not tied to a place |
| **Approver rule** | A description of who should act, stored in a process design: one of the rules a–g, referring to unit types, units and roles by code and to users by username |
| **Resolution result** | What a rule returns at one moment: a list of users (0, 1 or many), a readable explanation and, when empty, a reason |
| **Code** | The fixed identifier of a unit type, unit or role: letters, digits, `-`, `_`; up to 50 characters; unique per kind ignoring case; stored in upper case |

## 9. Out of scope
| Item | When |
|---|---|
| "Test a rule" admin screen (ORG-07) | v0.2 (G1) |
| Bulk import of units and users (Excel/CSV); v0.1 uses the demo tool (PLT-07) | v0.2 (O14) |
| "Allowed parent types" per unit type | Later (O1) |
| Dated history of org changes | Not in v0.1 (O12); each task records the resolved approver and the explanation instead |
| Authority matrix, delegation and out of office | v0.3 |
| Option lists filled from org data in forms | Later (F4) |
| Audit log screens | v0.4 (actions are prepared now) |
| Creating, editing and deactivating users; system roles | Feature 001 (not repeated here) |

## 10. Clarifications (open, for the owner)
| # | Question | Options (⭐ = Claude's recommendation) | Answer |
|---|---|---|---|
| C1 | **Where does the root unit come from?** ORG-02-AC5 says there is exactly one root, but a new installation has no tree, and codes can never change (O9) | **a) Installation creates one root unit and one unit type with fixed codes (e.g. `ROOT`); the admin renames them ⭐** · b) The admin creates the root unit as the first step; until then the tree is empty (an exception to ORG-02-AC5) | Open |
| C2 | **Primary unit when a user is created.** ORG-04-AC1 says every active user has an active primary unit, but the approved 001 spec creates users (and the first admin at installation) without one | **a) Creating a user also asks for the primary unit (required); the first admin gets the root unit; a re-activated user (001, C6) needs an active primary unit first. 001's user screen gains this field (IAM L1: "ORG adds primary unit, manager and business roles") ⭐** · b) New users get the root unit by default until the admin changes it · c) Users may be active without a primary unit; rules that need it give an empty result with a reason (an exception to ORG-04-AC1) | Open |
| C3 | **Re-activating** a deactivated unit type, unit or business role. Sources name only deactivation | **a) Allowed; a unit can be re-activated only while its parent is active ⭐** · b) Not in v0.1 | Open |
| C4 | **Adding or moving an active unit under an inactive unit.** O2 keeps inactive units free of active children, but says nothing about adding or moving | **a) Refused with a clear message, so an inactive unit never has active children ⭐** · b) Allowed | Open |
| C5 | **Rule c with an inactive manager in the middle of the chain** (e.g. N=2, the level-1 manager is inactive, the level-2 manager is active). P3 covers only the manager found at level N | **a) Count the inactive manager as a level and keep walking; only the user at level N must be active ⭐** · b) Skip the inactive manager (do not count them) · c) Empty result with a reason | Open |
| C6 | **Rule b when the requester is inactive.** Rule b is exempt from the self-approval check, but O13 says rules skip inactive users | **a) Empty result with a reason, like the other rules ⭐** · b) Return the requester anyway | Open |
| C7 | **Must a reporting manager or a new role member be an active user?** ORG-03-AC2 requires this only for heads | **a) Yes for both, the same as heads ⭐** · b) No | Open |
| C8 | **Design-time check of approver rules.** Should publishing check that the codes and usernames in a rule exist and are active? P10 asks only for a warning about deactivated roles | **a) Publish error for an unknown code or username; publish warning for a deactivated unit type, deactivated role or inactive user (extends P10). Specified in 005 (WF-04) ⭐** · b) Only the P10 warning; anything else gives an empty result with a reason at run time | Open |
| C9 | **What may an App Designer see of org data?** Designers choose unit types, roles, units and users in rules and start permissions (WF-06), but directory screens need Platform Admin (IAM-02, NFR-01) | **a) App Designers may look up (not change) unit types, units, business roles and users (name and username) when they build a process ⭐** · b) Designers type codes and usernames by hand · c) Designers also need the Platform Admin role | Open |

## 11. Spec quality checklist (Claude checks before asking for approval)
- [x] Every requirement has an ID, a milestone and at least one acceptance criterion
- [x] No technology choices in the spec (only customer constraints)
- [x] Every acceptance criterion is testable; none needs a manual check
- [x] Every owner decision (O1–O14, P1–P10) appears in the spec
- [x] Rules owned by other features (001, 004, 005, 006) are referenced, not repeated
- [x] Out-of-scope items are listed so nobody builds them by mistake
- [ ] All clarifications answered (C1–C9)
- [ ] Approved by the owner
