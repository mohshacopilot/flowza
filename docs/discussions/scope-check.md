# Scope check: size of v0.1 and milestone split

| | |
|---|---|
| **Status** | **Accepted by the owner, 2026-10-21.** SC1: as proposed. SC2: build starts today (2026-10-21) on the office PC. SC3: the owner reviews on weekdays, full days |
| **Applies to** | [specs/v0.1-scope.md](../../specs/v0.1-scope.md) (still "Draft") |

## Why this check
The first v0.1 scope was planned for 4 build days (17–20 Oct). The module discussions then added real
business needs: grid, tabs, explicit data types, "Required when", step rules, `YEARS_BETWEEN`, "All must
approve", maker-checker, more automatic emails, password policy, lockout, session lifetime, a Settings
screen, branding and CI. All of these are right for the product, but the total is far more than 4 days of
clean, tested work. Quality (tests, constitution) is never cut, so the **scope is split into milestones**.

## Estimate (focused build days: Claude builds, the owner reviews and runs)
| Area | Days |
|---|---|
| Foundation: solution, Aspire, Gateway (BFF), service defaults, Wolverine outbox, CI, README skeleton | 1.5–2 |
| IAM core (login, roles, first admin, users, lockout) | 1 |
| ORG (tree, users' org details, business roles, approver rules) | 1–1.5 |
| EXP (C# + TypeScript + conformance suite) | 1.5 |
| APP basic (apps, versions, publish, active version) | 1 |
| FRM designer + renderer + data types + validation (no grid, no tabs) | 2–3 |
| WF designer + publish checks | 1–1.5 |
| WF engine (tokens, actions, parallel, reopen, maker-checker, history) | 2 |
| INB screens | 1 |
| NTF (templates, Worker, retries, delivery log) | 1 |
| Demo data, end-to-end tests RS-1 and RS-2, README | 1–1.5 |
| **Milestone 1 total** | **about 13–16 days** |
| Milestone 2 (section below) | about 4–5 days |
| Milestone 3 (section below) | about 3–4 days |
| **All decided v0.1 items** | **about 20–25 days** |

These are honest ranges, not promises. The biggest unknowns are the form designer and setting up the
environment on the build machine.

## Proposed milestones
Each milestone ends with a **working, tested, demoable** product.

### Milestone 1 = v0.1 "Core engine": both reference scenarios work end to end
- **PLT:** PLT-01 to PLT-07, PLT-09 (CI)
- **IAM:**
  - IAM-01 to IAM-05
  - IAM-07 (lockout)
  - IAM-06 with fixed safe defaults: minimum length 10, no common passwords, not the username, users change their own password, temporary password on reset
- **ORG:** ORG-01 to ORG-06
- **EXP:** EXP-01 to EXP-07, without `SUM`/`COUNT` (they come with the grid)
- **APP:** APP-01 to APP-04, APP-06
- **FRM:**
  - FRM-02 to FRM-11, FRM-14
  - FRM-12 without tabs
  - data types (DT1–DT2)
  - "Required when" (CV1)
  - validation by action (CV3)
  - message placement (CV7)
- **WF:**
  - WF-01 to WF-09, WF-11 to WF-13
  - WF-15 (maker-checker)
  - send back inside a parallel section (W3)
  - "Any one" approval mode
- **INB:** INB-01 to INB-05, plus the Unassigned queue and reassign (Z2)
- **NTF:** NTF-01, NTF-02, NTF-04, NTF-05, **NTF-07** (SMTP settings table, screen and test email; moved here from M3 by the owner on 2026-10-21, SM4)
- **Proof:** RS-1 and RS-2 built through the UI and covered by end-to-end tests

### Milestone 2 = v0.1.1 "Real-world forms and approvals"
- **FRM-13 Grid**, with `SUM`/`COUNT`, view-only grids ("Datalist view")
- **Tabs** (rest of FRM-12)
- **Step rules** and "required at this step" (CV2)
- **WF-14 "All must approve"**
- INB-07 home counters
- NTF-06 automatic emails for sent back (the restart email comes in M3 with APP-05, spec 006 C15)
- **Proof:** an extended RS-2 (co-applicants grid, sanction committee with "All must approve")

### Milestone 3 = v0.1.2 "Administration and operations"
- APP-05 restart on a new version (single and bulk), APP-07 disable an app
- **PLT-08 Settings screen** and branding
- IAM-06 full password policy settings (expiry, reuse), IAM-08 session settings
- NTF-06 restart email (with APP-05, spec 006 C15)
- ~~NTF-07 SMTP test email~~ moved to Milestone 1 (owner, 2026-10-21)

## How this works with Spec Kit
- The feature order stays the same (001 to 006, see CLAUDE.md).
- Each `spec.md` holds **all** decided requirements of its modules, with a **Milestone** column (M1/M2/M3).
- `tasks.md` is ordered so that all M1 tasks come first; M2 and M3 tasks follow.
- Nothing decided is lost; it is only scheduled.

## Questions for the owner
| # | Question | Options |
|---|---|---|
| SC1 | Milestone split | **a) As proposed above ⭐** · b) Move items between milestones (name them) |
| SC2 | Build dates and machine | Free answer: start date, end date, which PC |
| SC3 | Your daily review time | Free answer: hours per day you can review and run the build (this sets the calendar) |
