# Module discussion: INB (Inbox and requests)

| | |
|---|---|
| **Status** | Decided by the owner (I1–I6, Z1–Z5), 2026-10-21 |
| **Feeds** | `/speckit-specify 006-inbox-notifications` (sections 1–5) and `/speckit-plan` (section 6) |
| **Scope IDs** | INB-01 to INB-05 and INB-07 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) (INB-06 moved to v0.2) |
| **Related** | [WF](workflow-engine.md) (tasks, statuses, claim/release, Unassigned queue), [FRM](form-builder.md) (task form), [APP](apps-and-versions.md) (active version, disabled apps) |
| **Owning service** | Runtime |

## What this file is
The result of the detailed discussion of the INB module. Sections 1–5 are **what and why**, the input for
`spec.md`. Section 6 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
The screens people use every day: start a request, see and act on tasks, follow their own requests.
They must be fast and clear even with tens of thousands of tasks (NFR-02).

## 2. Screens
```
┌ Home ─────────────────────────────────────────────────────────────┐
│  My tasks 5     Available 3     My requests in progress 2         │  ← counters (I1)
├───────────────────────────────────────────────────────────────────┤
│ My tasks   Available   My requests                [Search LN-…]   │
│ Request no.      Process        Requester  Step          Waiting  │
│ LN-2026-000123   Loan approval  Ravi       Credit check  2 d 4 h  │
│ LV-2026-000045   Leave          Meena      Manager       3 h      │
│                                              [Refresh]  < 1 2 3 > │
└───────────────────────────────────────────────────────────────────┘
```
| Screen | Scope ID | Contents |
|---|---|---|
| Home counters | INB-07 | My tasks, Available tasks, My requests in progress (I1) |
| Start a request | INB-01 | Processes the user may start, grouped by app, with search (Z4) |
| My tasks / Available tasks | INB-02 | Request number, process, requester, step, waiting time; sort, filter by app/process, search by request number; claim from Available |
| Task screen | INB-03 | The form (per-step field permissions), history timeline, action buttons, comment box, "2 of 3 approved" progress for "All must approve" steps (WF-14) |
| My requests | INB-04 | Status (WF statuses), current step, who it is with (Z5); opens a read-only view with history |
| Unassigned queue (admin) | WF-07 | Tasks with no assignee and the reason; reassign (Z2) |
| Reassign (admin) | WF-07 | Reassign any open task found by request number (Z2) |

## 3. Owner's decisions
| # | Question | Decision |
|---|---|---|
| I1 | Counters on the home page | **v0.1** |
| I2 | "Tasks I completed" tab | **Later** (a request can still be opened by its number) |
| I3 | Live updates | **Refresh when the page opens, plus a Refresh button.** Claiming a task someone else just took shows a clear message. Live push (SignalR) later |
| I4 | Bulk approve | **Later** |
| I5 | Print view | **Later**, with PDF documents (v0.4) |
| I6 | In-app notifications (bell) | **Later**; email only in v0.1 |

## 4. Details accepted by the owner (Z1–Z5)
| # | Detail |
|---|---|
| Z1 | Lists are paged on the server, **25 rows per page**, newest first by default (NFR-02) |
| Z2 | The admin's **Unassigned queue** screen and **reassign** action are in v0.1 (WF-07 needs them; this is not INB-06) |
| Z3 | Request number **`PREFIX-YYYY-NNNNNN`**. The prefix is set per process; the counter is per process per year (installation time zone); assigned **when the request is submitted**; never reused |
| Z4 | "Start a request" lists processes from **enabled** apps' **active** versions that the user may start (WF-06), grouped by app, with a search box |
| Z5 | "My requests" shows the status (WF W10), the current step and who it is with, e.g. "Credit Officers (3)" while unclaimed |

## 5. Acceptance criteria (draft for spec.md)
**INB-01 Start a request**
- INB-01-AC1: The list shows only processes of enabled apps' active versions that the user may start (WF-06, APP-07).
- INB-01-AC2: Starting a process opens its start form from the active version.

**INB-02 Task lists**
- INB-02-AC1: My tasks shows only tasks assigned to me; Available shows tasks offered to me that nobody has claimed.
- INB-02-AC2: Claiming a task that someone else claimed a moment earlier shows a clear message, and the list refreshes (I3).
- INB-02-AC3: Lists are paged (25 per page), sortable, filterable by app/process, and searchable by request number (Z1).
- INB-02-AC4: With 10,000 requests and 50,000 tasks, a list page loads in under 1 second (NFR-02).

**INB-03 Task screen**
- INB-03-AC1: The form shows only the fields allowed at this step, editable or view-only as configured (WF-03).
- INB-03-AC2: Only the allowed actions appear; a required comment is enforced (WF-03).
- INB-03-AC3: An "All must approve" step shows how many have approved (WF-14).

**INB-04 My requests**
- INB-04-AC1: Shows each request I started with status, current step and who it is with (Z5).
- INB-04-AC2: Opening one shows the form read-only and the full history (WF-11).

**INB-05 Request number**
- INB-05-AC1: The first request of a process in 2027 gets `PREFIX-2027-000001`; numbers never repeat, even when two requests are submitted at the same moment (Z3).

**INB-07 Home counters (new scope ID)**
- INB-07-AC1: The home page shows the number of my tasks, available tasks and my requests in progress, matching the lists.

**Admin (WF-07)**
- INB-Z2-AC1: The Unassigned queue lists tasks with their reason; the admin can reassign one to an active user.

## 6. Design notes (input for plan.md, not for spec.md)
- **Read side in Runtime:** tasks and requests tables with indexes for the list queries:
  (assignee, status, created), (candidate user, status) for offered tasks, (requester, status), request number (unique).
  Keyset or offset paging (25 rows); counters use `COUNT` on the same indexes.
- **Candidates of offered tasks** ("Any one" mode) in a separate table (task, user) so "Available to me" is one index seek.
- **Request number (Z3):** one counter row per (process, year), incremented with an `UPDATE … OUTPUT` (or a
  `sp_getapplock`) inside the submit transaction, so numbers are unique and gap-free under concurrency.
  SQL Server sequences are not used because they would need one sequence per process per year.
- **Claim race (I3):** `rowversion` on the task (WF-12); the losing claim returns 409 as ProblemDetails, and the UI shows the message and refreshes.
- **Waiting time** is computed from the task's created time (UTC) and shown in the user's local time (NFR-05).
