# Feature 006: Inbox and notifications (INB + NTF)

| | |
|---|---|
| **Status** | **Approved by the owner, 2026-10-21** (clarifications C1–C18 answered) |
| **Date** | 2026-10-21 |
| **Modules** | INB (Inbox and requests), NTF (Notifications) |
| **Scope IDs** | INB-01 to INB-05, INB-07, NTF-01, NTF-02, NTF-04 to NTF-07 ([v0.1 scope](../v0.1-scope.md)); INB-06 and NTF-03 stay in v0.2 |
| **Sources** | [INB discussion](../../docs/discussions/inbox-and-requests.md), [NTF discussion](../../docs/discussions/notifications.md), [constitution](../../docs/constitution.md) |
| **Next documents** | `plan.md` (how), `tasks.md` (steps); both only after this spec is approved |

> **This spec says what and why, never how.** Technology choices (frameworks, libraries, protocols) belong in
> `plan.md`. The only technical words here are **constraints the customer gives us** (e.g. "the customer's own
> mail server, reached by SMTP"), because they are requirements, not design choices.

## 1. Summary
This feature holds the screens people use every day and the emails that tell them something happened.
People start a request, see and act on their tasks, and follow their own requests. Administrators clear
stuck tasks. The platform sends emails in the background: emails a designer puts into a process, and a few
automatic ones. Administrators set up the mail server, test it, and see which emails failed.

**Why it matters:** the inbox is where most users spend their time. If it is slow or unclear, people stop
using the platform, whatever the engine can do. Emails make sure nobody waits for a decision they do not
know about. And a mail server problem must never stop the business: work goes on, and no email is lost
without an administrator seeing it.

**Related features** (rules they own are not repeated here):
- `001-platform-foundation`: users, system roles, installation settings (name, logo, time zone, currency, locale).
- `002-org-hierarchy`: approver rules (ORG-06) used as email recipients.
- `004-apps-and-form-builder`: forms, field permissions per step, active version (APP-03), restart on a new version (APP-05), disabled apps (APP-07).
- `005-workflow-engine`: task actions, claim and release, statuses (W10), history (WF-11), the Unassigned queue reasons (WF-07, WF-15, Q4), two people acting at once (WF-12), the Notification node in the designer (WF-02).

## 2. Users
| Persona | What they need from this feature |
|---|---|
| **Requester** | Start a request; follow it; get emails when it finishes or is sent back |
| **Approver** | See their tasks and the tasks they may claim; open a task and act on it quickly |
| **Platform Admin** | Find stuck tasks and reassign them; set up and test the mail server; see failed emails and resend them |
| **App Designer** | Write email templates with placeholders; choose recipients; turn automatic emails off or change their text per process |
| **Email recipient** | Receive a clear, safe email, possibly as someone outside the organization (e.g. an applicant's address from a form field) |

## 3. User stories and acceptance scenarios
Priority **P1** = needed for Milestone 1; **P2** = Milestone 2.

### US1: Start a request (P1, INB-01)
As a user, I see the processes I am allowed to start and open one, so that I can ask for something without knowing how the process works.
1. **Given** apps with active versions, **when** I open "Start a request", **then** I see only the processes I may start (WF-06), grouped by app, with a search box (Z4).
2. **Given** a process in a disabled app *(M3, APP-07)*, **then** it is not listed.
3. **When** I choose a process, **then** its start form from the active version opens.

### US2: See my tasks and claim offered tasks (P1, INB-02)
As an approver, I see the tasks assigned to me and the tasks offered to me, so that I know what to do next.
1. **Given** tasks assigned to me, **when** I open "My tasks", **then** I see only those, with request number, process, requester, step and waiting time, newest first, 25 per page (Z1).
2. **Given** a task offered to me and others that nobody has claimed, **when** I open "Available", **then** I see it and can claim it.
3. **Given** someone else claimed that task a moment before me, **when** I claim it, **then** I see a clear message and the list refreshes (I3).
4. **When** I sort, filter by app or process, or search by request number, **then** the list shows the matching tasks.
5. **When** I open a list or press Refresh, **then** I see the current data (I3).

### US3: Work on a task (P1; P2 for "All must approve" progress; INB-03)
As an approver, I open a task and see everything I need to decide, so that I can act with confidence.
1. **When** I open a task, **then** I see the form with only the fields allowed at this step, editable or view-only as configured (WF-03), the history timeline, the allowed action buttons and a comment box.
2. **Given** the step requires a comment for Reject or Send back, **when** I choose that action without a comment, **then** it is refused with a clear message.
3. *(M2)* **Given** an "All must approve" step, **then** I see how many have approved, e.g. "2 of 3 approved" (WF-14).

### US4: Follow my requests (P1, INB-04)
As a requester, I see all requests I started and where each one is, so that I do not have to ask anyone.
1. **When** I open "My requests", **then** each request shows its status (W10), current step and who it is with, e.g. "Credit Officers (3)" while nobody has claimed it (Z5).
2. **When** I open one, **then** I see the form read-only and the full history (WF-11).

### US5: Readable request numbers (P1, INB-05)
As anyone working with requests, I refer to a request by a short, readable number, so that phone calls and emails are clear.
1. **When** the first request of a process is submitted in 2027, **then** it gets `PREFIX-2027-000001` (Z3).
2. **When** two requests of one process are submitted at the same moment, **then** they get different numbers.
3. **When** a request is sent back, reopened or restarted and then resubmitted, **then** it keeps its number.

### US6: Clear stuck tasks (P1, INB-Z2)
As a Platform Admin, I see tasks that have nobody to work on them and give them to someone, so that no request gets stuck.
1. **When** I open the Unassigned queue, **then** I see each task with the reason it has no assignee.
2. **When** I reassign a task to an active user, **then** it leaves the queue and appears in that user's "My tasks".
3. **Given** any open task, **when** I find it by its request number, **then** I can reassign it (Z2).

### US7: Home counters (P2, INB-07)
As a user, I see on the home page how much is waiting for me, so that I know at a glance whether to act.
1. **When** I open the home page, **then** I see the number of my tasks, available tasks and my requests in progress, and each number matches its list (I1).

### US8: Design an email (P1, NTF-01, NTF-02)
As an App Designer, I add a Notification step with an email text and recipients, so that the right people hear about an important moment without any code.
1. **When** I write a subject and a plain-text body with placeholders (detail M1), **then** the sent email shows the request's values inside the standard layout: installation name and logo at the top, a "do not reply" footer (N1).
2. **When** I save a design with an unknown placeholder, **then** it is reported (detail M2).
3. **Given** a form value containing markup such as `<script>`, **then** the email shows it as text; it never runs (detail M3).
4. **When** I choose recipients (the requester, an approver rule, an email field of the form, fixed addresses), **then** each recipient gets their own email and cannot see the other addresses (detail M4).
5. **Given** a recipient without an email address, or a deactivated user, **then** they are skipped and the skip is noted in the delivery log (detail M4).

### US9: The requester hears when the request finishes (P1, NTF-04)
As a requester, I get an email when my request finishes, so that I do not have to keep checking.
1. **When** my request ends as Approved, Completed, Rejected or Rejected (final), **then** I get an email with the built-in default text.
2. **Given** the designer turned this email off for the process, **then** no email is sent.

### US10: Emails never block work, and failures are visible (P1, NTF-05)
As the business, I need work to continue when the mail server is down, and failed emails to be seen and resent, so that no message is silently lost.
1. **Given** the mail server is down, **when** an approver acts, **then** the action completes at once; the email is sent later.
2. **Given** an email keeps failing, **then** after the first send it is retried 5 times with growing waits (1 min, 5 min, 15 min, 1 h, 6 h) and then shown as **Failed** (N5, C8).
3. **When** an admin opens the email log, **then** they see each email as Sent, Retrying, Failed or Waiting for email settings, and can **Resend** a failed one.
4. **Given** an email older than 30 days, **then** the log keeps only who, when and status; the email itself is gone (detail M6).

### US11: Set up and test the mail server (P1, NTF-07)
As a Platform Admin, I enter the customer's mail server settings on a screen and send a test email, so that I know emails will arrive before users depend on them.
1. **When** I open the SMTP settings screen, **then** I can set host, port, security mode (none, STARTTLS or SSL), username, password, sender address, sender name and an active flag (SM5).
2. **When** I save a password, **then** it is stored encrypted and is never shown again, on any screen, in any answer or in any log (SM2).
3. **When** I press "Send test email", **then** I see success, or the error from the mail server (N6).
4. **Given** no mail server settings yet, **when** the platform has emails to send, **then** they wait as "Waiting for email settings" and do not use up their retries; once settings are saved, they are sent.
5. **Given** a development installation, **then** it comes with working test mail settings; a production installation starts empty (SM3).
6. **Given** an installer without the screen at hand, **then** they can set the same values from the command line, typing the password without it being shown (SM4).

### US12: More automatic emails (P2, NTF-06)
As a requester, I am told when my request is sent back to me or restarted on a new version, so that I fix it without delay.
1. **When** my request is sent back (WF-08), **then** I get an automatic email.
2. *(M3)* **When** an admin restarts my request on a new version (APP-05), **then** I get an automatic email (built with APP-05, C15).
3. **Given** the designer turned one of these emails off for the process, **then** it is not sent.
4. **Given** the designer wrote their own text for an automatic email (including the "request finished" email), **then** that text replaces the default (N3, N4).

## 4. Requirements
| ID | Requirement | Milestone |
|---|---|---|
| INB-01 | "Start a request" lists the processes the user may start (WF-06) from the **active** versions of **enabled** apps, grouped by app, with a search box (Z4). Choosing one opens its start form from the active version. Processes of disabled apps are hidden once APP-07 exists (M3) | M1 |
| INB-02 | **My tasks** (assigned to me) and **Available** (offered to me, nobody has claimed it; I can claim). Columns: request number, process, requester, step, waiting time. Paged on the server, **25 rows per page, newest first** by default (Z1); sortable; filter by app or process; search by request number (the full number or its beginning, ignoring letter case, C6). A request sent back to the requester, or reopened and waiting for resubmission, is a task in the requester's **My tasks** (C2). Lists refresh when opened, plus a **Refresh** button; no live push (I3). Claiming a task someone else just claimed shows a clear message and refreshes the list (I3). With 10,000 requests and 50,000 tasks a page loads in under 1 second (NFR-02) | M1 |
| INB-03 | **Task screen:** the form with only the fields allowed at this step, editable or view-only as configured (WF-03); history timeline; only the allowed action buttons; comment box, with a required comment enforced (WF-03). **M2:** an "All must approve" step shows progress, e.g. "2 of 3 approved" (WF-14). The actions themselves are specified in `005-workflow-engine` | M1 / M2 |
| INB-04 | **My requests:** every request I started, with status (W10), current step and who it is with, e.g. "Credit Officers (3)" while unclaimed (Z5). With several open steps (a parallel section) there is **one line per open step**; an unassigned task shows "Unassigned (waiting for an administrator)" (C3). A request waiting for my action (sent back or reopened) is marked, with a button to open it (C2). Opening one shows the form read-only and the full history (WF-11) | M1 |
| INB-05 | **Request number** `PREFIX-YYYY-NNNNNN`. The prefix is set per process: letters and digits only, 2–6 characters, **unique across the installation**, checked when the process is saved and published (C4). The counter is per process per year (installation time zone); after 999,999 the number grows to 7 digits and never wraps (C5). The number is assigned **when the request is submitted**; it is never reused (Z3) | M1 |
| INB-07 | **Home counters:** my tasks, available tasks, my requests in progress (I1); "in progress" means every unfinished request: In progress, Sent back and Waiting for resubmission (C1). Each number matches its list | M2 |
| INB-Z2 | **Unassigned queue** (Platform Admin): tasks with no assignee and the reason; the admin reassigns one to an active user. The admin can also **reassign any open task** found by its request number. Every reassign needs a **reason**, recorded in the history (C7), and the target must pass the checks of `005-workflow-engine` (WF-07, 005 C5). Needed by WF-07; this is not INB-06 (Z2) | M1 |
| NTF-01 | **Email templates:** subject and plain-text body with line breaks (N1), placed inside a fixed, clean layout with the installation name and logo at the top and a "do not reply" footer (N1); until branding arrives in M3 (PLT-08) the header shows the installation name from the default settings and no logo (C14). Placeholders: request number, process name, status, current step, requester name, today's date, a **link** to the request (opens after login), and any form field by key (detail M1). Form fields show readable values: a blank or hidden field is empty text; Dropdown and Radio show the label; Checkboxes show their labels separated by commas; User picker shows the display name; File upload shows the file names; grids cannot be placeholders in v0.1 (C13). Unknown or unsupported placeholders are reported when the design is saved (detail M2). Values are shown as text, never run (detail M3). Values are formatted for people using the installation's locale and currency, e.g. `₹12,34,567.00`, `17-10-2026` (detail M7). One language for emails (detail M5). Form values are allowed in emails; the designer is responsible (N2) | M1 |
| NTF-02 | A **Notification node** sends an email to any mix of: the requester, an approver rule (ORG-06), an email address from a form field, fixed addresses. **Each recipient gets their own email**; addresses are not exposed; an address that appears more than once (ignoring letter case) gets one email (C12). A recipient without an email address, or a deactivated user, is skipped and noted in the delivery log (detail M4). For an approver-rule recipient: if the directory cannot be reached, the user's action fails cleanly and they retry (like W8); if the rule finds nobody, no email is sent and this is noted in the delivery log (C11). Otherwise the node never stops the flow (X1) | M1 |
| NTF-04 | When a request **finishes**, the requester gets an automatic email with a built-in default text. In v0.1 a request finishes as Approved, Completed, Rejected or Rejected (final); "withdrawn" comes with WF-10 in v0.2. The email can be **turned off per process** (N3) | M1 |
| NTF-05 | Emails are sent **in the background**; the workflow never waits for them. A failing email is tried **5 attempts** with growing waits (1 min, 5 min, 15 min, 1 h, 6 h), then marked **Failed** (N5); the first send does not count, so an email is sent at most 6 times (C8). Admins see the delivery log (Sent, Retrying, Failed, Waiting for email settings, and skipped recipients) with recipient, subject, process, request number, status, attempts and last error, but **not the email body** (C10). Admins can **Resend** a Failed email while its content is kept; a resend starts a fresh round with the same waits, and the log shows who resent it (N5, C9). The log keeps the full email for **30 days** after it was sent or marked Failed (for Resend); after that only who, when and status remain (detail M6, C18). System logs never hold email text or addresses, only IDs (constitution 11.2) | M1 |
| NTF-06 | Automatic emails also for **sent back** (WF-08) and **restarted on a new version** (APP-05), each with a built-in default text (N3). **Each automatic email**, including "request finished" (NTF-04), can be turned off and its text **overridden per process**; otherwise the default text is used (N3, N4). Override texts follow the same placeholder rules as templates. **M2:** the sent-back email and the on/off and override settings. **M3:** the restart email, built and tested together with APP-05 (C15) | M2 / M3 |
| NTF-07 | **SMTP settings stored by the platform**, not in a configuration file (N6, changed by the owner 2026-10-21): host, port, security mode (none / STARTTLS / SSL), username, password, sender address, sender name, active flag (SM5). The password is stored **encrypted**, is **write-only** (never shown, never returned, never logged) (SM2). A Platform Admin sets them on an **SMTP settings screen** and can press **"Send test email"** to see success or the error (N6, SM4); the test email goes to an address the admin types (filled in with their own email), is sent at once without retries, and is kept in the delivery log (C16). Switching the **active** flag off has the same effect as not configured (C17). Installers can also set them from the command line, with the password typed hidden (SM4). A development installation comes with working test mail settings; production starts empty (SM3). While SMTP is not configured, emails wait as **"Waiting for email settings"** and do not use up their retries (NTF discussion, section 4a). The rest of the Settings screen stays in M3 (PLT-08) | M1 |

## 5. Acceptance criteria
Every criterion gets at least one automated test whose name contains its ID (constitution 1.4), unless marked *manual*.

| ID | Criterion |
|---|---|
| INB-01-AC1 | The list shows only processes of enabled apps' active versions that the user may start (WF-06, APP-07). *(The disabled-app part is tested when APP-07 arrives in M3.)* |
| INB-01-AC2 | Starting a process opens its start form from the active version |
| INB-01-AC3 | Processes are grouped by app, and the search box narrows the list (Z4) |
| INB-02-AC1 | My tasks shows only tasks assigned to me; Available shows tasks offered to me that nobody has claimed |
| INB-02-AC2 | Claiming a task that someone else claimed a moment earlier shows a clear message, and the list refreshes (I3) |
| INB-02-AC3 | Lists are paged (25 per page), sortable, filterable by app/process, and searchable by request number (Z1) |
| INB-02-AC4 | With 10,000 requests and 50,000 tasks, a list page loads in under 1 second (NFR-02) |
| INB-02-AC5 | Each row shows request number, process, requester, step and waiting time; the default order is newest first (Z1) |
| INB-02-AC6 | A list shows current data when it opens, and the Refresh button reloads it (I3) |
| INB-02-AC7 | Searching `ln-2026-0001` finds `LN-2026-000123`; searching the full number finds exactly that request; a text that is not the beginning of a number finds nothing (C6) |
| INB-02-AC8 | A request sent back to the requester, or reopened and waiting for resubmission, appears as a task in the requester's My tasks (C2) |
| INB-03-AC1 | The form shows only the fields allowed at this step, editable or view-only as configured (WF-03) |
| INB-03-AC2 | Only the allowed actions appear; a required comment is enforced (WF-03) |
| INB-03-AC3 | *(M2)* An "All must approve" step shows how many have approved (WF-14) |
| INB-03-AC4 | The task screen shows the history timeline and a comment box |
| INB-03-AC5 | A user who is not the task's assignee or claimant cannot act on it; the server refuses (NFR-01) |
| INB-04-AC1 | Shows each request I started with status, current step and who it is with (Z5) |
| INB-04-AC2 | Opening one shows the form read-only and the full history (WF-11) |
| INB-04-AC3 | A user who is not the requester, has not worked on the request and is not an admin cannot open it (NFR-01) |
| INB-04-AC4 | A request with three open parallel steps shows three lines, one per step with who it is with; an unassigned task shows "Unassigned (waiting for an administrator)"; a request waiting for my action is marked and has a button to open it (C2, C3) |
| INB-05-AC1 | The first request of a process in 2027 gets `PREFIX-2027-000001`; numbers never repeat, even when two requests are submitted at the same moment (Z3) |
| INB-05-AC2 | The year comes from the installation time zone: a request submitted late on 31 December local time gets that year, even if it is already 1 January in UTC |
| INB-05-AC3 | A request keeps its number through send back, reopen and restart; no number is ever given to a second request |
| INB-05-AC4 | A prefix already used by another process (ignoring letter case), shorter than 2 or longer than 6 characters, or with other characters than letters and digits, is reported when the process is saved and blocks publishing (C4) |
| INB-05-AC5 | The 1,000,000th request of a process in one year gets a 7-digit number, e.g. `LV-2026-1000000` (C5) |
| INB-07-AC1 | *(M2)* The home page shows the number of my tasks, available tasks and my requests in progress, matching the lists |
| INB-07-AC2 | *(M2)* "My requests in progress" counts requests with status In progress, Sent back and Waiting for resubmission, and no finished ones (C1) |
| INB-Z2-AC1 | The Unassigned queue lists tasks with their reason; the admin can reassign one to an active user |
| INB-Z2-AC2 | The admin finds any open task by its request number and reassigns it to an active user; only Platform Admins can do this |
| INB-Z2-AC3 | A reassign without a reason is refused; the history records who reassigned, when, from whom, to whom and why (C7) |
| NTF-01-AC1 | A template with placeholders renders the request's values; form values are shown as text, never run (detail M3) |
| NTF-01-AC2 | Saving a template with an unknown placeholder reports it (detail M2) |
| NTF-01-AC3 | The email is shown inside the standard layout (N1) |
| NTF-01-AC4 | Every placeholder of detail M1 renders; the link opens the request after login |
| NTF-01-AC5 | Currency, numbers and dates are formatted with the installation's locale and currency (detail M7) |
| NTF-01-AC6 | *(manual checklist)* The standard layout looks clean and readable in common desktop and web mail programs |
| NTF-01-AC7 | Placeholders show readable values: a blank or hidden field is empty; a Dropdown shows "Sick leave", not `SICK`; Checkboxes show labels separated by commas; a User picker shows the display name; a File upload shows the file names; a grid used as a placeholder is reported when the design is saved (C13) |
| NTF-01-AC8 | In M1 the email header shows the installation name and no logo (C14) |
| NTF-02-AC1 | Recipients can be the requester, an approver rule, an email field and fixed addresses; each recipient gets a separate email (detail M4) |
| NTF-02-AC2 | A recipient without an email address, or a deactivated user, is skipped and the skip is logged in the delivery log (detail M4) |
| NTF-02-AC3 | When the requester is also found by the approver rule of the same Notification, they get one email, not two (C12) |
| NTF-02-AC4 | When the directory cannot be reached while resolving an approver-rule recipient, the user's action fails, nothing is saved, and the user is asked to retry; when the rule finds nobody, the request moves on and the delivery log notes that no recipient was found (C11) |
| NTF-04-AC1 | When a request finishes, the requester gets an email unless the process turned it off |
| NTF-04-AC2 | The email is sent for each way a request finishes in v0.1 (Approved, Completed, Rejected, Rejected (final)) and uses the built-in default text |
| NTF-05-AC1 | The workflow action completes even when the mail server is down |
| NTF-05-AC2 | A failing email is retried 5 times with the N5 waits and then shown as Failed; an admin can Resend it |
| NTF-05-AC3 | After 30 days the delivery log no longer holds the email body (detail M6) |
| NTF-05-AC4 | Only Platform Admins can open the email log; it shows each email's status |
| NTF-05-AC5 | An automated scan of system log output finds no email subject, body or address, only IDs (constitution 11.2) |
| NTF-05-AC6 | An email that always fails is sent 6 times in total (the first send and 5 retries after 1 min, 5 min, 15 min, 1 h, 6 h) and then shown as Failed (C8) |
| NTF-05-AC7 | Only a Failed email whose content is still kept can be resent; a resend starts a fresh round of retries, and the log shows who resent it (C9) |
| NTF-05-AC8 | The email log shows recipient, subject, process, request number, status, attempts and last error, but never the email body (C10) |
| NTF-05-AC9 | An email that waited 40 days for mail server settings still has its content and is sent once settings are saved; its 30 days start when it is sent or marked Failed (C18) |
| NTF-06-AC1 | *(M2)* Send back sends its automatic email to the requester; it can be turned off per process, and a designer's override text replaces the default (N3, N4) |
| NTF-06-AC3 | *(M3)* Restart (APP-05) sends its automatic email to the requester; it can be turned off and overridden per process like the others (C15) |
| NTF-06-AC2 | *(M2)* An override text for the "request finished" email replaces its default; override texts report unknown placeholders when saved (N4, detail M2) |
| NTF-07-AC1 | An admin can send a test email using the installation's SMTP settings and sees success or the error (N6) |
| NTF-07-AC2 | Only Platform Admins can view or change the SMTP settings; the saved password is never returned, shown or logged (SM2) |
| NTF-07-AC3 | While SMTP is not configured, new emails wait as "Waiting for email settings" without using retries; after settings are saved they are sent |
| NTF-07-AC4 | A new development installation sends email at once with its test mail settings; a production installation starts with no settings (SM3) |
| NTF-07-AC5 | The settings can be set from the command line; the password is not shown while typed and is stored encrypted (SM4) |
| NTF-07-AC6 | The test email goes to the address the admin typed (pre-filled with their own email), is sent once without retries, and appears in the delivery log (C16) |
| NTF-07-AC7 | With the active flag off, new emails wait as "Waiting for email settings" without using retries, and are sent when it is switched on again (C17) |

## 6. Edge cases
- Two approvers claim the same offered task at the same moment: one gets it; the other sees a clear message and a refreshed list (I3, WF-12).
- A task is reassigned or cancelled while its approver has it open: their action is refused with a clear message (WF-12, rules in `005-workflow-engine`).
- An approver is deactivated: their open tasks move to the Unassigned queue (Q4, Y7), where the admin sees them with the reason.
- A request is submitted just before midnight on 31 December, local time: it gets the old year (installation time zone, Z3).
- The mail server is down for hours: work continues; emails retry and then become Failed; the admin resends them.
- The mail server settings are wrong: the test email shows the error; until fixed, real emails retry and fail.
- No mail server is configured yet: emails wait as "Waiting for email settings" and keep all their retries.
- A recipient outside the organization (e.g. from an email field) follows the link: they reach the login page; only people allowed by the view rules can see the request (NFR-01).
- A form value contains markup or a script: the email shows it as plain text (detail M3).
- A failed email is older than 30 days: its body is gone, so it can no longer be resent (detail M6).
- An app is disabled *(M3)*: its processes leave "Start a request"; its running requests and tasks stay in the lists (APP-07).
- A request is in a parallel section: it has several open steps at once; "My requests" shows one line per open step (C3).
- An email waits for mail server settings for more than 30 days: it keeps its content and is sent once settings exist; its 30 days start only when it is sent or marked Failed (C18).

## 7. Non-functional requirements that apply
From the [v0.1 scope](../v0.1-scope.md) section 6: NFR-01 (security: who may act and view), NFR-02 (lists under 1 second; actions under 1 second, excluding email delivery), NFR-03 (reliability: no lost events, safe repeats), NFR-04 (SQL Server 2019 or later, no cloud-only services, Windows without containers), NFR-05 (UTC storage, local display of waiting times and dates), NFR-06 (browsers, desktop first), NFR-07 (privacy: no form values or personal data in logs), NFR-08 (no hard delete), NFR-10 (translation files; dates, numbers and currency by locale), NFR-11 (keyboard and labels), NFR-12 (coverage).

## 8. Key entities (business view, not database design)
| Entity | Meaning |
|---|---|
| **Request** | One case started by a requester from a process: request number, process, app version, status (W10), current step(s), requester, form data, history |
| **Request number** | Readable, unique number `PREFIX-YYYY-NNNNNN`, counted per process per year, given on submit, never reused |
| **Task** | Work for a person at an Approval step: assigned to one user, offered to several (claimable), or unassigned (with a reason) |
| **Task list** | A user's view of tasks: "My tasks" or "Available"; paged, sorted, filtered |
| **Unassigned queue** | Tasks that have no assignee, each with its reason, waiting for a Platform Admin |
| **Email template** | Subject and plain-text body with placeholders, used by a Notification node |
| **Notification node recipients** | Who gets a designed email: requester, approver rule, email field, fixed addresses |
| **Automatic email** | A platform-sent email (request finished; M2: sent back; M3: restarted) with a default text, which the designer can turn off or override per process |
| **Email (delivery log entry)** | One email to one recipient: status (Sent, Retrying, Failed, Waiting for email settings), attempts, times, last error; full content for 30 days, then only who, when and status |
| **Mail server settings** | Host, port, security mode, username, password (encrypted, write-only), sender address, sender name, active flag |

## 9. Out of scope
| Item | When |
|---|---|
| All requests screen for admins (INB-06) | v0.2 |
| "New task" email to assignees of an Approval node (NTF-03) | v0.2 |
| Email when a request is withdrawn (with WF-10) | v0.2 |
| "Tasks I completed" tab (I2) | Later (a request can still be opened by its number) |
| Live updates pushed to the screen (I3) | Later |
| Bulk approve (I4) | Later |
| Print view (I5) | Later, with PDF documents (v0.4) |
| In-app notifications, the bell (I6); any channel other than email | Later |
| Rich-text email editor (N1) | Later |
| Email attachments (N7) | Later |
| Automatic blocking of sensitive fields in emails (N2, constitution 11.3) | Later |
| Emails in more than one language (detail M5) | Later |
| Approver forwards a task / delegation (W9) | v0.3 |
| SLA timers and escalation | v0.3 |
| The rest of the Settings screen (PLT-08) | M3, in `001-platform-foundation` |

## 10. Clarifications (answered by the owner, 2026-10-21)
| # | Question | Answer |
|---|---|---|
| C1 | Which requests count as "my requests in progress"? | **All unfinished**: In progress, Sent back, Waiting for resubmission |
| C2 | Where does the requester find a sent-back or waiting request? | **As a task in My tasks**, and marked in My requests with a button to open it |
| C3 | "Current step" for parallel sections and unassigned tasks | **One line per open step**; "Unassigned (waiting for an administrator)" |
| C4 | Must request number prefixes be unique? | **Yes**: unique across the installation, checked on save and publish; letters and digits, 2–6 characters |
| C5 | After request 999,999 in a year | **The number grows to 7 digits**; it never wraps |
| C6 | How search by request number matches | **Full number or its beginning**, ignoring letter case |
| C7 | Reassign rules | **Reason required** and recorded in the history; the target passes the checks of 005 (005 C5) |
| C8 | Does the first send count as one of the 5 attempts? | **No**: first send plus 5 retries after the five waits (6 sends at most) |
| C9 | Which emails can be resent? | **Only Failed emails while their content is kept**; a fresh round of retries; the log shows who resent it |
| C10 | May admins read email bodies in the log? | **No**: details only; the body is kept only for Resend |
| C11 | Notification to an approver rule: directory down, or nobody found | **Directory down: the action fails cleanly** (like W8); **nobody found: no email, noted in the log** |
| C12 | The same address twice | **One email per distinct address** |
| C13 | How placeholders show special fields | **Readable values** (empty, labels, display name, file names); grids not allowed in v0.1 |
| C14 | Email branding before M3 | **Installation name, no logo** until M3 |
| C15 | When the restart email is built | **M2**: sent-back email and the on/off and override settings; **M3**: restart email with APP-05 |
| C16 | Where the test email goes | **An address the admin types** (pre-filled with their own); no retries; kept in the log |
| C17 | What the active flag off means | **Same as not configured**: emails wait and keep their retries |
| C18 | When the 30 days start | **When the email is sent or marked Failed** |

## 11. Spec quality checklist (Claude checks before asking for approval)
- [x] Every requirement has an ID, a milestone and at least one acceptance criterion
- [x] No technology choices in the spec (only customer constraints)
- [x] Every acceptance criterion is testable; the one manual check is marked
- [x] Out-of-scope items are listed so nobody builds them by mistake
- [x] All clarifications answered (C1–C18, 2026-10-21)
- [x] Approved by the owner (2026-10-21)
