# Module discussion: APP (Apps and versions)

| | |
|---|---|
| **Status** | Decided by the owner (A1–A10), 2026-10-16. Details Q1–Q6 proposed by Claude; the owner raised no objection. Event change (section 7) **approved by the owner** ("events ok") |
| **Feeds** | `/speckit-specify 004-apps-and-form-builder` (sections 1–6) and `/speckit-plan` (section 7) |
| **Scope IDs** | APP-01 to APP-07 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) |
| **Owning services** | Design (apps, drafts, publishing), Runtime (running requests, restart) |

## What this file is
The result of the detailed discussion of the APP module. Sections 1–6 are **what and why**, the input for
`spec.md`. Section 7 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
An **app** is a box that holds one business solution (e.g. "Leave Management", "Loan Origination"): its forms
and processes. The app is **versioned as a whole** (v1, v2, v3), so a process can never point at a field that
a newer form removed.

## 2. Already decided (scope APP-01 to APP-06, Q8.x)
- Draft → Published. A published version never changes; a new draft is copied from a chosen version.
- One version is **active**; new requests use it. Choosing an older version is a **rollback**.
- Running requests stay on their version; an admin can **restart** them on the active version (APP-05).
- Approvers are found when each task is created (APP-06).

### Lifecycle example
```
Leave Management
  v1  Published               ← requests #101–#250 run on v1
  v2  Published  ★ ACTIVE     ← new requests start here
  v3  Draft (copied from v2)  ← designer is editing
```
1. The designer publishes v3 with a note. All forms and processes are checked (EXP-04, WF-04, form checks); publish is all-or-nothing.
2. The designer is asked "Make v3 the active version now?" and says yes. New requests use v3; #101–#250 stay on v1.
3. v3 has a bug: the designer makes v2 active again (rollback). Requests already started on v3 stay on v3.
4. The admin restarts #240 on the active version; it returns to its requester as "Waiting for resubmission".

## 3. Owner's decisions
| # | Question | Decision |
|---|---|---|
| A1 | Drafts per app | **One draft at a time** |
| A2 | Who can edit an app | **Any App Designer can edit any app** (per-app permissions later) |
| A3 | After publishing | The designer is asked **"Make this the active version now?"** (default yes) |
| A4 | Stopping an app | An app can be **Disabled**: hidden from "Start a request", no new requests, running requests continue. Apps are **never deleted** |
| A5 | Restart target version | **Only the active version** |
| A6 | Version labels | **Whole numbers** (1, 2, 3) plus a **required publish note** |
| A7 | Restart: same field key, different type | **Copy the value if it converts cleanly; otherwise leave it empty and note it in the history** |
| A8 | Two designers edit the same form in one draft | **Optimistic concurrency**: the second save gets "Changed by Priya at 10:42, reload" |
| A9 | Trying a version before users get it | **Form preview only** in v0.1; after activating, **rollback** is the safety net. A test environment comes with test → live promotion (v0.2) |
| A10 | Compare versions screen | **Later** |

## 4. Details proposed by Claude (no objection from the owner)
| # | Detail |
|---|---|
| Q1 | Each app has a unique **code that cannot change** (same rule as ORG codes, O9) |
| Q2 | Forms and processes belong to **one app only**; no sharing between apps in v0.1. Form and process keys are unique within the app |
| Q3 | A **draft can be discarded** (soft delete). A published version can never be discarded |
| Q4 | Restart is **blocked** for a request whose process does not exist in the active version; the message names the request |
| Q5 | **Bulk restart** runs in the background, one transaction per request; the admin sees a result list (restarted / failed with reason) |
| Q6 | A rollback does not affect reopen (WF-13): a reopened request keeps its own version |

### Clean type conversion on restart (A7)
| From → To | Converts when |
|---|---|
| Number ↔ Currency | Always |
| Number/Currency → Text | Always (plain number text) |
| Text → Number/Currency | The text is a valid number |
| Text → Date | The text is a valid ISO date |
| Dropdown/Radio → Dropdown/Radio | The stored value exists in the new option list |
| Anything else | Not converted: empty, noted in the history |

## 5. Screens
1. **Apps list** (designer): name, code, active version, status (Enabled/Disabled).
2. **App page:** version list (number, status, published by, date, note); actions: Edit draft, New draft from
   version…, Publish (note required), Make active, Discard draft, Disable/Enable app.
3. **Inside a draft:** list of forms and processes; opens the form designer or process designer; form preview.
4. **Restart (admin):** choose running requests (filter by app, process, version), enter a reason, run;
   see the result list.

## 6. Acceptance criteria (draft for spec.md)
**APP-01 Apps**
- APP-01-AC1: A designer creates an app with a unique code, name and description; a duplicate code is rejected; the code cannot change later (Q1).

**APP-02 Versions**
- APP-02-AC1: An app has at most one draft (A1); "New draft from version…" is refused while a draft exists.
- APP-02-AC2: Publishing requires a note (A6) and checks all forms and processes; if any check fails, nothing is published and every error points to its form, field or node.
- APP-02-AC3: A published version cannot be edited or discarded; versions are numbered 1, 2, 3 in publish order.
- APP-02-AC4: A second save of the same form or process based on an old copy is rejected with who changed it and when (A8).
- APP-02-AC5: A draft can be discarded (Q3).

**APP-03 Active version**
- APP-03-AC1: After publishing, the designer can make the new version active in the same step (A3).
- APP-03-AC2: Making an older version active (rollback) makes new requests use it; requests already started keep their version.

**APP-04 Running requests**
- APP-04-AC1: A request started on v1 continues on v1 after v2 becomes active.

**APP-05 Restart**
- APP-05-AC1: The admin can restart one or several running requests only on the **active** version (A5), with a required reason.
- APP-05-AC2: Values are copied by field key; type changes follow the conversion table; unconverted values stay empty and are noted in the history (A7).
- APP-05-AC3: A request whose process does not exist in the active version is not restarted, and the result says why (Q4).
- APP-05-AC4: Bulk restart reports each request as restarted or failed with a reason (Q5).

**APP-06 Approver timing**: see ORG-06-AC-x2.

**APP-07 Disable an app (new scope ID, A4)**
- APP-07-AC1: A disabled app's processes disappear from "Start a request"; its running requests continue normally.
- APP-07-AC2: A disabled app can be enabled again; there is no delete.

## 7. Design notes (input for plan.md, not for spec.md)
- **Storage (Design service):** `App` (code, name, description, status), `AppVersion` (number, status, note,
  format version, published by/at, rowversion), and each form and process as a **JSON document** in
  `nvarchar(max)` with its own rowversion (A8). Published versions are immutable (constitution 8.1); the
  format version allows auto-upgrade (8.7).
- **Patterns:** new draft from a version = **Prototype** (deep copy of the documents); publish checks =
  independent validators (**Specification** pattern), each returning errors that point to a form, field or node.
- **Event change, approved by the owner on 2026-10-16 (constitution 1.6):** replace the planned `FormPublished` and
  `ProcessPublished` with one **`AppVersionPublished`** (all definitions of the version) plus
  **`ActiveVersionChanged`** and **`AppStatusChanged`** (A4). Runtime keeps its own copy and never calls Design at run time.
- **Restart** runs in Runtime (it owns requests). Copy by key = dictionary lookup, O(fields). Bulk restart =
  one Wolverine message per request, one transaction each, results collected for the admin (Q5).
