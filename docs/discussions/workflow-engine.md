# Module discussion: WF (Workflow engine)

| | |
|---|---|
| **Status** | Decided by the owner (W1–W10), 2026-10-16. Details X1–X5 and Y1–Y8 proposed by Claude; Y1–Y8 confirmed by the owner ("Y ok", 2026-10-21) |
| **Feeds** | `/speckit-specify 005-workflow-engine` (sections 1–6) and `/speckit-plan` (section 7) |
| **Scope IDs** | WF-01 to WF-15 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) |
| **Related** | [ORG](org-hierarchy.md) (approver rules), [EXP](expression-language.md) (conditions), [FRM](form-builder.md) (field permissions, validation), [APP](apps-and-versions.md) (versions, restart), [ADR-0003](../decisions/0003-form-data-storage-layers.md) (step-wise saving) |
| **Owning services** | Design (process designer, publish checks), Runtime (engine, tasks, history) |

## What this file is
The result of the detailed discussion of the WF module. Sections 1–6 are **what and why**, the input for
`spec.md`. Section 7 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
The engine moves a request through the steps a designer drew. At each step it creates tasks for the right
people (ORG), applies their actions, evaluates conditions (EXP) and records everything in the history.
It never knows what "leave" or "loan" means; it only knows nodes, arrows and rules.

## 2. Example: RS-2 as the engine sees it
```
(Start: Loan form)
      │
[Approval: Branch officer review]   rule d (head of unit)
      │
<Parallel split>
   ├──[Approval: Credit check]      rule f (Credit Officer in ZONE and below), mode Any one → claim
   └──[Approval: Legal check]       rule f (Legal Officer …)
<Parallel join>                      waits for both (WF-09)
      │
<Condition>  LoanAmount <= 1000000 → [Approval: Branch head]                      (rule d)
             default               → [Approval: Sanction committee, mode All must approve] (rule f)
      │
[Notification: "Loan sanctioned" → applicant email field]
      │
(End: Approved)
```

## 3. Already decided (scope WF-01 to WF-13, Q3.x, APP, CV)
- Node types: Start, Approval, Condition, Parallel split/join, Notification, End.
- Actions: Approve, Reject (can be reopened), Reject (final), Send back to the requester (returns to the same step).
- Assignment: one user → assigned; several → offered (claim) or one task each (W1); none → Unassigned queue.
- Publish checks (WF-04) plus: no shared editable field across parallel branches (ADR-0003).
- Two people acting on one task at once: only the first succeeds (WF-12).
- Reopen restarts from the first step (WF-13); restart on a new version (APP-05).
- Reject and Send back do not validate or save field changes (CV3).

## 4. Owner's decisions
| # | Question | Decision |
|---|---|---|
| W1 | Several approvers at one step | The designer sets an **approval mode** per Approval node: **Any one** (offered to all; the first to claim decides) or **All must approve** (one task per person; the request moves on only when everyone approves). "N of M" comes with the authority matrix (v0.3) |
| W2 | Send back target | **Requester only** in v0.1; "send back to an earlier step" later |
| W3 | Send back inside a parallel section | Open tasks in the other branches are **cancelled**; after resubmission the request returns to the **parallel split**, so all branches run again |
| W4 | Maker-checker (four eyes) | Per Approval node tick **"Must be a different person from earlier approvers"**, default **ON**. If that removes everyone, the task goes to the Unassigned queue |
| W5 | Claimed by mistake | The claimant can **release** the task back to the group |
| W6 | Condition with several true branches | Branches are checked **in the order the designer sets**; the first true one wins, otherwise the default path |
| W7 | Comment visibility | Everyone who can view the request sees all comments (private notes later) |
| W8 | Identity unavailable when finding the next approver | The action **fails cleanly**: nothing is saved; the user sees "Please try again in a moment" |
| W9 | Approver forwards a task | **Later** (delegation, v0.3). Admins can reassign in v0.1 |
| W10 | Request statuses | **In progress · Sent back · Waiting for resubmission · Approved · Rejected · Rejected (final) · Completed** (Withdrawn from v0.2) |

### "All must approve" mode (W1), details proposed by Claude and confirmed by the owner
| # | Rule |
|---|---|
| Y1 | The approval mode is part of the **process design** (set by the App Designer, published with the version); it cannot be changed on a running request |
| Y2 | The approver rule is resolved once when the step starts (APP-06); each resolved user gets **their own task**. Duplicates are removed; maker-checker (W4) and self-approval (ORG O5) filters apply first |
| Y3 | The step completes when **every** task is approved. The task screen shows progress, e.g. "2 of 3 approved", with each person's decision |
| Y4 | **Any Reject** ends the whole request as Rejected (or Rejected (final)); the other open tasks of the step are cancelled |
| Y5 | **Any Send back** cancels the step's other open tasks; after resubmission the step starts again and **everyone approves again** (the data may have changed) |
| Y6 | In "All must approve" steps, form fields are **view-only** (the publish check enforces it), so several people never edit the same fields at once. Comments are still allowed |
| Y7 | If one of the approvers is deactivated, only **their** task moves to the Unassigned queue (Q4); an admin reassigns it |
| Y8 | Resolution finding one user behaves like "Any one"; finding nobody sends the step to the Unassigned queue |

