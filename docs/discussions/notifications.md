# Module discussion: NTF (Notifications)

| | |
|---|---|
| **Status** | Decided by the owner (N1–N8, M1–M7), 2026-10-21 |
| **Feeds** | `/speckit-specify 006-inbox-notifications` (sections 1–6) and `/speckit-plan` (section 7) |
| **Scope IDs** | NTF-01, NTF-02, NTF-04 to NTF-07 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) (NTF-03 moved to v0.2) |
| **Related** | [WF](workflow-engine.md) (Notification node, send back), [APP](apps-and-versions.md) (restart email), [FRM](form-builder.md) (currency, labels language) |
| **Owning services** | Runtime (templates, rendering), Worker (sending, retries, delivery log) |

## What this file is
The result of the detailed discussion of the NTF module. Sections 1–6 are **what and why**, the input for
`spec.md`. Section 7 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
The platform sends **emails** so people know when something needs them or has happened. Email is the only
channel in v0.1 (INB I6). Two kinds:
- **Designed emails:** a **Notification node** in a process (NTF-02), e.g. "Loan sanctioned" to the applicant.
- **Automatic emails:** sent by the platform itself (N3), e.g. "Your request was approved".

Emails are sent **in the background**; the workflow never waits for them (NTF-05).

## 2. How an email travels
```
Runtime: request reaches a Notification node (or an automatic-email event)
   │ 1. render the template with the request's data (Fluid / Liquid)
   │ 2. save a "send email" message in the outbox (same transaction)
   ▼
Worker: receives the message
   │ 3. send through SMTP; on failure retry later (N5)
   │ 4. record the result in the delivery log
   ▼
Admin: "Email log" screen shows Sent / Retrying / Failed, with Resend
```

## 3. A template in the designer
```
┌─ Notification: Loan sanctioned ──────────────────────────────────┐
│ To       [✓] Field: ApplicantEmail   [ ] Requester               │
│          [ ] Approver rule …         [ ] Fixed: loans@bank.in    │
│ Subject  Loan {{ request.number }} sanctioned                    │
│ Body     Dear {{ form.ApplicantName }},                          │
│          Your loan of {{ form.LoanAmount }} has been sanctioned. │
│          View it here: {{ request.link }}                        │
│                                   [Insert placeholder ▾]          │
└───────────────────────────────────────────────────────────────────┘
```

## 4. Owner's decisions
| # | Question | Decision |
|---|---|---|
| N1 | Email body editor | **Plain text with line breaks**, placed inside a **fixed, clean HTML layout** (installation name/logo at the top, "do not reply" footer). Rich-text editor later |
| N2 | Form values in emails | **Allowed**; the designer is responsible. Sensitive fields (constitution 11.3, later) will be blocked automatically |
| N3 | Automatic emails in v0.1 | **Three:** request finished (NTF-04), sent back to you (WF-08), restarted on a new version (APP-05). Each has a built-in default text and can be **turned off per process** |
| N4 | Changing the automatic texts | The designer can **override them per process**; otherwise the default text is used |
| N5 | Retries | **5 attempts** with growing waits (1 min, 5 min, 15 min, 1 h, 6 h), then **Failed**. Admins see failures and can **Resend** |
| N6 | SMTP settings | ~~Installation configuration~~ **Changed by the owner on 2026-10-21:** SMTP **host, port, username and password** (plus sender address, sender name and TLS mode) are stored in a **database table**, configurable later. The password is stored **encrypted**, never shown or logged. Plus an admin **"Send test email"** button. Details SM1–SM5 in section 4a |
| N7 | Attachments | **Later** |
| N8 | Email library | **MailKit** (MIT licence), **approved** under constitution 1.6 |

