# Feature 001: Platform foundation (IAM + PLT)

| | |
|---|---|
| **Status** | **Approved by the owner, 2026-10-21** (clarifications C1–C6 answered) |
| **Date** | 2026-10-21 |
| **Modules** | IAM (Identity and access), PLT (Platform) |
| **Scope IDs** | IAM-01 to IAM-08, PLT-01 to PLT-09 ([v0.1 scope](../v0.1-scope.md)) |
| **Sources** | [IAM discussion](../../docs/discussions/identity-and-access.md), [PLT discussion](../../docs/discussions/platform.md), [constitution](../../docs/constitution.md) |
| **Next documents** | `plan.md` (how), `tasks.md` (steps); both only after this spec is approved |

> **This spec says what and why, never how.** Technology choices (frameworks, libraries, protocols) belong in
> `plan.md`. The only technical words here are **constraints the customer gives us** (e.g. "runs on SQL Server
> 2019", "runs on Windows without container software"), because they are requirements, not design choices.

## 1. Summary
Before anyone can build forms or processes, the platform needs a safe front door and a reliable base:
people log in, administrators manage users and roles, passwords and sessions are protected, and the
product starts, stores data, passes messages, reports its health and installs easily, on a customer's own
servers or in the cloud.

**Why it matters:** customers will run financial and health processes on this platform. A weak login, lost
messages or a hard installation would stop a sale or harm real people, whatever the other features do.

## 2. Users
| Persona | What they need from this feature |
|---|---|
| **Every user** | Log in and out safely; change their own password |
| **Platform Admin** | Create and manage users and their system roles; unlock accounts; (M3) change installation settings and branding |
| **Installer / developer** | Start the whole product with one command; set up a new machine from a guide; load demo data |
| **Customer IT / DBA** | Review database changes before they run; know which databases to back up; read logs and health status |

## 3. User stories and acceptance scenarios
Priority **P1** = needed for Milestone 1; **P3** = Milestone 3.

### US1: Log in and log out (P1, IAM-01, IAM-05)
As a user, I log in with my username and password and log out when I finish, so that only I can act as me.
1. **Given** an active user with a correct password, **when** they log in, **then** they reach the home page.
2. **Given** a wrong username or password, **when** they log in, **then** they see "Username or password is incorrect", which does not reveal which one was wrong.
3. **Given** a logged-out user, **when** they open any page or call any API other than login and health, **then** they are refused.

### US2: First administrator (P1, IAM-03)
As the person installing the product, I get one administrator account so I can set everything else up.
1. **Given** a new installation, **then** exactly one Platform Admin account exists, with a strong random password generated at setup and shown once (C2).
2. **When** that admin logs in for the first time, **then** they must set a new password before doing anything else.

### US3: Administrators manage users and system roles (P1, IAM-02, IAM-04)
As a Platform Admin, I create users and give them roles, so that people can use the platform without signing themselves up.
1. **When** the admin creates a user with username, display name, email and (optionally) mobile, **then** the user can log in after setting their password.
2. **Given** a user, **then** the admin can give them several system roles (User, App Designer, Platform Admin); every active user has User.
3. **When** the admin resets a password, **then** a temporary password is shown **once**, and the user must change it at their next login.
4. **When** the admin deactivates a user who is logged in, **then** that user's next action is refused.
5. **Given** only one active Platform Admin, **when** someone tries to deactivate them or remove their admin role, **then** it is refused.
6. **Given** a user without the App Designer role, **when** they open a designer screen or call its API, **then** they are refused; the same for directory and user screens without Platform Admin.

### US4: Safe passwords (P1 with fixed defaults; P3 configurable; IAM-06)
As a user, I can only choose a strong password and can change it myself, so that accounts are hard to break into.
1. **When** a password is shorter than 10 characters, is a commonly used password, or equals the username, **then** it is rejected with a clear reason.
2. **When** a logged-in user changes their own password (entering the current one), **then** the new password works and the old one does not.
3. *(M3)* **Given** the admin set expiry to 90 days, **when** a user's password is older, **then** they must change it at login.
4. *(M3)* **Given** the admin set reuse to 5, **when** a user chooses one of their last 5 passwords, **then** it is rejected.

### US5: Protection against guessing (P1, IAM-07)
As a Platform Admin, I want repeated wrong passwords to be blocked, so that attackers cannot guess passwords.
1. **When** a wrong password is entered 5 times in a row, **then** the account is locked for 15 minutes and the user sees that it is locked.
2. **Given** a locked account, **then** the admin can unlock it immediately.
3. **When** very many login attempts come from one network address, **then** further attempts are slowed down or refused for a while.

### US6: Sessions end on their own (P1 with fixed defaults, P3 configurable; IAM-08, C1)
As a Platform Admin, I want unattended sessions to end, so that a forgotten open browser is not a risk.
1. **When** a user is inactive for 30 minutes, **then** they must log in again.
2. **When** 12 hours have passed since login, **then** they must log in again even if active.
3. *(M3)* **Then** the admin can change both values on the Settings screen.

### US7: One command starts everything (P1, PLT-01, PLT-02)
As a developer or installer, I start the whole product with one command on Windows without container software, so that setup is simple and repeatable.
1. **Given** a prepared Windows machine, **when** the start command runs, **then** all services, the web application and a development mail catcher start, and the application opens at one address.

### US8: Each service keeps its own data and upgrades it safely (P1, PLT-03)
As customer IT, I want each part of the product to own its data and to review database changes, so that upgrades are controlled.
1. **When** the product starts for the first time in development, **then** each service creates its own database, and restarting changes nothing.
2. **When** preparing a production upgrade, **then** a database change script can be produced for each service, reviewed by the DBA, and safely run twice.

### US9: Messages between services are never lost (P1, PLT-04)
As the business, I need every event to reach the services that depend on it, so that no request gets stuck or out of step.
1. **Given** a change that was cancelled (rolled back), **then** its event is never delivered.
2. **Given** a change that was saved, **when** the receiving service was down at that moment, **then** the event is delivered once it is back.
3. **When** the same event arrives twice, **then** the result is the same as arriving once.

### US10: Health and traceable logs (P1, PLT-05)
As customer IT and support, I can see whether each service is healthy and follow one user action through all services, without exposing personal data.
1. **Then** each service reports "running" and "ready" (its database and messaging reachable) separately.
2. **When** one user action passes through several services, **then** all their log entries share one reference, and an error screen shows the user a short reference code that support can find.
3. **Then** logs never contain form values or personal data, only IDs.

### US11: Setup guide (P1, PLT-06)
As a new developer or installer, I can set up a fresh machine by following one guide in under 30 minutes.
1. **When** someone follows the guide on a fresh machine, **then** the product runs in under 30 minutes (manual checklist, constitution 1.4).

### US12: Demo data (P1, PLT-07)
As a presenter, I load a ready-made demo organization, users and both reference apps, so that a demo starts in minutes.
1. **Given** empty databases, **when** the demo tool runs, **then** the demo org tree, users and the two reference apps (HR leave, loan approval) are available.
2. **Given** any database that already has data, **when** the demo tool runs, **then** it refuses and changes nothing.

### US13: Every change is built and tested automatically (P1, PLT-09)
As the owner, I want every change built and tested automatically, so that quality never depends on memory.
1. **When** a change is pushed, **then** the build, all automated tests and the coverage check (80%, constitution 5.6) run, and a failure is clearly reported.

### US14: Settings screen and branding (P3, PLT-08)
As a Platform Admin, I set installation-wide business settings on one screen, so that the product fits my organization without editing files.
1. **Then** the admin can change installation name and logo, time zone, currency, locale, password policy and session timeouts.
2. **When** a setting changes, **then** all services use the new value without a restart.
3. **Then** the name and logo appear in the application header and in emails.

## 4. Requirements
| ID | Requirement | Milestone |
|---|---|---|
| IAM-01 | Log in with username and password; log out. Wrong credentials give one general message | M1 |
| IAM-02 | System roles Platform Admin, App Designer, User; a user may hold several; every active user has User; the server checks roles on every action | M1 |
| IAM-03 | One Platform Admin exists after installation, with a random password generated at setup and shown once; it must be changed at first login (C2) | M1 |
| IAM-04 | Admin creates, edits, activates, deactivates and re-activates users (C6); no self sign-up; usernames are 3–50 characters (letters, digits, `.` `_` `-`), unique ignoring case, and never change (C4); email is required and unique ignoring case (C3); reset gives a one-time temporary password; deactivation ends the user's session immediately; the last active Platform Admin is protected | M1 |
| IAM-05 | Every API needs a logged-in user, except login and health checks | M1 |
| IAM-06 | Password rules: minimum 10, no common passwords, not the username; users change their own password. **M3:** minimum length, expiry and reuse become installation settings | M1 / M3 |
| IAM-07 | 5 wrong passwords lock the account for 15 minutes; admin can unlock; login attempts are rate-limited per network address | M1 |
| IAM-08 | Sessions end after 30 minutes of inactivity and 12 hours at most (fixed in M1, C1); **M3:** both become installation settings | M1 / M3 |
| IAM-L5 | Logins, failures, lockouts and password changes publish events with IDs only (for the future audit log) | M1 |
| PLT-01 | The product consists of five services and a web application, each deployable on its own (constitution Article 3) | M1 |
| PLT-02 | One command starts everything on Windows without container software | M1 |
| PLT-03 | Each service owns its own database; automatic database updates in development; reviewable, re-runnable scripts for production | M1 |
| PLT-04 | Events between services are never lost and never sent for cancelled changes; handling an event twice is safe | M1 |
| PLT-05 | Health ("running" / "ready") per service; one reference across services for each user action; error screens show a reference code; no personal data in logs | M1 |
| PLT-06 | A setup guide gets a fresh machine running in under 30 minutes; it lists the databases to back up | M1 |
| PLT-07 | A demo tool loads demo org, users and both reference apps; it refuses to run on databases that have data. Demo users share one documented demo password without forced change (C5) | M1 |
| PLT-08 | Settings screen: installation name and logo, time zone, currency, locale, password policy, session timeouts; changes apply without restart. Technical settings stay in configuration | M3 |
| PLT-09 | Every pushed change is built and tested automatically, including the 80% coverage check | M1 |

## 5. Acceptance criteria
Every criterion gets at least one automated test whose name contains its ID (constitution 1.4), unless marked *manual*.

| ID | Criterion |
|---|---|
| IAM-01-AC1 | A user logs in and out; after logout, protected pages and APIs are refused |
| IAM-01-AC2 | A wrong username or password gives one general message that does not say which was wrong |
| IAM-02-AC1 | Designer screens and APIs need App Designer; directory and user screens and APIs need Platform Admin; the server refuses otherwise |
| IAM-02-AC2 | A user can hold several system roles; the last active Platform Admin cannot be deactivated or lose the role |
| IAM-03-AC1 | A new installation has one admin account with a generated password shown once; it must be changed at first login (C2) |
| IAM-04-AC1 | The admin creates, edits, activates and deactivates users; a username cannot be changed |
| IAM-04-AC2 | A reset shows a temporary password once; the user must change it at the next login |
| IAM-04-AC3 | Deactivating a logged-in user ends their session; their next action is refused |
| IAM-04-AC4 | A username outside the format, or a username or email already used (ignoring letter case), is rejected (C3, C4) |
| IAM-04-AC5 | A deactivated user can be activated again and logs in; their history is unchanged (C6) |
| IAM-05-AC1 | Every API except login and health checks rejects calls without a valid login |
| IAM-06-AC1 | Passwords shorter than 10, common passwords and passwords equal to the username are rejected |
| IAM-06-AC2 | *(M3)* With expiry 90 days, an older password must be changed at login; with reuse 5, the last 5 passwords are refused |
| IAM-06-AC3 | A user changes their own password after entering the current one |
| IAM-07-AC1 | After 5 wrong passwords the account is locked for 15 minutes; the admin can unlock it |
| IAM-07-AC2 | Many attempts from one network address are slowed down or refused |
| IAM-08-AC1 | After 30 minutes inactive, or 12 hours in total, the user must log in again |
| IAM-L5-AC1 | Login, failed login, lockout and password change each publish an event containing IDs only |
| PLT-02-AC1 | On Windows without container software, one command starts all services, the web app and the mail catcher; the app opens at one address |
| PLT-03-AC1 | First start in development creates each service's database; a restart changes nothing |
| PLT-03-AC2 | A production change script can be produced per service and run twice safely |
| PLT-04-AC1 | An event from a cancelled change is never delivered; an event from a saved change is delivered even if the receiver was down |
| PLT-04-AC2 | Delivering the same event twice has the same effect as once |
| PLT-05-AC1 | "Ready" reports unhealthy when the service's database is unreachable |
| PLT-05-AC2 | One user action across several services shows one reference in all their logs; error screens show a reference code |
| PLT-05-AC3 | An automated test scans log output for test personal values and finds none |
| PLT-06-AC1 | *(manual checklist)* A fresh machine runs the product in under 30 minutes by following the guide |
| PLT-07-AC1 | The demo tool loads demo org, users and both reference apps into empty databases |
| PLT-07-AC2 | The demo tool refuses to run when any service database already has data |
| PLT-08-AC1 | *(M3)* The admin changes the business settings; all services use the new values without a restart |
| PLT-08-AC2 | *(M3)* The installation name and logo appear in the header and in emails |
| PLT-09-AC1 | A pushed change triggers build, tests and the coverage check; a failure is reported and blocks the change |

## 6. Edge cases
- A user is deactivated while filling a long form: their save is refused with a clear message; nothing half-saved remains.
- Two admins edit the same user at the same moment: the second gets a clear "changed by someone else" message (constitution 8.5).
- The admin locks themselves out (wrong password 5 times): another admin unlocks them; if they are the only admin, the lock expires after 15 minutes.
- A user's role is removed while they are logged in: their next action that needs the role is refused.
- A service starts while its database is unreachable: it reports "not ready" and recovers by itself when the database returns.
- The demo tool is pointed at a production installation by mistake: it refuses (US12).
- An email address is entered in a different letter case for two users: treated as the same address and rejected (C3).

## 7. Non-functional requirements that apply
From the [v0.1 scope](../v0.1-scope.md) section 6: NFR-01 (security), NFR-03 (reliability), NFR-04 (SQL Server 2019 or later, no cloud-only services, Windows without containers), NFR-05 (UTC storage, local display), NFR-07 (privacy), NFR-10 (translation files), NFR-11 (keyboard and labels), NFR-12 (coverage).

## 8. Key entities (business view, not database design)
| Entity | Meaning |
|---|---|
| **User** | A person who can log in: username (never changes), display name, email, mobile (optional), status (active/inactive/locked), system roles |
| **System role** | Platform Admin, App Designer or User: what parts of the platform a user may use |
| **Session** | A period during which a user is logged in; ends on logout, inactivity, maximum time or deactivation |
| **Password policy** | The rules a new password must follow (fixed in M1, settings in M3) |
| **Installation settings** | Business-level settings of one installation (M3): name, logo, time zone, currency, locale, password policy, session timeouts |
| **Platform event** | A record that something important happened (e.g. "user locked"), carrying an event ID, actor, UTC time and reference, never personal data |

## 9. Out of scope
| Item | When |
|---|---|
| Forgot-password self-service by email | Later (K5) |
| Two-factor login | v1.0 (K8) |
| AD/LDAP, Entra ID, single sign-on | v1.0 |
| Per-user time zone | Later (L7) |
| Audit log screens | v0.4 (events are prepared now) |
| Installer and cloud deployment | v1.0 |
| Admin view of failed (dead-letter) messages | v0.2 |

## 10. Clarifications (answered by the owner, 2026-10-21)
| # | Question | Answer |
|---|---|---|
| C1 | Sessions in M1 | **M1 uses the fixed defaults** (30 minutes inactive, 12 hours maximum); M3 makes them settings |
| C2 | First admin's password | **Generated** strong random password at setup, **shown once**, must be changed at first login (no well-known default) |
| C3 | Email uniqueness | **Required and unique**, ignoring letter case |
| C4 | Username format | **3–50 characters**: letters, digits, `.` `_` `-`; unique ignoring letter case |
| C5 | Demo users' passwords | **One documented demo password** for all demo users, no forced change (the demo tool only runs on empty databases) |
| C6 | Re-activating users | **Allowed**; history and past actions stay unchanged |

## 11. Spec quality checklist (Claude checks before asking for approval)
- [x] Every requirement has an ID, a milestone and at least one acceptance criterion
- [x] No technology choices in the spec (only customer constraints)
- [x] Every acceptance criterion is testable; the one manual check is marked
- [x] Out-of-scope items are listed so nobody builds them by mistake
- [x] All clarifications answered (C1–C6)
- [x] Approved by the owner (2026-10-21)
