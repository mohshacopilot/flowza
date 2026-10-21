# Feature 005: Workflow engine (WF)

| | |
|---|---|
| **Status** | **Draft**; clarifications C1–C12 answered by the owner on 2026-10-21; waiting for approval |
| **Date** | 2026-10-21 |
| **Modules** | WF (Workflow engine) |
| **Scope IDs** | WF-01 to WF-09, WF-11 to WF-15 ([v0.1 scope](../v0.1-scope.md)); reference scenarios RS-1 and RS-2. WF-10 is in v0.2 |
| **Sources** | [WF discussion](../../docs/discussions/workflow-engine.md) (W1–W10, X1–X5, Y1–Y8), [v0.1 scope](../v0.1-scope.md), [ORG discussion](../../docs/discussions/org-hierarchy.md), [ADR-0003](../../docs/decisions/0003-form-data-storage-layers.md), [constitution](../../docs/constitution.md) |
| **Next documents** | `plan.md` (how), `tasks.md` (steps); both only after this spec is approved |

> **This spec says what and why, never how.** Technology choices (frameworks, libraries, protocols) belong in
> `plan.md`. The only technical words here are **constraints the customer gives us** (e.g. "runs on SQL Server
> 2019"), because they are requirements, not design choices.

## 1. Summary
The workflow engine moves a request through the steps a designer drew. At each step it creates tasks for the
right people, applies their actions (approve, reject, send back), evaluates conditions, runs parallel checks,
sends designed emails and records everything in the request's history. An App Designer draws the process in a
visual designer, and the platform refuses to publish a process that could not run.

The engine never knows what "leave" or "loan" means. It only knows nodes, arrows and rules.

**Why it matters:** this is the heart of the product. Every business process a customer builds (leave,
loans, approvals of any kind) runs through it. If a task reaches the wrong person, two people approve the same
thing, or a step is lost, the customer's decisions are wrong or cannot be proven later.

**What other features own** (this spec only references them):
| Topic | Owned by |
|---|---|
| Approver rules a–g, who they resolve to, vacant heads, self-approval, the reason for an empty result | [002-org-hierarchy](../002-org-hierarchy/) (ORG-06) |
| Condition expressions, blank values, checks when a design is saved | [003-expression-language](../003-expression-language/) (EXP-01 to EXP-07) |
| Apps, versions, publish, active version, restart on a new version (APP-05), disabling an app | [004-apps-and-form-builder](../004-apps-and-form-builder/) (APP) |
| Forms, field validation, step-wise saving rules, "required at this step" rules, grids | [004-apps-and-form-builder](../004-apps-and-form-builder/) (FRM-05, FRM-13, FRM-14) |
| Inbox screens, task screen, Unassigned queue screen, request numbers, emails and templates | [006-inbox-notifications](../006-inbox-notifications/) (INB, NTF) |

## 2. Users
| Persona | What they need from this feature |
|---|---|
| **App Designer** | Draw processes, configure each step, and publish only processes that can run |
| **Requester** | Start a request they are allowed to start, fix it when it is sent back, reopen it when it was rejected (if allowed), and see what happened |
| **Approver** | Receive the right tasks, claim or release shared ones, and approve, reject or send back |
| **Platform Admin** | Find tasks that nobody could be found for and give them to the right person |

## 3. User stories and acceptance scenarios
Priority **P1** = needed for Milestone 1; **P2** = Milestone 2.

### US1: Draw a process (P1, WF-01, WF-02)
As an App Designer, I draw a process with nodes and arrows, so that a business process is built by configuration, not code.
1. **Given** an app draft, **when** the designer drags Start, Approval, Condition, Parallel split, Parallel join, Notification and End nodes onto the canvas and connects them with arrows, **then** the process is saved in the draft, and it reopens with the same layout (X4).
2. **Given** a Start node, **then** it is linked to the process's start form.
3. **Given** an End node, **then** the designer chooses its outcome: Approved, Rejected or Completed.
4. **Given** a Condition node with several outgoing arrows, **then** the designer sets the order in which they are checked and marks one as the default path (W6).

### US2: Configure an Approval step (P1; P2 for some settings; WF-03)
As an App Designer, I configure each Approval step, so that the right people see the right fields and can take only the right actions.
1. **Given** an Approval node, **then** the designer sets: a name, an approver rule (feature 002), an approval mode (WF-14), the maker-checker tick (on by default, WF-15), the allowed actions (Approve, Reject, Reject (final), Send back), whether a comment is required for Reject/Send back, and which form fields the approver can see or edit.
2. *(M2)* **Then** the designer can also tick fields as "required at this step" (CV2), set grids as hidden, view-only or editable, and choose "All must approve".
3. **When** an approver opens the task, **then** only the allowed actions are offered, and only the fields allowed at this step are shown or editable.

### US3: Publish only processes that can run (P1; P2 for one check; WF-04, WF-05)
As an App Designer, I want the platform to stop me from publishing a broken process, so that no request gets stuck.
1. **When** the designer publishes a version whose process has errors, **then** every error is listed, each error highlights its node, and nothing is published.
2. **Given** two branches of one parallel section that can both edit the same field, **when** the designer publishes, **then** it is refused (ADR-0003).
3. *(M2)* **Given** an "All must approve" step that makes a field editable, **when** the designer publishes, **then** it is refused (Y6).
4. **Given** running requests, **when** a new version is published and made active, **then** the running requests keep following the process of the version they started on (WF-05; versions are owned by 004).

### US4: Decide who may start a process (P1, WF-06)
As an App Designer, I choose who may start a process, so that only the right people can raise a request.
1. **Given** a process that only business role R may start, **when** a user without role R tries to start it, **then** it is refused, even if they call the platform directly.
2. **Given** a process limited to certain org units, **then** users whose primary unit is one of those units or below it may start it; a user who matches a chosen role or a chosen unit is enough (C6).

### US5: Tasks reach the right people (P1, WF-07, WF-14 "Any one")
As an approver, I get exactly the tasks meant for me, so that work is neither lost nor done twice.
1. **When** a request reaches an Approval node, **then** its approver rule is resolved with the org data at that moment (APP-06).
2. **Given** the rule finds one user, **then** the task is assigned to that user.
3. **Given** the rule finds several users and the step is in "Any one" mode, **then** the task is offered to all of them; the first to claim it owns it, and the others no longer see it as available.
4. **Given** a claimed task, **when** the claimant releases it, **then** it is offered to the whole group again (W5).
5. **Given** the rule finds nobody, **then** the task goes to the Unassigned queue with the reason, and a Platform Admin reassigns it to an active user.
6. **Given** the directory cannot be reached while the next approver is being found, **when** a user acts, **then** the action fails, nothing is saved, and the user sees "Please try again in a moment" (W8).
7. **When** an admin deactivates a user, **then** that user's open tasks move to the Unassigned queue (Q4).

### US6: Approve and reject (P1, WF-08)
As an approver, I approve or reject a request, so that the decision is made and recorded.
1. **When** the approver approves, **then** the fields they may edit are validated and saved, and the request moves on.
2. **When** the approver rejects, **then** the whole request ends as Rejected and all its open tasks are cancelled; the requester may reopen it if the process allows (WF-13).
3. **When** the approver chooses Reject (final), **then** the request ends as Rejected (final) and can never be reopened.
4. **Given** a parallel section, **when** an approver in one branch rejects, **then** the whole request ends and all open tasks in the other branches are cancelled (X2).
5. **When** the approver rejects or sends back with invalid or changed field values, **then** the action succeeds, nothing is validated, the field changes are not saved, and only the comment is kept (CV3).

### US7: Send back and resubmit (P1, WF-08)
As an approver, I send a request back to the requester to fix it, so that small mistakes do not need a new request.
1. **When** the approver sends a request back, **then** it goes to the requester (only the requester, W2) with the status Sent back.
2. **When** the requester edits it (all fields they could edit at submission, Q5) and resubmits, **then** it is validated and returns to the same Approval node.
3. **Given** a parallel section, **when** an approver in one branch sends back, **then** the open tasks in the other branches are cancelled, and after resubmission the request returns to the parallel split, so all branches run again (W3).

### US8: Route by conditions and send designed emails (P1, WF-02)
As an App Designer, I route requests by their data and notify people on the way, so that one process serves many cases.
1. **Given** a Condition node, **when** two branches are true, **then** the first one in the designer's order is taken; **when** none is true, **then** the default path is taken (W6).
2. **Given** a branch whose expression gives blank (for example an empty field), **then** that branch is not taken (EXP-07, feature 003).
3. **When** a request reaches a Notification node, **then** the request moves on at once; the email is sent in the background (X1; recipients and texts are owned by 006).
4. **When** a request reaches an End node, **then** it finishes with that End's outcome.

### US9: Parallel checks (P1, WF-09)
As an App Designer, I let several teams check a request at the same time, so that the request is decided faster.
1. **When** a request reaches a Parallel split, **then** every branch starts.
2. **Given** some branches have finished and one has not, **then** the request waits at the Parallel join.
3. **When** the last branch finishes, **then** the request continues past the join.

### US10: Four eyes (P1, WF-15)
As a business owner, I want each approval step to be done by a different person from earlier approvers, so that nobody can approve their own work twice.
1. **Given** the maker-checker tick is on (default), **when** a user who approved an earlier step of the request would get the task, **then** they are left out.
2. **Given** that leaves nobody, **then** the task goes to the Unassigned queue with the reason.
3. **Given** the tick is off, **then** the same user may act at both steps.

### US11: Everyone can see what happened (P1, WF-11)
As a requester, approver or admin, I see the full history of a request, so that every decision can be explained later.
1. **When** anyone acts on a request, **then** the history shows who, what, when, the comment, the changed fields (old → new) and, for tasks, the approver-rule explanation.
2. **Given** a user who can view the request, **then** they see all comments (W7).

### US12: Two people never both act on one task (P1, WF-12)
As the business, I need one decision per task, so that two people never approve or claim the same thing.
1. **When** two users act on the same task at the same moment, **then** exactly one succeeds and the other sees a clear message.
2. **When** two users claim the same offered task at the same moment, **then** exactly one owns it and the other sees a clear message.

### US13: Reopen a rejected request (P1, WF-13)
As a requester, I reopen my rejected request, fix it and send it again, so that I do not start from nothing.
1. **Given** the process allows reopening and the request is Rejected, **when** the requester reopens it, **then** it keeps its request number, full history and app version, and shows Waiting for resubmission.
2. **When** the requester resubmits, **then** the request restarts from the first step, so all approvals happen again.
3. **Given** a request rejected with Reject (final), or a process that does not allow reopening, **then** reopening is refused.
4. **Then** a request can be reopened any number of times.

### US14: Clear request statuses (P1, W10)
As a requester or admin, I see a clear status for each request, so that I know where it stands.
1. **Then** every request has one of these statuses: In progress, Sent back, Waiting for resubmission, Approved, Rejected, Rejected (final), Completed.

### US15: A committee where everyone must approve (P2, WF-14 "All must approve")
As an App Designer, I make a step need the approval of every member of a group, so that committee decisions are modelled correctly.
1. **Given** an "All must approve" step that resolves to three users, **then** each of them gets their own task.
2. **When** two have approved, **then** the task screen shows "2 of 3 approved" with each person's decision, and the request waits.
3. **When** the third approves, **then** the request moves on.
4. **When** any one of them rejects, **then** the request ends as Rejected (or Rejected (final)) and the other open tasks of the step are cancelled (Y4).
5. **When** any one of them sends back, **then** the other open tasks are cancelled; after resubmission the step starts again and everyone approves again (Y5).

### US16: The reference scenarios run end to end (P1 for RS-1 and RS-2; P2 for the extended RS-2)
As the owner, I want the two reference processes to run using only configuration, so that the platform is proven generic.
1. **Given** RS-1 (HR leave) and RS-2 (NBFC loan) built through the screens only, **then** they run end to end without any code change.
2. *(M2)* **Given** the extended RS-2 (co-applicants grid and a sanction committee with "All must approve"), **then** it runs end to end the same way.

## 4. Requirements
| ID | Requirement | Milestone |
|---|---|---|
| WF-01 | Visual process designer: drag-and-drop nodes and arrows. Node positions are saved; each publish error highlights its node (X4) | M1 |
| WF-02 | Node types: **Start** (linked to a start form), **Approval** (human task), **Condition** (branches checked in the designer's order; the first true one is taken, otherwise the default path, W6; a blank result is not taken, EXP-07), **Parallel split** and **Parallel join**, **Notification** (email; never stops the flow, X1), **End** (outcome: Approved, Rejected or Completed; an End with outcome Rejected gives status Rejected, which the requester can reopen if the process allows, C1) | M1 |
| WF-03 | Each Approval node has: a name, an approver rule (ORG-06, feature 002), an approval mode (WF-14), the maker-checker tick (WF-15), allowed actions (Approve, Reject, Reject (final), Send back), whether a comment is required for Reject, Reject (final) and Send back (one setting for all three, C10), which form fields (and grids) the approver can see, edit or must fill ("required at this step"). The "email new tasks to assignees" setting is not shown in v0.1; it comes with NTF-03 in v0.2 (C9). **M2:** "required at this step" (CV2), grid permissions (FRM-13) and choosing "All must approve" | M1 / M2 |
| WF-04 | Checks before publishing: exactly one Start; at least one End; every node reachable from Start; every node can reach an End; every Condition has a default path; every Parallel split has a matching join, and every path from a split reaches its own join (an End inside a branch is an error; Conditions and nested parallel sections inside a branch are allowed if they stay inside it, C7); no loops in the diagram; parallel branches never share an editable field (ADR-0003); a start permission is chosen (at least one entry, or "All users", C6); approver rules refer only to existing codes and usernames (unknown → error; deactivated unit type or role, or inactive user → warning; 002 C8, extends P10). Each error points to its node; a draft with errors is saved, but it is not published (004 C10). **M2:** "All must approve" steps have no editable fields (Y6) | M1 / M2 |
| WF-05 | Processes are versioned as part of their app (APP-02 to APP-05, feature 004); there are no separate process versions. A running request follows the process of its own version | M1 |
| WF-06 | Start permission: the designer chooses which business roles and/or org units may start the process, or "All users". A user may start when they hold **any** chosen role **or** their primary unit is a chosen unit **or below it**. Choosing nothing is a publish error (C6). The server refuses everyone else | M1 |
| WF-07 | Task assignment: the approver rule is resolved when the task is created, using the org data at that moment (APP-06). One user: assigned. Several users: depends on the approval mode (WF-14); in "Any one" mode the task is offered to all, the first to **claim** owns it, and a claimant can **release** it (W5). Nobody: **Unassigned** queue with the reason; a Platform Admin reassigns it. Admins can reassign any open task (W9); the target must pass the same checks as automatic assignment: active, not the requester, maker-checker when on, and not already holding a task in the same "All must approve" step (C5). A deactivated user's open tasks move to the Unassigned queue (Q4); for a task offered to several users and not yet claimed, only that user is removed from the candidates, and the task moves to the Unassigned queue only when no candidate is left (C4). If the directory cannot be reached, the action fails cleanly: nothing is saved and the user sees "Please try again in a moment" (W8) | M1 |
| WF-08 | Actions. **Approve:** validates and saves the fields editable at this step, and the request moves on. **Reject:** the whole request ends as Rejected and all open tasks (also in other parallel branches, X2) are cancelled; it may be reopened (WF-13). **Reject (final):** the same, but never reopenable. **Send back:** to the requester only (W2), who edits and resubmits; the request returns to the same Approval node, whose approver rule is resolved again with the org data at that moment (C3, APP-06); inside a parallel section the other branches' tasks are cancelled and, after resubmission, the request returns to the split (W3). Reject, Reject (final) and Send back do not validate or save field changes; only the comment is kept (CV3). Every action happens completely or not at all (X5) | M1 |
| WF-09 | Parallel: the join continues only when all branches have finished | M1 |
| WF-11 | History: every action is recorded with who, what, when, comment, the old and new values of changed fields, and the approver-rule explanation. Automatic steps are recorded as **system entries**: which Condition branch was taken, parallel split and join, Notification sent (C8). Everyone who can view the request sees all comments (W7). History is never deleted | M1 |
| WF-12 | If two people act on the same task at the same time (including claiming), only the first succeeds; the second sees a clear message. Never cut from v0.1 | M1 |
| WF-13 | Reopen: if the designer allows it for the process, the requester can reopen a rejected request, edit it and resubmit it. It keeps the same request number, its full history and its app version. After resubmitting, it restarts from the first step. No limit on how many times. A request rejected with Reject (final) cannot be reopened | M1 |
| WF-14 | Approval mode per Approval node: **Any one** (offered to all; first claim decides) or **All must approve** (one task per person after removing duplicates and applying the maker-checker and self-approval filters, Y2; moves on when all approve, with progress such as "2 of 3 approved", Y3; any Reject ends the request and cancels the other tasks, Y4; any Send back means everyone approves again after resubmission, Y5; fields are view-only, comments allowed, Y6; a deactivated approver's task alone moves to the Unassigned queue, Y7; one user found behaves like "Any one", nobody found goes to the Unassigned queue, Y8). The mode is part of the published design and cannot change on a running request (Y1). **M1:** "Any one". **M2:** "All must approve" | M1 / M2 |
| WF-15 | Maker-checker: per Approval node, "Must be a different person from earlier approvers", **on by default**. "Earlier approvers" are the users who approved **other** steps of the request **since the last submit, resubmit or reopen**; approvals of the same step, and of earlier rounds, do not count (C2). If nobody remains, the task goes to the Unassigned queue (W4) | M1 |
| WF-W10 | Request statuses: In progress, Sent back, Waiting for resubmission, Approved, Rejected, Rejected (final), Completed (meanings in section 8). "Waiting for resubmission" after an admin restart comes with APP-05 (feature 004, M3). Withdrawn comes in v0.2 | M1 |
| RS-1 | The HR leave scenario (scope section 4) is built through the screens only and runs end to end. Verified once 006 (inbox and emails) is built; until then the engine part is proven by service-level tests of the same scenario (C12) | M1 |
| RS-2 | The NBFC loan scenario (scope section 4) is built through the screens only and runs end to end (verified as for RS-1, C12). **M2:** the **extended RS-2** does the same: RS-2 as in the scope (three parallel checks) plus a co-applicants grid, with the Zone head step for loans above 10,00,000 replaced by a **Sanction committee** step (a new business role, rule f, "All must approve") (C11) | M1 / M2 |

## 5. Acceptance criteria
Every criterion gets at least one automated test whose name contains its ID (constitution 1.4), unless marked *manual*.

| ID | Criterion |
|---|---|
| WF-01-AC1 | The designer adds each node type, connects nodes with arrows, moves them and saves; reopening the draft shows the same nodes, arrows and positions (X4) |
| WF-01-AC2 | Nodes can be added, connected and configured using the keyboard only (NFR-11) |
| WF-02-AC1 | With two true branches, the first in the designer's order is taken; with none, the default path (W6, EXP-07) |
| WF-02-AC2 | A branch whose expression gives blank is not taken (EXP-07) |
| WF-02-AC3 | A request that reaches a Notification node moves on at once, even when the email cannot be sent at that moment (X1) |
| WF-02-AC4 | A request that reaches an End with outcome Approved or Completed finishes with that status; an End with outcome Rejected gives status Rejected, and the requester can reopen it when the process allows reopening (C1) |
| WF-03-AC1 | All Approval node settings are saved and published with the version; the maker-checker tick is on for a new node |
| WF-03-AC2 | The approver is offered only the allowed actions; any other action is refused by the server |
| WF-03-AC3 | When a comment is required for Reject/Send back, the server refuses those actions without a comment |
| WF-03-AC4 | The approver sees only the fields visible at this step; values sent for fields not editable at this step are ignored (ADR-0003) |
| WF-03-AC5 | *(M2)* A field ticked "required at this step" is required only at that step (CV2); a grid is hidden, view-only or editable as set for the step |
| WF-03-AC6 | When a comment is required, Reject (final) without a comment is refused too (C10); the Approval node settings in v0.1 have no "email new tasks" option (C9) |
| WF-04-AC1 | Each failed check (one Start, an End, reachability, can reach an End, default path, split/join pairs, no loops) is reported and highlights its node; nothing is published |
| WF-04-AC2 | Two parallel branches that can edit the same field are rejected (ADR-0003) |
| WF-04-AC3 | *(M2)* An "All must approve" step that makes a field editable is rejected (Y6) |
| WF-04-AC4 | A branch of a parallel section that leads to an End, or leaves the section without reaching its own join, is a publish error; a Condition or a nested parallel section that stays inside the branch is accepted (C7) |
| WF-04-AC5 | An approver rule with an unknown unit type, unit or role code, or an unknown username, is a publish error; a deactivated unit type or role, or an inactive user, gives a publish warning (002 C8, P10) |
| WF-04-AC6 | A process with no start permission chosen (no role, no unit, not "All users") is a publish error (C6) |
| WF-04-AC7 | A draft process with errors is saved and lists its errors; publishing is refused until they are fixed (004 C10) |
| WF-05-AC1 | A running request keeps following its own version's process after a new version is published and made active |
| WF-06-AC1 | A user outside the start permission cannot start the process, also by calling the platform directly; a user inside it can |
| WF-06-AC2 | With start permission "role Loan Officer, unit South Zone": a Loan Officer anywhere may start; a user without the role whose primary unit is a branch below South Zone may start; a user with neither may not; with "All users" every active user may start (C6) |
| WF-07-AC1 | One resolved user → task assigned; several in "Any one" mode → offered, first claim owns it; nobody → Unassigned queue with the reason |
| WF-07-AC2 | A claimant can release a task; it is offered to the group again (W5) |
| WF-07-AC3 | If the directory cannot be reached, the action fails, nothing changes, and the user sees a retry message (W8) |
| WF-07-AC4 | The approver rule is resolved when the task is created; a later org change does not change the assignee of an existing task (APP-06) |
| WF-07-AC5 | A Platform Admin reassigns a task from the Unassigned queue, or any open task, to an active user; the history records it (W9) |
| WF-07-AC6 | When a user is deactivated, the open tasks assigned to or claimed by them move to the Unassigned queue (Q4) |
| WF-07-AC7 | A task offered to three users and not claimed: when one is deactivated, the other two still see it; when all three are deactivated, it moves to the Unassigned queue (C4) |
| WF-07-AC8 | An admin reassign to an inactive user, to the requester, to a user excluded by maker-checker, or to a user who already holds a task in the same "All must approve" step, is refused (C5) |
| WF-08-AC1 | Approve moves the request on; Reject ends it as Rejected and cancels open tasks; Reject (final) ends it so it can never be reopened |
| WF-08-AC2 | Send back inside a parallel section cancels the other branches' tasks; after resubmission the request returns to the split (W3) |
| WF-08-AC3 | A Reject in one parallel branch ends the whole request and cancels all open tasks in the other branches (X2) |
| WF-08-AC4 | Send back (outside a parallel section) gives status Sent back; after the requester resubmits, the request returns to the same Approval node |
| WF-08-AC5 | Reject, Reject (final) and Send back succeed with invalid field values, and those field changes are not saved; the comment is kept (CV3) |
| WF-08-AC6 | Approve with invalid values is refused with field messages, and the request does not move |
| WF-08-AC7 | If any part of an action fails, nothing of that action is saved: task, request status, form data and history stay as before (X5) |
| WF-08-AC8 | After a send back and resubmission, the approver rule of the step is resolved again: when the approver who sent it back has moved to another unit, the task goes to the person the rule finds now (C3) |
| WF-09-AC1 | With one branch still open, the request waits at the join; when the last branch finishes, it continues |
| WF-11-AC1 | Every action appears in the history with who, what, when, comment, changed fields (old → new) and the approver-rule explanation |
| WF-11-AC2 | Every user who can view the request sees all its comments (W7) |
| WF-11-AC3 | The history shows system entries for the Condition branch taken (with the branch name), parallel split and join, and Notification sent (C8) |
| WF-12-AC1 | When two users act on the same task at once, exactly one succeeds; the other sees a clear message |
| WF-12-AC2 | When two users claim the same offered task at once, exactly one owns it; the other sees a clear message |
| WF-13-AC1 | With reopening allowed, the requester reopens a Rejected request; it keeps its number, history and version and shows Waiting for resubmission; after resubmission it starts again at the first step |
| WF-13-AC2 | Reopening is refused for Rejected (final) requests and when the process does not allow it |
| WF-13-AC3 | The same request can be rejected and reopened three times in a row |
| WF-14-AC0 | A process step in "Any one" mode behaves as WF-07-AC1; the mode of a running request cannot be changed (Y1) |
| WF-14-AC1 | *(M2)* In "All must approve" mode with three approvers, the request moves on only after the third approval; the screen shows "2 of 3 approved" before that |
| WF-14-AC2 | *(M2)* One Reject among them ends the request and cancels the other tasks (Y4) |
| WF-14-AC3 | *(M2)* One Send back cancels the others; after resubmission all three must approve again (Y5) |
| WF-14-AC4 | *(M2)* Resolution removes duplicates and applies maker-checker and self-approval first; one user found → behaves like "Any one"; nobody → Unassigned queue (Y2, Y8) |
| WF-14-AC5 | *(M2)* When one of the approvers is deactivated, only their task moves to the Unassigned queue (Y7) |
| WF-15-AC1 | With the tick on (default), a user who approved an earlier step of the request is not given the task; if nobody remains, it goes to the Unassigned queue |
| WF-15-AC2 | With the tick off, the same user may act at both steps |
| WF-15-AC3 | A user who approved step 1, after the request is rejected, reopened and resubmitted, can approve step 1 again; in the same round that user is still excluded from step 2 (C2) |
| WF-W10-AC1 | Each status appears in its situation: In progress after submit, Sent back after a send back, Waiting for resubmission after a reopen, Approved or Completed at an End, Rejected after Reject or at an End with outcome Rejected (C1), Rejected (final) after Reject (final) |
| RS-1-AC1 | RS-1 runs end to end: submit; manager approves; more than 10 days goes to an HR Officer, otherwise ends Approved; send back and reject also work |
| RS-1-AC2 | *(manual checklist)* RS-1 is built through the screens only, with no code change, and shown in a demo |
| RS-2-AC1 | RS-2 runs end to end: Branch Manager approves; Credit, Legal and Valuer checks in the zone are offered, claimed and approved in parallel; the join waits for all three; the amount routes to the Branch head (up to 10,00,000) or the Zone head (above); the applicant's email field gets the notification; ends Approved |
| RS-2-AC2 | *(manual checklist)* RS-2 is built through the screens only, with no code change, and shown in a demo |
| RS-2-AC3 | *(M2)* The extended RS-2 runs end to end: co-applicants grid; loans above 10,00,000 go to a Sanction committee in "All must approve" mode instead of the Zone head (C11) |

## 6. Edge cases
- An approver's org data changes after the task was created: the task keeps its assignee (APP-06); an admin can reassign it.
- Maker-checker and self-approval (feature 002) together remove everyone: the task goes to the Unassigned queue with the reason.
- A user acts on a task that was cancelled a moment earlier (another branch rejected or sent back): the action is refused with a clear message, and nothing is saved.
- The last two approvers of an "All must approve" step approve at the same moment: the step moves on once, not twice.
- The last two branches of a parallel section finish at the same moment: the request passes the join once, not twice.
- The directory cannot be reached when the requester submits or resubmits: the same rule as W8 applies; nothing is saved and the user is asked to retry.
- A "Send back" happens in an "All must approve" step inside a parallel section: the other tasks of the step and of the other branches are cancelled; after resubmission the request returns to the split and everyone approves again (W3, Y5).
- An approver sends values for fields that are not editable at their step: those values are ignored (ADR-0003).
- An approver saves based on an old copy of the data and one of their fields was changed meanwhile: the save is refused with a clear message (ADR-0003; rule owned by 004).
- A Notification recipient has no email address: the request continues; the skip is handled by 006 (NTF-02).
- A rejected request is reopened after a newer app version became active: it stays on its own version (Q8.4).
- An app is disabled (APP-07, feature 004): its running requests continue.

## 7. Non-functional requirements that apply
From the [v0.1 scope](../v0.1-scope.md) section 6: NFR-01 (only the assignee or claimant acts; view rules), NFR-02 (a task action completes in under 1 second with 10,000 requests and 50,000 tasks, excluding email delivery), NFR-03 (no lost events; handling twice is safe), NFR-04 (SQL Server 2019 or later, no cloud-only services, Windows without containers), NFR-05 (UTC storage, local display), NFR-07 (no form values or personal data in logs), NFR-08 (no hard delete), NFR-09 (every saved process design stores its format version), NFR-10 (translation files), NFR-11 (keyboard and labels), NFR-12 (coverage).

## 8. Key entities (business view, not database design)
| Entity | Meaning |
|---|---|
| **Process** | A diagram of nodes and arrows inside an app version, with its start form, start permission and whether reopening is allowed. Published processes never change |
| **Node** | One element of a process: Start, Approval, Condition, Parallel split, Parallel join, Notification or End |
| **Arrow** | A path from one node to the next; from a Condition it carries an expression, an order and possibly the default mark |
| **Approval step settings** | Name, approver rule, approval mode, maker-checker tick, allowed actions, comment rule, field and grid permissions |
| **Request** | One run of a process for one requester: request number, app version, form data, status, current step(s) |
| **Task** | Work for a person at an Approval step: assigned, offered (waiting to be claimed), claimed, unassigned, completed or cancelled |
| **Unassigned queue** | Tasks for which nobody could be found, each with its reason, waiting for an admin |
| **History entry** | One recorded action: who, what, when, comment, changed fields (old → new), approver-rule explanation |
| **Request status** | In progress (moving through the steps) · Sent back (with the requester after a Send back) · Waiting for resubmission (with the requester after a reopen or an admin restart) · Approved / Completed (reached an End with that outcome) · Rejected (ended by Reject or at an End with outcome Rejected; may be reopened if the process allows) · Rejected (final) (can never be reopened) |

## 9. Out of scope
| Item | When |
|---|---|
| Requester withdraws an unfinished request (WF-10) and the Withdrawn status | v0.2 (G1) |
| Send back to an earlier step instead of the requester | Later (W2) |
| "N of M" approval | v0.3, with the authority matrix (W1) |
| Approver forwards a task; delegation and out of office | v0.3 (W9) |
| SLA timers and escalation | v0.3 (X3) |
| Private notes (comments visible only to some people) | Later (W7) |
| "New task" email to assignees (NTF-03) and its Approval node setting | v0.2 (G1, C9) |
| Sub-processes, timers, loops in processes | Later |
| Moving a running request into the middle of another version | Not planned (Q10); restart on a new version (APP-05) is used instead |
| Connectors to outside systems inside a process (e.g. credit bureau) | v0.3 |

## 10. Clarifications (answered by the owner, 2026-10-21)
| # | Question | Answer |
|---|---|---|
| C1 | An End with outcome Rejected: status and reopen? | **Status Rejected; reopenable if the process allows**, like a Reject action |
| C2 | Which earlier approvals count for maker-checker? | **Approvals of other steps since the last submit, resubmit or reopen**; the same step's earlier approvals do not count |
| C3 | Who gets the task after send back and resubmission? | **The approver rule is resolved again** with the org data at that moment (APP-06) |
| C4 | A deactivated candidate of an unclaimed offered task | **Removed from the candidates**; Unassigned queue only when no candidate is left |
| C5 | Do admin reassigns get the same checks? | **Yes**: active, not the requester, maker-checker when on, no second task in the same "All must approve" step |
| C6 | What does start permission mean? | **Any chosen role or a chosen unit (or below it)**; at least one entry or "All users" is required at publish |
| C7 | What may sit inside a parallel section? | **Every path reaches its own join** (End inside a branch is an error); Conditions and nested sections allowed if they stay inside |
| C8 | Automatic steps in the history? | **Yes, as system entries** |
| C9 | The "email new tasks" setting in v0.1 | **Not shown**; comes with NTF-03 in v0.2 |
| C10 | Does "comment required" cover Reject (final)? | **Yes**: one setting for Reject, Reject (final) and Send back |
| C11 | Shape of the extended RS-2 (M2) | **RS-2 as in the scope + co-applicants grid; the Zone head step is replaced by a Sanction committee** (new business role, rule f, "All must approve") |
| C12 | Where do the RS end-to-end criteria live? | **In this spec**, verified once 006 is built; service-level tests of the same scenarios until then |

## 11. Spec quality checklist (Claude checks before asking for approval)
- [x] Every requirement has an ID, a milestone and at least one acceptance criterion
- [x] No technology choices in the spec (only customer constraints)
- [x] Every acceptance criterion is testable; the manual checks are marked
- [x] Out-of-scope items are listed so nobody builds them by mistake
- [x] All clarifications answered (C1–C12, 2026-10-21)
- [ ] Approved by the owner
