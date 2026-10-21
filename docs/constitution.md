# Project Constitution

| | |
|---|---|
| **Version** | 1.0 |
| **Status** | Approved by the owner after review (C1–C10) |
| **Date** | 2026-10-16 |

## What this document is
The constitution holds the **non-negotiable rules** for this project. Every spec, plan, task and line of
code must follow them. If a rule blocks something we really need, we **change the constitution first**
(see "Changing this document"); we never quietly break it.

Each rule has a **Why**, so that we understand it and don't just obey it.

---

## Article 1: Spec first
1.1 No change in **behaviour** is made without an approved spec for it. Bug fixes and clean-ups (refactoring) that do not change behaviour need no spec: a bug fix needs a test that reproduces the bug, and a clean-up must keep all tests passing.
1.2 When a requirement changes, the spec changes **before** the code.
1.3 Every requirement and acceptance criterion has a stable ID (e.g. `WF-08`). Tasks, commits and tests refer to these IDs.
1.4 Every acceptance criterion has at least one automated test that names its ID. If a criterion really cannot be automated (for example "the designer feels smooth"), it gets a **manual test checklist** in the feature folder, which the owner signs off.
1.5 If something is unclear, ask and write the answer into the spec. Never guess silently.
1.6 Claude (the developer) must **not** add a third-party library, change an API or event contract, or change the database design without the owner's approval, written in the plan.

**Why:** the spec is the shared memory between the owner and Claude, and across machines and sessions. Code without a spec cannot be checked for correctness.

## Article 2: Generic core
2.1 The platform core contains **no business-domain concepts** (leave, loan, patient, invoice, …). Those exist only as configuration: forms, processes, org data.
2.2 Something goes into the core only if **most organizations** need it. Anything specific to one domain or system (for example a credit bureau check) is a plugin or connector.
2.3 Every new core feature must be shown to work in at least **two different business domains** (see the reference scenarios in the scope).

**Why:** the product's value is that any organization can use it. A domain shortcut in the core helps one customer and hurts all the others.

## Article 3: Service boundaries
3.1 Each service owns its data in **its own database**. No service reads or writes another service's database.
3.2 Services communicate by **events** (preferred) or by **HTTP APIs** (for queries that need an immediate answer).
3.3 Events are published through a **transactional outbox**, so an event is never lost and never sent for a change that was rolled back.
3.4 Event handlers are **idempotent**: handling the same event twice has the same effect as handling it once.
3.5 Contracts (APIs and events) are **versioned**. A released contract is never changed in a breaking way; a new version is added instead.
3.6 A new service is created only with a written decision record (ADR) that explains why.

**Why:** microservices only pay off if services can change and be deployed independently. Shared databases and breaking contracts destroy that.

## Article 4: Clean design inside each service
4.1 Layers: **Domain → Application → Infrastructure → Api**. Dependencies point inward only.
4.2 The Domain layer has no references to EF Core, ASP.NET or other frameworks.
4.3 Follow SOLID. Prefer small classes with one job, interfaces at boundaries, and composition over inheritance.
4.4 Use a design pattern only when it solves a real problem here, and name the pattern in the plan.
4.5 Choose data structures and algorithms on purpose (for example a tree for the org hierarchy, a graph for processes, a topological sort for calculated fields) and state the time cost when it matters.

**Why:** a platform lives for years. Clean boundaries let us replace parts (database, message broker, UI) without rewriting the rest.

## Article 5: Testing
5.1 Domain logic and algorithms have unit tests.
5.2 Each service has integration tests against a **real SQL Server**, not an in-memory fake.
5.3 Each reference scenario has an end-to-end test.
5.4 The expression language has one shared set of test cases that **both** the C# and TypeScript implementations must pass.
5.5 A bug fix starts with a test that reproduces the bug.
5.6 Domain and Application code has at least **80% line coverage** (per service and per building block). There is no coverage target for UI code; its behaviour is covered by component and end-to-end tests.

**Why:** tests are how we prove the code matches the spec, which is the "verify" step of spec-driven development.

## Article 6: Portability (Azure and on-premises)
6.1 Everything must run both on Azure and on a customer's own servers.
6.2 No cloud-specific service is used directly; it sits behind an interface with an on-prem implementation (storage, messaging, email, secrets).
6.3 The database must stay compatible with **SQL Server 2019**.
6.4 Local development must work on Windows **without Docker**.

