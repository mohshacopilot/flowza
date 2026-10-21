# ADR-0002: Form data: original in SQL Server, optional read copy (SQL Server or MongoDB)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-16 |
| **Decided by** | Owner |
| **Related** | [ADR-0001](0001-audit-service-pluggable-store.md) (same pluggable-store pattern) |

## Context
- Form data has **no fixed shape**. Designers create the fields, and forms change between app versions.
  Grids and sub-forms (e.g. co-applicants) are lists inside a record.
- A **document database (MongoDB)** fits this shape naturally.
- But when someone submits or approves, the Runtime service must save these **together or not at all**:
  form data, request state, tasks, history, and the outbox message. That is simple only when they are
  all in **one SQL Server database** (one transaction).
- Lists, search, filters and dashboards read **many records with flexible filters**. As data grows,
  these heavy reads should not slow down approvals.

## Decision
1. **The original (source of truth) is SQL Server, inside the Runtime service.** Each form submission is
   one row: fixed columns (record ID, app, form, app version, request, created by/at, data version) plus
   one **JSON column** with all field values. Submit and approve are one transaction.
2. Every time form data changes, Runtime publishes a **`FormDataSaved`** event through the outbox,
   in the same transaction. The event carries the record ID, form, app version, the **data version**
   (a number that goes up by 1 on every change to that record), and a **full snapshot** of the field values
   (not just the changes).
3. **Later, when lists and search need it**, a separate **Datalist service** keeps a **read copy**:
   - It subscribes to `FormDataSaved` and writes the snapshot into its own store: insert it if new,
     replace it if it exists.
   - It **ignores** an event whose data version is older than the one it already has (handles duplicates
     and wrong order).
   - Its store sits behind an interface (`IFormDataReadStore`) with **two implementations, SQL Server or
     MongoDB**, chosen by **dependency injection** from configuration, exactly like the Audit service
     (ADR-0001).
4. **Which screen reads from where:**
   | Screen | Reads from |
   |---|---|
   | Task screen, request detail, My tasks (inbox), anything before a change | **Runtime (the original)** |
   | Datalists, search, filters, dashboards, reports | **Datalist service (the copy)** |

   Rule: **if you are going to change the data, read the original. If you are only looking, read the copy.**
5. **Rebuild:** if the copy is lost or damaged, the Datalist service asks **Runtime, through its API**,
   to re-send all records. It never reads Runtime's database directly (constitution 3.1).

## How data moves (example: a loan application is submitted)
1. The user clicks **Submit**.
2. Runtime saves form data, request state, new task, history and the `FormDataSaved` outbox message in
   **one SQL Server transaction**. The user sees "Submitted".
3. The outbox sender (a background job in Runtime) sends the message to the message transport.
4. The Datalist service receives it and writes the document into its read store (SQL Server or MongoDB).
5. Lists and search now show the application, usually **less than a second** after the submit.

If the read store is down, messages **wait in the queue** and nothing is lost. Submit and approve keep
working because they only use Runtime's SQL Server.

## Options considered
| Option | Why not chosen |
|---|---|
| All form data only in MongoDB | Breaks the single transaction with the workflow; risk of form data and workflow state disagreeing |
| All of Runtime in MongoDB | Needs a replica-set setup even on one server; our outbox is built for SQL Server; many on-prem customers only approve SQL Server |
| A table per form (Joget style) | The platform would change table structures while running, which is risky with versions and disliked by bank DBAs |
| One row per field (EAV) | Very slow and complex to query |
| Only SQL Server, no read copy ever | Fine for small installations, but heavy lists and search would compete with approvals at large ones |

## Consequences
**Good**
- Submit and approve stay **safe and simple** (one transaction).
- Lists and search can scale separately, and large customers can choose MongoDB for the copy.
- A classic **CQRS + polyglot persistence** design: relational for writes, documents for reads, synced by events.

**Costs**
- Data is stored **twice** when the copy is used.
- The copy is **slightly behind** the original (eventual consistency). Screens that must be exact read the original.
- More moving parts: the Datalist service and its store must be run and backed up.
- `FormDataSaved` carries form values, so the message transport holds personal data for a short time.
  Messages are internal only, and processed messages must be cleaned up quickly (constitution Article 11).
  The Audit service records only IDs from this event, never the values (ADR-0001).

## Timing
| Version | What |
|---|---|
| **v0.1** | Original in SQL Server (JSON column) **and** the `FormDataSaved` event with data version and snapshot. No read copy yet |
| **v0.2** | Datalists read **directly from Runtime** (through its API) to keep things simple |
| **Later, when needed** | The Datalist service with the read copy (SQL Server or MongoDB by DI); lists and search switch to the copy |