## 5. Details proposed by Claude (no objection from the owner)
| # | Detail |
|---|---|
| X1 | A Notification node never stops the flow; emails are sent in the background (NTF-05) |
| X2 | A Reject in any parallel branch ends the whole request and cancels all other open tasks (WF-08) |
| X3 | Task screens show the current step name, how long it has been waiting, and the history timeline. SLA timers come in v0.3 |
| X4 | The process designer saves node positions; each publish error highlights its node (WF-04) |
| X5 | Every action is **one database transaction**: task, request state, form data, history and outbox events together (ADR-0002) |

### Status meanings (W10)
| Status | Meaning |
|---|---|
| In progress | Moving through the steps |
| Sent back | With the requester after a Send back |
| Waiting for resubmission | With the requester after an admin restart (APP-05) or a reopen (WF-13) |
| Approved / Completed | Reached an End with that outcome |
| Rejected | Ended by Reject; may be reopened if the process allows (WF-13) |
| Rejected (final) | Ended by Reject (final); can never be reopened |

## 6. Acceptance criteria (draft for spec.md)
**WF-04 Publish checks**
- WF-04-AC1: Each failed check (one Start, an End, reachability, can reach an End, default path, split/join pairs, no loops) is reported and highlights its node.
- WF-04-AC2: Two parallel branches that can edit the same field are rejected (ADR-0003).
- WF-04-AC3: An "All must approve" step that makes a field editable is rejected (Y6).

**WF-07 Assignment**
- WF-07-AC1: One resolved user → task assigned; several in "Any one" mode → offered, first claim owns it; nobody → Unassigned queue with the reason.
- WF-07-AC2: A claimant can release a task; it is offered to the group again (W5).
- WF-07-AC3: If Identity cannot be reached, the action fails, nothing changes, and the user sees a retry message (W8).

**WF-08 Actions**
- WF-08-AC1: Approve moves the request on; Reject ends it as Rejected and cancels open tasks; Reject (final) ends it so it can never be reopened.
- WF-08-AC2: Send back inside a parallel section cancels the other branches' tasks; after resubmission the request returns to the split (W3).

**WF-02 Condition**
- WF-02-AC1: With two true branches, the first in the designer's order is taken; with none, the default path (W6, EXP-07).

**WF-14 Approval mode (new scope ID, W1)**
- WF-14-AC1: In "All must approve" mode with three approvers, the request moves on only after the third approval; the screen shows "2 of 3 approved" before that.
- WF-14-AC2: One Reject among them ends the request and cancels the other tasks (Y4).
- WF-14-AC3: One Send back cancels the others; after resubmission all three must approve again (Y5).

**WF-15 Maker-checker (new scope ID, W4)**
- WF-15-AC1: With the tick on (default), a user who approved an earlier step of the request is not given the task; if nobody remains, it goes to the Unassigned queue.
- WF-15-AC2: With the tick off, the same user may act at both steps.

**WF-11 / WF-12**
- WF-11-AC1: Every action appears in the history with who, what, when, comment, changed fields (old → new) and the approver-rule explanation.
- WF-12-AC1: When two users act on the same task at once, exactly one succeeds; the other sees a clear message.

## 7. Design notes (input for plan.md, not for spec.md)
- **Process = directed graph** (nodes and arrows) stored as JSON in the app version. Publish checks:
  | Check | Algorithm | Cost |
  |---|---|---|
  | Reachable from Start | BFS | O(V+E) |
  | Can reach an End | BFS on the reversed graph | O(V+E) |
  | No loops | DFS with three colours | O(V+E) |
  | Split/join pairs match | DFS with a stack (well-structured blocks) | O(V+E) |
  | No shared editable field across branches | Intersect each branch's editable-field set | O(fields) |
- **Token-based execution** (like BPMN): a request holds tokens on nodes; a split creates one token per
  branch; a join waits for all its branch tokens. Send back in a branch (W3) removes the block's tokens and
  places one token back on the split after resubmission.
- **Patterns:** **State** (request and task status; illegal moves impossible), **Command** (Approve, Reject,
  SendBack, Claim, Release…, all validated and executed the same way), **Strategy** (node behaviour by node
  type; new node types later without touching existing code).
- **Approval mode:** "Any one" = one task with several candidates (claim); "All must approve" = one task per
  user plus a step counter; both share the same task table.
- **WF-12:** `rowversion` on the task; the second action fails with a clear message.
- **W8:** the engine calls Identity's resolve API **before** committing; on failure the transaction is not
  started and the API returns a retryable error (HTTP 503 as ProblemDetails).
- **History:** append-only rows: who, what, when, comment, changed fields (old → new), rule explanation.