**Why:** many customers (banks, hospitals, NBFCs) require on-premises installs. Losing portability later is very expensive.

## Article 7: Security by default
7.1 Every API endpoint requires authentication, except login and health checks.
7.2 Authorization is checked on the **server** for every action (for example, only a task's assignee can act on it).
7.3 All input is validated on the server, even if the browser already validated it.
7.4 SQL is always parameterized. No SQL text is built from user input.
7.5 The expression language cannot run code or access files, network or database.
7.6 No secrets in git. Configuration comes from environment variables or a secret store.
7.7 Passwords are stored only as hashes (ASP.NET Core Identity).

**Why:** customers will run financial and health processes on this platform. One security hole damages every customer.

## Article 8: Data and versioning
8.1 A **published** form or process version never changes. Editing creates a new version.
8.2 A running request always stays on the definition versions it started with.
8.3 Every change to a request is recorded in its history (who, what, when).
8.4 Times are stored in UTC; date-only values are stored as dates.
8.5 Concurrent updates are detected (optimistic concurrency); the second writer gets a clear message, never silent data loss.
8.6 Business records (requests, form data, history, and designs that requests use) are **never hard-deleted**. "Delete" only marks a record as deleted (soft delete). Each installation sets a **retention period**; permanent removal happens only after that period, through a controlled retention job (a later feature).
8.7 Every saved design (app, form, process) carries a **format version**. When the platform is upgraded, older designs are upgraded automatically, so apps built on an older platform version keep working.

**Why:** approvals are business records. They must be traceable and must not change because a designer edited a form later. Banks and NBFCs must often keep records for 8–10 years, and customers must be able to upgrade the platform without rebuilding their apps.

## Article 9: Simplicity
9.1 Build only what the current scope asks for (YAGNI).
9.2 Prefer well-known, stable technology over new and clever technology.
9.3 Use only free, permissively licensed dependencies. No commercially licensed packages (for example MediatR, AutoMapper, MassTransit v9+, NServiceBus).
9.4 Every new third-party dependency is named and justified in a plan.

**Why:** one developer maintains this for now. Every extra moving part is a cost.

## Article 10: Observability
10.1 Logs are structured and carry a **correlation ID** that follows a request across services.
10.2 Every service exposes health checks.
10.3 Errors returned to users are clear; internal details go only to logs.

**Why:** in a distributed system you cannot fix what you cannot trace.

## Article 11: Privacy and personal data
11.1 Collect only the personal data a process needs, for a clear purpose.
11.2 **No personal data in logs, error messages or telemetry.** Logs may contain IDs (request number, user ID) but never form values.
11.3 Designers will be able to mark a field as **sensitive** (for example PAN, Aadhaar, diagnosis), so it is masked in lists, logs and notifications. The feature comes in a later version; rule 11.2 applies from day one.
11.4 Only people allowed by the view rules (NFR-01 in the scope) can see a request's data.
11.5 The platform must let customers comply with India's Digital Personal Data Protection Act 2023 (DPDP) and similar laws.

**Why:** customers will store identity, financial and health details. A leak harms real people and the customer's business.

## Article 12: Usable by everyone
12.1 **All screen text lives in translation files from day one**; no text is hard-coded in components. v0.1 ships in English only. Dates, numbers and currency are formatted by the user's locale.
12.2 Screens aim for **WCAG 2.1 AA** accessibility. From v0.1: everything works with the keyboard, and every form field has a proper label. A full accessibility audit comes later.

**Why:** customers work in many Indian languages, and banks and government bodies often require accessibility. Adding both later is far more expensive than designing for them now.

---

## Changing this document
1. Write down the proposed change and the reason.
2. The owner approves it.
3. Increase the version number and add a line to the change log below.
4. Update any spec or code that the change affects.

## Change log
| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-10-16 | First version, approved after the owner's review (C1–C10): spec-free bug fixes and clean-ups (1.1), manual test checklists (1.4), Claude approval rule (1.6), 80% coverage (5.6), no hard delete and retention (8.6), design format versions (8.7), new Article 11 (privacy) and Article 12 (languages and accessibility) |
