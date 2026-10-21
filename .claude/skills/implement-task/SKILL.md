---
name: implement-task
description: Implement one Flowza task from specs/<feature>/tasks.md the spec-driven way - read the task, its spec IDs and the plan, write code and ID-named tests that follow SOLID/patterns/DSA rules, build with 0 warnings, run tests, make one local commit and update the task's progress and traceability. Use for every build task (T0xx) in any feature.
---

# Implement one Flowza task

Use this for **one** task at a time (e.g. `T012`). Never start the next task before this one is committed.

## 1. Understand the task (no code yet)
1. Open `specs/<feature>/tasks.md` and find the task row: its **spec IDs** and its **"Done when"**.
2. Read those IDs in `specs/<feature>/spec.md` (requirement + acceptance criteria) and the matching sections of
   `specs/<feature>/plan.md` (design, data, API, events, approved libraries).
3. Re-read the rules that apply in `CLAUDE.md`: conventions, "Engineering principles" (SOLID, patterns, DSA),
   privacy, no hard delete, translation files, accessibility.
4. **Stop and ask the owner** if the task needs anything the spec or plan does not cover: a new library, an API or
   event contract change, a database design change (constitution 1.6), or an unclear requirement (1.5).
   Never guess silently.

## 2. Write the code and the tests
- Put code in the layer the plan names (Domain → Application → Infrastructure → Api; vertical slices in Application).
- Follow SOLID; use a design pattern only where the plan names it or it clearly solves a real problem; choose data
  structures and algorithms on purpose.
- Expected failures return `Result`/`Error`; exceptions only for the unexpected. Log IDs only, never personal data.
- Every acceptance criterion the task covers gets at least one test **named with its ID**, e.g.
  `IAM_07_AC1_five_wrong_passwords_lock_the_account`. Algorithms get edge-case tests (empty, single, cycles, large).
- Integration tests use the real `flowza_test_*` databases through `tests/Flowza.Testing` (never the dev databases).
- Write tests that would **fail without the change** (if unsure, break the code briefly and watch the test fail).

## 3. Verify
```powershell
dotnet build Flowza.slnx -v q          # must say 0 Warning(s), 0 Error(s)
dotnet test --project <affected test project>
```
- For web tasks (`src/Web`): `npm run lint`, `npm run typecheck`, `npm test` must pass.
- Fix every warning and failure; do not skip or weaken tests to make them pass.

## 4. Commit (local only)
- One commit per task, Conventional Commits with the scope and IDs, ending with the co-author line:
  ```
  feat(identity): lockout after 5 wrong passwords (IAM-07, T012)

  <what changed and why, in 2-5 lines>

  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  ```
- **Never push.** The owner decides when to push.

## 5. Record progress
- In `specs/<feature>/tasks.md`: mark the task ✅ in the **Progress** table with the commit hash, and set the
  **Traceability** status of each acceptance criterion it completed (✅, or 🟡 with what is still missing).
- Note any decision you took inside the approved plan under the Progress notes. Commit this with the task or as a
  small `docs(<feature>):` commit.

## 6. Report
Tell the owner in plain words: what the task delivers, which tests prove it (IDs), anything not done or not verified,
and any question that needs their decision.
