---
name: write-plan
description: Write specs/<feature>/plan.md (the "how") for an approved Flowza feature spec, in the same format as specs/001-platform-foundation/plan.md - technical context, constitution check, structure, domain/data/API/events design, named design patterns, data structures and algorithms with time costs, testing plan per spec ID, risks, estimate and the list of items the owner must approve. Use after a feature spec is approved and before tasks.md.
---

# Write a feature plan

A plan is written **only for an approved spec** (status "Approved" in `specs/<feature>/spec.md`). The plan says
**how**; it never changes **what** the spec asks for. If the spec looks wrong or incomplete, stop and ask the owner.

## Inputs to read
1. `specs/<feature>/spec.md` (requirements, acceptance criteria, milestones, clarifications).
2. The feature's module discussion files in `docs/discussions/`: their **"Design notes"** sections are the starting
   point for the plan.
3. `CLAUDE.md` (decisions table, services, conventions, "Engineering principles"), `docs/constitution.md`,
   `docs/discussions/tech-discussion.md` (T1–T13), the ADRs in `docs/decisions/`.
4. The pattern to copy: `specs/001-platform-foundation/plan.md` and how its tasks turned out in `tasks.md`.

## Sections (same order as plan 001)
1. **Approach in one paragraph.**
2. **Technical context**: stack pieces this feature uses; constraints (SQL Server 2019, no containers, on-premises).
3. **Constitution check**: one line per article saying how the plan complies.
4. **Structure**: projects, folders and vertical slices that change or are added.
5. **Design per service**: domain model (aggregates, value objects, rules), **data** (tables, keys, indexes, JSON
   columns, soft delete, row version), **API** (method, path, purpose, who may call it), key flows.
6. **Events** (`Flowza.Contracts.<Service>.Events.V1`): name, extra data (IDs only), who subscribes; add the routes to
   `MessageRoutes`.
7. **Design patterns**: each one named with the problem it solves here (constitution 4.4). No pattern "just in case".
8. **Data structures and algorithms**: each choice with its **time cost** (constitution 4.5), e.g. "topological sort
   (Kahn), O(V+E)".
9. **Web app** parts (screens, routes, components, translations, accessibility), if any.
10. **Testing plan**: table of spec IDs → test type (unit / integration on `flowza_test_*` / end-to-end) → how.
11. **Items needing the owner's approval (constitution 1.6)**: numbered **PA1, PA2 …** — every API contract, event
    contract, database design and **new library** (with licence; only permissive licences, constitution 9.3).
12. **Risks** and mitigations.
13. **Estimate** in build days (from `docs/discussions/scope-check.md` where available).

## Rules
- No technology choice that contradicts `CLAUDE.md` or an ADR; a real need to change one becomes a question for the
  owner (and possibly a new ADR), not a silent change.
- Keep business-domain words out of platform design (constitution 2.1); they appear only in examples and demo data.
- Status of the new plan: **"Draft for the owner's review"**. After approval, write `tasks.md` (ordered, linked to
  spec IDs, M1 tasks first, with a traceability table and an analysis of spec ↔ plan ↔ tasks gaps).
- Commit locally: `docs(<feature>): draft plan ...` with the co-author line. Never push.
