# Module discussion: IAM (Identity and access)

| | |
|---|---|
| **Status** | Decided by the owner (K1–K8, L1–L7), 2026-10-21 |
| **Feeds** | `/speckit-specify 001-platform-foundation` (sections 1–6) and `/speckit-plan` (section 7) |
| **Scope IDs** | IAM-01 to IAM-08 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) |
| **Related** | [ORG](org-hierarchy.md) (business roles, units, managers; usernames never change, P8) |
| **Owning service** | Identity (Gateway takes part in the login session, section 7) |

## What this file is
The result of the detailed discussion of the IAM module. Sections 1–6 are **what and why**, the input for
`spec.md`. Section 7 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
IAM controls **who can log in** and **what they may do**. Customers will run financial and health processes,
so it must be safe by default (constitution Article 7) without making daily login painful.

**System roles vs business roles:** IAM roles (Platform Admin, App Designer, User) are about **using the
platform**. ORG business roles (Credit Officer, …) are about **who approves**.

## 2. Already decided (scope IAM-01 to IAM-05)
- Username + password login, and logout.
- System roles: **Platform Admin**, **App Designer**, **User** (every active user has User).
- A first admin account is created at install; its password must be changed at first login.
- No self sign-up; the admin creates users, resets passwords, activates and deactivates users.
- Every API call needs a logged-in user, except login and health checks.

## 3. Admin screen: users
```
┌ Users ────────────────────────────────────────────────── [+ New user] ┐
│ Username   Name          Email              Roles            Status   │
│ ravi.k     Ravi Kumar    ravi@bank.in       User             Active   │
│ priya.s    Priya S       priya@bank.in      User, Designer   Active   │
│ admin      Administrator admin@bank.in      Admin            Active   │
│ meena.r    Meena R       meena@bank.in      User             Locked 🔒 │
└───────────────────────────────────────────────────────────────────────┘
  Actions: Edit · Reset password · Unlock · Deactivate / Activate
```

## 4. Owner's decisions
| # | Question | Decision |
|---|---|---|
| K1 | Login identifier | **Username** (e.g. an employee code); email stored separately for notifications |
| K2 | Password rules | **Installation settings with safe defaults:** minimum length 10; rejected if common or equal to the username; **expiry off** by default (can be set, e.g. 90 days); **reuse of the last N passwords** blocked (default 0, can be set, e.g. 5) |
| K3 | Wrong password | **5 failures → locked for 15 minutes**; the admin can unlock early |
| K4 | Login lifetime | Logged out after **30 minutes of inactivity** and after **12 hours** at most (both installation settings) |
| K5 | Forgot password | **Admin resets it** in v0.1; self-service email link later |
| K6 | Admin reset | The admin gets a **temporary password shown once**; the user must change it at the next login |
| K7 | Deactivating a logged-in user | Their **session ends immediately**; their next action is refused |
| K8 | Two-factor login | **Later** (security hardening, v1.0) |

## 5. Details accepted by the owner (L1–L7)
| # | Detail |
|---|---|
| L1 | User fields: username (cannot change, ORG P8), display name, email, mobile (optional). ORG adds primary unit, manager and business roles |
| L2 | A user can have **several system roles** (e.g. User + App Designer) |
| L3 | The **last active Platform Admin** cannot be deactivated or lose the admin role (prevents lock-out) |
| L4 | Every user can **change their own password** and see their profile |
| L5 | Logins, failures, lockouts and password changes publish events with **IDs only** (for the Audit service, v0.4) |
| L6 | The login endpoint is **rate-limited** per IP address (slows down password guessing) |
| L7 | Times are shown in the **browser's time zone** (NFR-05); no per-user time-zone setting in v0.1 |

## 6. Acceptance criteria (draft for spec.md)
**IAM-01 Login**
- IAM-01-AC1: A user logs in with username and password and logs out; after logout, protected pages and APIs are refused.
- IAM-01-AC2: A wrong password shows a general message ("Username or password is incorrect") that does not reveal which one was wrong.

**IAM-02 Roles**
- IAM-02-AC1: Designer screens need App Designer; directory and user screens need Platform Admin; the server refuses otherwise (constitution 7.2).
- IAM-02-AC2: A user can hold several system roles (L2); the last active Platform Admin cannot be deactivated or lose the role (L3).

**IAM-03 First admin**
- IAM-03-AC1: A new installation has one admin account that must change its password at first login.

**IAM-04 User administration**
- IAM-04-AC1: The admin creates, edits, activates and deactivates users; usernames cannot change (L1).
- IAM-04-AC2: An admin reset shows a temporary password once; the user must change it at the next login (K6).
- IAM-04-AC3: Deactivating a logged-in user ends their session; their next action is refused (K7).

**IAM-05 Protected APIs**
- IAM-05-AC1: Every API except login and health checks rejects calls without a valid login.

**IAM-06 Password policy (new scope ID)**
- IAM-06-AC1: Passwords shorter than the minimum, common passwords and passwords equal to the username are rejected (K2).
- IAM-06-AC2: With expiry set to 90 days, a user whose password is older must change it at login; with reuse set to 5, the last 5 passwords are refused (K2).
- IAM-06-AC3: Users can change their own password (L4).

**IAM-07 Lockout (new scope ID)**
- IAM-07-AC1: After 5 wrong passwords the account is locked for 15 minutes; the admin can unlock it (K3).
- IAM-07-AC2: Repeated login attempts from one IP address are slowed down or refused (L6).

**IAM-08 Session lifetime (new scope ID)**
- IAM-08-AC1: After 30 minutes without activity, or 12 hours in total, the user must log in again (K4).

## 7. Design notes (input for plan.md, not for spec.md)
- **ASP.NET Core Identity:** users, PBKDF2 password hashing, lockout (K3), password validators (K2 as custom
  validators: common-password list, username check, history of the last N hashes).
- **OpenIddict:** short-lived access tokens (about 15 minutes) and refresh tokens limited by K4.
- **Gateway as "BFF" (backend for frontend):** the browser holds only a **secure, HTTP-only, SameSite cookie**;
  the Gateway keeps the tokens and forwards them to services. JavaScript never sees a token, so injected
  scripts cannot steal it (current best practice for browser apps). **To be confirmed by the owner in the plan**
  (it shapes the Gateway's job).
- **Services** validate access tokens and check roles on the server for every action (constitution 7.2).
- **K7:** deactivation revokes the user's tokens in OpenIddict and changes their security stamp, so the next
  refresh fails; services also check a short-lived "user active" cache.
- **L6:** ASP.NET Core rate limiter on the login endpoint.
- **Data protection keys** stored in the Identity database, so cookies survive restarts and work with several instances.
