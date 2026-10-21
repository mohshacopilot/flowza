# ADR-0001: Audit service with a pluggable store (SQL Server or MongoDB)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-16 |
| **Decided by** | Owner |
| **Required by** | Constitution 3.6 (a new service needs an ADR) |

## What an ADR is
An **Architecture Decision Record** captures one important decision: the situation, the choice, the
options we rejected, and the consequences. It is written once and not edited later; if the decision
changes, a new ADR replaces it.

## Context
- The platform needs a **platform-wide audit log**: who did what, and when, across all services
  (logins, user and role changes, design publishes, admin restarts, settings changes).
- Audit data is **append-only**, **high volume** (tens of millions of entries a year at a large customer),
  has a **different shape per event type**, and must be kept for a **long time** (8–10 years for banks).
- SQL Server is enough for small installations; large, high-volume installations may prefer a document database.
- The platform must run on Azure **and** on-premises (constitution Article 6).

This is **not** the request history (WF-11). Request history stays in the Runtime service on SQL Server,
because it must be saved in the same transaction as the request's state.

## Decision
1. Create a separate **Audit service** that owns its own audit database.
2. The Audit service stores entries through an interface, **`IAuditStore`**, with **two implementations**:
   - **SQL Server** (default)
   - **MongoDB**
3. **Dependency injection** chooses the implementation at startup, from configuration
   (`Audit:Store` = `SqlServer` or `MongoDb`). Exactly one store is active per installation.
   The rest of the service depends only on `IAuditStore`, never on SQL Server or MongoDB directly.
4. Other services **publish integration events** (through the outbox). The Audit service **subscribes**
   and appends an entry. The event ID is the entry's unique key, so handling an event twice stores it
   only once (idempotent, constitution 3.4).
5. The store is **append-only**: there is no update operation. Entries are removed only by the
   retention job after the installation's retention period (constitution 8.6).
6. Audit entries hold **IDs and change descriptions, never form values or other personal data**
   (constitution 11.2).
7. **One shared test suite** runs against **both** implementations. Both must pass the same tests,
   so either store can replace the other (Liskov substitution).

### What an audit entry contains
Entry ID (the event ID), time (UTC), actor (user ID and display name at that time), action
(e.g. `ProcessPublished`), entity type and ID, source service, correlation ID, and a details document
(a different shape per action).

### What `IAuditStore` offers
- **Append** one or many entries (duplicates are ignored)
- **Query** by time range, actor, entity, action and source service, with paging
- **Purge** entries older than the retention period

### How each implementation stores data
| | SQL Server | MongoDB |
|---|---|---|
| Storage | One append-only table; details in a JSON (`nvarchar(max)`) column; partitioned by month | One collection; each entry is a document |
| Unique key | Unique index on the entry ID | `_id` = entry ID |
| Main indexes | (entity type, entity ID, time), (actor, time), (time) | The same, as compound indexes |
| .NET access | EF Core or Dapper | Official MongoDB C# driver (Apache 2.0 licence) |

## Options considered
| Option | Why not chosen |
|---|---|
| SQL Server only | Works, but high-volume installations may be better served by a document database |
| MongoDB only | Forces every on-prem customer to install and run MongoDB, even small ones |
| Azure Cosmos DB | Azure only; breaks the on-prem rule |
| OpenSearch / Elasticsearch | Excellent for search, but heavy to run on-prem. It can be added later as a third `IAuditStore` implementation or as a search service |

## Consequences
**Good**
- Each customer chooses the store that fits their size and IT policy.
- Clean SOLID example: the service depends on an abstraction (Dependency Inversion); new stores can be
  added without changing callers (Open/Closed); both stores pass the same tests (Liskov).
- Adding OpenSearch later is just a third implementation.

**Costs**
- Two implementations to build, test and maintain.
- Customers who choose MongoDB must install, back up and secure it. The **MongoDB server** is
  optional infrastructure that the customer runs; it is not a library inside our code. The MongoDB
  server licence (SSPL) should be checked by customers who are strict about licences.
- Moving an existing installation from one store to the other needs an export/import tool (later).

## Timing
The Audit service is **not in v0.1**. It is planned with the "full audit trail" in **v0.4** (see the
roadmap in the v0.1 scope). v0.1 prepares for it: all important actions already publish integration
events with an event ID, actor and correlation ID.
