# AGENTS.md

Instructions for AI coding agents (GitHub Copilot, Codex, Cursor and others) working on **Flowza**.

This file is only a pointer, so there is **one source of truth**. Before doing anything, read:

1. [CLAUDE.md](CLAUDE.md): project rules, decisions, conventions and the way of working (it applies to every
   agent, not only Claude).
2. [docs/constitution.md](docs/constitution.md): the non-negotiable rules.
3. [docs/discussions/discussion.md](docs/discussions/discussion.md): where the work stands and what is next.
4. The feature you work on: `specs/<feature>/spec.md` (what), `plan.md` (how), `tasks.md` (steps).

Key rules, in short (details in CLAUDE.md):
- **No code without an approved spec**; tests carry spec IDs in their names (e.g. `IAM_07_AC1_...`).
- No new library, API/event contract or database change without the owner's approval (constitution 1.6).
- No personal data or form values in logs; no secrets in git.
- Build with 0 warnings; all tests green; commit locally with Conventional Commits and spec IDs;
  **never push without the owner's OK**.