## 4a. SMTP settings in a table (owner's change, 2026-10-21; decided: SM1–SM5 all "a")
**Decision:** table in `flowza_worker` (SM1); password encrypted, write-only, never logged (SM2); development
seed for smtp4dev (SM3); the `flowza smtp-settings` command **and an admin screen** to configure the values
(SM4: the owner asked for the screen, so the **SMTP settings screen and the "Send test email" button (NTF-07)
move into Milestone 1** with feature 006; the rest of the Settings screen stays in M3); fields as in SM5.
| # | Question | Options (⭐ = Claude's recommendation) |
|---|---|---|
| SM1 | Which service owns the table | **a) Worker (it sends the emails), table in `flowza_worker` ⭐** · b) Identity, with the other installation settings |
| SM2 | Password protection | **a) Encrypted with ASP.NET Core Data Protection; the key ring is protected by Windows DPAPI on-premises (Azure Key Vault on Azure); the password is write-only (never returned by any API, never logged) ⭐** · b) Plain text (breaks constitution 7.6) |
| SM3 | Development | **a) A development seed points to smtp4dev (localhost, no login) so emails work at once; production stays empty until the admin configures it ⭐** · b) Empty everywhere |
| SM4 | How to configure before the Settings screen exists (M3) | **a) A command `flowza smtp-settings` that asks for the password without showing it and stores it encrypted (an encrypted value cannot be typed into the table by hand); the screen comes in M3 ⭐** · b) Bring the SMTP part of the Settings screen into M1 |
| SM5 | Fields | **a) Host, port, TLS mode (none / STARTTLS / SSL), username, password, sender address, sender name, active flag ⭐** · b) Other |

While SMTP is not configured, emails wait as "Waiting for email settings" and do not use up their retries (N5).

## 5. Details accepted by the owner (M1–M7)
| # | Detail |
|---|---|
| M1 | Placeholders: request number, process name, status, current step, requester name, today's date, a **link** to the request (opens after login), and any form field by key |
| M2 | Unknown placeholders are reported **when the design is saved** (like EXP-04) |
| M3 | Values are **HTML-escaped**: `<script>` is shown as text, never run |
| M4 | Each recipient gets **their own email** (addresses are not exposed). A recipient without an email address, or a deactivated user, is skipped and noted in the delivery log |
| M5 | One language for emails in v0.1 (FRM R4) |
| M6 | The delivery log keeps the full email for **30 days** (for Resend); after that only who, when and status remain (constitution 11) |
| M7 | Values are formatted for people (e.g. `₹12,34,567.00`, `17-10-2026`) using the installation's locale and currency (FRM F3) |

## 6. Acceptance criteria (draft for spec.md)
**NTF-01 Templates**
- NTF-01-AC1: A template with placeholders renders the request's values; form values are HTML-escaped (M3).
- NTF-01-AC2: Saving a template with an unknown placeholder reports it (M2).
- NTF-01-AC3: The email is shown inside the standard layout (N1).

**NTF-02 Notification node**
- NTF-02-AC1: Recipients can be the requester, an approver rule, an email field and fixed addresses; each recipient gets a separate email (M4).
- NTF-02-AC2: A recipient without an email address is skipped and the skip is logged (M4).

**NTF-04 / NTF-06 Automatic emails**
- NTF-04-AC1: When a request finishes, the requester gets an email unless the process turned it off.
- NTF-06-AC1: Send back and restart (APP-05) send their automatic emails to the requester; each can be turned off per process, and a designer's override text replaces the default (N3, N4).

**NTF-05 Delivery**
- NTF-05-AC1: The workflow action completes even when the mail server is down.
- NTF-05-AC2: A failing email is retried 5 times with the N5 waits and then shown as Failed; an admin can Resend it.
- NTF-05-AC3: After 30 days the delivery log no longer holds the email body (M6).

**NTF-07 Settings (new scope ID)**
- NTF-07-AC1: An admin can send a test email using the installation's SMTP settings and sees success or the error (N6).

## 7. Design notes (input for plan.md, not for spec.md)
- **Runtime renders, Worker sends.** Templates use **Fluid** (Liquid; cannot run code). Rendering is in Runtime
  because only Runtime has the form data. Worker receives `SendEmailRequested` (subject, HTML body, recipients).
- **`IEmailSender`:** SMTP via **MailKit** in v0.1; Azure Communication Services or others later behind the
  same interface (constitution 6.2).
- **Retries:** Wolverine scheduled retries with the N5 waits; delivery log in Worker's database.
- **Delivery guarantee: at least once.** SMTP cannot promise exactly once; in a rare crash an email may be
  sent twice, but it is never lost.
- **Privacy:** the message transport and the delivery log hold email bodies with personal data; the transport
  cleans processed messages quickly and the log purges bodies after 30 days (M6). Logs contain IDs only (constitution 11.2).
