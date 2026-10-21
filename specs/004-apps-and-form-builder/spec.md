# Feature 004: Apps and form builder (APP + FRM)

| | |
|---|---|
| **Status** | **Approved by the owner, 2026-10-21** (clarifications C1–C13 answered) |
| **Date** | 2026-10-21 |
| **Modules** | APP (Apps and versions), FRM (Form builder) |
| **Scope IDs** | APP-01 to APP-07, FRM-02 to FRM-14 ([v0.1 scope](../v0.1-scope.md)). The scope has no FRM-01; the gap is kept so that IDs never change (constitution 1.3) |
| **Sources** | [APP discussion](../../docs/discussions/apps-and-versions.md), [FRM discussion](../../docs/discussions/form-builder.md), [ADR-0002](../../docs/decisions/0002-form-data-original-and-read-copy.md), [ADR-0003](../../docs/decisions/0003-form-data-storage-layers.md), [constitution](../../docs/constitution.md) |
| **Next documents** | `plan.md` (how), `tasks.md` (steps); both only after this spec is approved |

> **This spec says what and why, never how.** Technology choices (frameworks, libraries, storage design)
> belong in `plan.md`. The "design notes" sections of the APP and FRM discussions go there, not here.

## 1. Summary
An **app** is a box that holds one business solution (for example a leave approval or a loan approval):
its forms and its processes. The whole app is **versioned as one unit** (v1, v2, v3). A designer edits a
draft, publishes it with a note, and chooses which published version is **active**. New requests use the
active version; running requests stay on the version they started with. An administrator can restart
running requests on the active version, and an app can be disabled, but never deleted.

The **form builder** lets designers build data-entry screens by drag-and-drop, with no code. One form
definition is used three ways: the requester **fills** it, approvers **see or edit** the parts allowed at
their step, and the server **checks** it again before saving. The server always decides.

**Why it matters:** customers build their own processes by configuration, never by code. Forms are where
their business data enters the platform. Versioning the whole app means a process can never point at a
field that a newer form removed, and a running request never changes under the people working on it.

> **Related features.** Expressions (formulas, functions, blank values, the save check) are specified in
> `003-expression-language`. The process designer, publish checks of processes, per-step field permissions
> and the workflow engine are in `005-workflow-engine`. "Start a request", the task screen and request
> screens are in `006-inbox-notifications`, and so are the automatic emails (including the restart email).
> This spec refers to those rules; it does not repeat them.

## 2. Users
| Persona | What they need from this feature |
|---|---|
| **App Designer** | Create apps; build forms by drag-and-drop; preview them; publish versions; choose the active version; (M3) disable and enable apps |
| **Requester** | Fill forms that are clear, check their input early, and never lose work because of a later design change |
| **Approver** | See and edit only the parts of the form allowed at their step; never overwrite a colleague working in parallel |
| **Platform Admin** | (M3) Restart running requests on the active version after a design change |

## 3. User stories and acceptance scenarios
Priority **P1** = needed for Milestone 1; **P2** = Milestone 2; **P3** = Milestone 3.

### US1: Create an app (P1, APP-01)
As an App Designer, I create an app for one business solution, so that its forms and processes stay together.
1. **When** the designer creates an app with a code, a name and a description, **then** it appears in the apps list.
2. **Given** an app with code `HRLEAVE`, **when** someone creates another app with the same code, **then** it is rejected.
3. **Given** an existing app, **then** its code cannot be changed (Q1).
4. **Given** any user with the App Designer role, **then** they can open and edit any app (A2).

### US2: Draft and publish a version (P1, APP-02, FRM-08)
As an App Designer, I work on a draft and publish it as a frozen version, so that requests always run on a design that cannot change under them.
1. **Given** an app with a draft, **when** the designer asks for a new draft from a version, **then** it is refused, because an app has only one draft at a time (A1).
2. **When** the designer publishes the draft without a note, **then** it is refused; the note is required (A6).
3. **When** any form or process of the draft fails a check, **then** nothing is published and every error points to its form, field or node.
4. **When** all checks pass, **then** the version gets the next whole number (1, 2, 3, in publish order) and can never be edited or discarded.
5. **Given** two designers editing the same form in the draft, **when** the second saves a copy that the first has already changed, **then** the second save is refused with a message such as "Changed by Priya at 10:42, reload" (A8).
6. **When** the designer discards the draft, **then** it is gone from the version list; it was only marked as deleted (Q3).

### US3: Choose the active version and roll back (P1, APP-03)
As an App Designer, I choose which version new requests use, so that I control when a change reaches users and can undo it.
1. **When** a version is published, **then** the designer is asked "Make this the active version now?", with yes preselected (A3).
2. **When** the designer makes an older version active (rollback), **then** new requests use it, and requests already started keep their own version.
3. **Then** form preview is the only way to try a version before users get it in v0.1 (A9); after activating, rollback is the safety net.

### US4: Running requests stay stable (P1, APP-04, APP-06)
As a requester or approver, I want my request to keep the design it started with, so that it does not change while people work on it.
1. **Given** a request started on v1, **when** v2 becomes active, **then** the request continues on v1.
2. **Given** a rejected request on v3, **when** v2 is made active and the requester reopens the request, **then** it stays on v3 (Q6).
3. **Then** approvers are found when each task is created, using the org data at that moment; tasks that already exist keep their assignee (APP-06).

### US5: Build a form by drag-and-drop (P1, P2 for tabs and grids; FRM-02, FRM-03, FRM-12)
As an App Designer, I build a form by dragging fields and layout items, so that I need no programmer.
1. **When** the designer drags fields and sections onto the form and reorders them, **then** the live preview shows the result, and expressions in it work.
2. **Then** everything the designer can do with the mouse also works with the keyboard (constitution 12.2).
3. **Given** a section set to 2 fields per row with labels on the left, **then** the form shows 2 fields per row with labels on the left; on a narrow screen it shows one field per row (F1).
4. *(M2)* **Then** the designer can also drag tabs and grids onto the form.

### US6: Field properties and data types (P1, FRM-04)
As an App Designer, I give each field a key, a label and a data type, so that the value is stored and checked exactly as the business needs.
1. **When** the designer types a key that is already used in the form (ignoring letter case), starts with a digit, contains a space, or is a reserved word, **then** it is rejected.
2. **When** the designer adds a Number control, **then** only Whole number and Decimal are offered, with Whole number preselected (DT1).
3. **Given** a Date field with default `TODAY()`, **when** the form opens, **then** the field shows today's date (F7).
4. **When** a user enters "abc" in a Whole number field, or 3 decimal places in a Decimal (2) field, **then** the server rejects it, even if the browser did not.
5. **Given** `LoanAmount` was Decimal in v2, **when** the designer publishes v3 where `LoanAmount` is Text, **then** publishing is rejected: "LoanAmount was Decimal in v2. To change its type, create a new field with a new key." (DT2)

### US7: Validation rules (P1, P2 for step rules; FRM-05)
As an App Designer, I add rules that stop bad data, so that approvers can trust what they see.
1. **Given** a mobile field with the ready-made 10-digit pattern, **when** the user enters `98765`, **then** it is rejected in the browser and on the server (F8).
2. **Given** a rule `ToDate >= FromDate` attached to To date, **when** it fails, **then** its message appears under To date; a rule attached to the top of the form shows its message there (CV7).
3. **Given** PAN "Required when `LoanAmount > 50000`", **then** PAN is required only while the expression is true (CV1).
4. **When** an approver uses Reject, Reject (final) or Send back with invalid field values, **then** the action succeeds and their field changes are not saved; only the comment is kept (CV3).
5. **When** someone sends data straight to the server, skipping the browser checks, **then** the server still rejects invalid values, with a message for each field.
6. *(M2)* **Given** a rule limited to the Sanction step, **then** it is checked only at that step; a field ticked "required at this step" is required only at that step (CV2).

### US8: Calculations and show/hide (P1, FRM-06, FRM-07)
As an App Designer, I let the form calculate values and hide what does not apply, so that users fill only what matters.
1. **Given** a calculated field `DAYS_BETWEEN(FromDate, ToDate) + 1`, **then** it updates as the user types, and the server calculates it again and ignores the browser's value (R1).
2. **When** the designer makes two calculated fields depend on each other, **then** the save reports the loop, and the version cannot be published until it is fixed (C10).
3. **Given** Medical certificate shown only when Leave type = Sick, **when** Leave type is Casual, **then** the field is hidden, is not validated, and its value is not saved.

### US9: Upload files (P1, FRM-10)
As a requester, I attach several documents to one field, so that I can send all papers with my request.
1. **Given** a File upload field that allows 5 files, **when** the user adds a 6th, **then** it is rejected (F6).
2. **When** a file is larger than the field's limit or of a type the field does not allow, **then** the server rejects it.
3. **When** the user removes or replaces a file, **then** the old file stays in the request's history (R7).

### US10: Form data is saved safely, step by step (P1, FRM-09, FRM-11, FRM-14)
As an approver, I save only the part of the form my step may edit, so that colleagues working in parallel never overwrite each other.
1. **When** an approver's step saves, **then** only the fields editable at that step change; other values sent with it are ignored (S2).
2. **Given** two parallel steps that edit different fields, **when** both save at the same time, **then** both succeed and both changes are kept.
3. **Given** a step that saves based on an old copy, **when** one of its own fields was changed meanwhile, **then** the save is refused with a clear message; changes to other fields are merged (ADR-0003).
4. **Then** every saved change records the app version used to enter it and increases the record's data version by 1, and other parts of the platform are told about it (FRM-09, FRM-11).

### US11: Tabs (P2, FRM-12)
As an App Designer, I split a long form into tabs, so that users find their way.
1. **Given** a form with tabs, **when** the user submits with errors, **then** each tab with errors shows a red error count and the first such tab opens (T3).
2. **Given** a tab with a show/hide rule, **when** the rule hides it, **then** its fields are hidden, not validated and not saved (T2).
3. **Then** the user moves between tabs with the arrow keys (T4).

### US12: Grid (P2, FRM-13)
As a requester, I enter a list of items (for example co-applicants) as rows of a table, so that one request can hold many similar entries.
1. **When** the user adds, edits and deletes rows, **then** the row count stays within the designer's minimum and maximum (GR2).
2. **Given** a total `SUM(CoApplicants.MonthlyIncome)`, **when** rows change, **then** the total updates in the browser and is the same on the server (GR4).
3. **When** a cell fails its column's rule, **then** the error names the row number (GR5).
4. **Given** a step where the grid is view-only, **then** rows cannot be added, edited or deleted there, also not by calling the server directly (GR6).

### US13: Restart running requests on the active version (P3, APP-05)
As a Platform Admin, I move running requests onto the active version after a design change, so that they follow the corrected form and flow.
1. **When** the admin chooses one or several running requests and enters a reason, **then** each goes back to its requester as "Waiting for resubmission", using the active version's form.
2. **Given** a field whose value cannot be converted cleanly to the new type, **then** it is left empty and the history notes it (A7).
3. **Given** a request whose process does not exist in the active version, **then** it is not restarted, and the result names the request and says why (Q4).
4. **When** a bulk restart finishes, **then** the admin sees each request as restarted, or failed with a reason (Q5).

### US14: Disable an app (P3, APP-07)
As an App Designer, I stop an app from taking new requests without losing anything, so that I can retire a process safely.
1. **When** the designer disables an app, **then** its processes disappear from "Start a request", and its running requests continue normally (A4).
2. **When** the designer enables it again, **then** its processes return; there is no delete.

## 4. Requirements
| ID | Requirement | Milestone |
|---|---|---|
| APP-01 | An App Designer creates an **app** with a code, a name and a description. The code is unique ignoring letter case and cannot change (Q1); it starts with a letter and has only letters, digits, `-` and `_`, up to 20 characters (C8). Creating an app also creates its empty first draft (C7). Any App Designer can edit any app (A2). Forms and processes belong to **one app only**; form and process keys are unique within the app (Q2) | M1 |
| APP-02 | **Versions:** Draft → Published. One draft at a time (A1). A new draft is a copy of a chosen published version. Publishing needs a **note** (A6), checks all forms and processes (form checks here; expressions in 003; processes in 005), and is all-or-nothing; each error points to its form, field or node. Versions are numbered 1, 2, 3 in publish order (A6). A published version never changes and cannot be discarded. A draft can be discarded (soft delete, Q3); an app with no published version and no draft can get a new empty draft (C7). A draft with design errors (broken references, loops, expression errors) **is saved** and lists every error; **publishing is refused** until all are fixed (C10, same rule as 003 C2). A save based on an old copy of a form or process is refused with who changed it and when (A8) | M1 |
| APP-03 | **Active version:** one published version is active; new requests use it. After publishing, the designer is asked "Make this the active version now?" (default yes, A3). Making an older version active is a **rollback**; requests already started keep their version. In v0.1, form preview is the only way to try a version first (A9) | M1 |
| APP-04 | **Running requests** stay on the version they started with, unless an admin restarts them (APP-05). A rollback does not change the version of a reopened request (Q6) | M1 |
| APP-05 | **Restart on the active version** (Platform Admin): for one or several running requests, **only onto the active version** (A5), with a required reason. Open tasks are cancelled; the request goes back to the requester at the start, using the active version's form. A request already on the active version is not restarted ("already on the active version", C12). Values are copied by field key; type changes follow the conversion rules in 4.3 (A7, C4, C5); new fields start empty or with their default; removed fields are no longer shown but their old values stay in the history. The requester reviews, fills and resubmits; the request then follows the new version's flow from the first step. Request number and history are kept; the history shows who restarted it, when, why and onto which version. Until resubmitted, the status is **Waiting for resubmission**. A request whose process is not in the active version is not restarted (Q4). Bulk restart runs in the background, each request on its own, and ends with a result list (Q5). Grid rows are copied too, columns matched by key with the same conversion rules (GR8). The restart email is specified in 006 (NTF-06) | M3 |
| APP-06 | Approvers are found **when each task is created**, using the org data at that moment; existing tasks keep their assignee (rule owned by 002, ORG-06-AC-x2; task creation in 005) | M1 |
| APP-07 | An app can be **disabled**: its processes disappear from "Start a request", no new requests start, running requests continue. Disabling stops only new requests: designers can still edit, publish and change the active version, and requesters can still reopen rejected requests (C11). It can be enabled again. Apps are never deleted (A4) | M3 |
| FRM-02 | **Drag-and-drop form designer** with live preview in which expressions work; reordering; everything works with the keyboard (constitution 12.2). **M1:** fields, sections, labels/help text. **M2:** tabs and grids | M1 / M2 |
| FRM-03 | **Field types:** Text, Text area, Number, Currency, Date, Dropdown, Radio buttons, Checkboxes, Yes/No, Email, File upload (multiple files), User picker, Calculated (read-only), and **Grid** (FRM-13). **Layout items:** Tabs, Section (with title), Label/help text. Option lists are fixed lists typed by the designer, each option with a stored value and a label (F4). One currency per installation, shown in the user's locale format (F3). User picker: any active user, searched by name (F9). Yes/No starts blank; "required" means Yes or No must be chosen (R5). Email checks for a basic address format, in the browser and on the server (C13). Labels and help text are entered in one language in v0.1 (R4). **M1:** all fields and layout items except Grid and Tabs. **M2:** Grid and Tabs | M1 / M2 |
| FRM-04 | **Field properties:** label, key, **data type**, help text, default value, required, read-only. Keys are typed by hand (F2): unique in the form ignoring case, start with a letter, only letters, digits and `_`, no reserved words (003, E1). Renaming a key does not update expressions; the save check reports every broken reference (F2). Default value: a fixed value or an expression, evaluated once when the form opens (F7). Every control has an explicit data type from its allowed list, with a default (DT1, section 4.1). The server rejects values that do not fit the data type. A key keeps **one type family in all versions**; a family change is blocked at publish (DT2, section 4.2) | M1 |
| FRM-05 | **Validation**, in the levels of section 4.4: field settings (required, "Required when" (CV1), length, min/max value, decimals, data type, ready-made or custom pattern (F8)); expression rules across fields, each with its own message shown under one chosen field or at the top of the form (CV7). Field settings run first; an expression rule runs only when its fields passed their own checks (no double errors); a rule whose result is blank passes (003, EXP-07). Rules run in the browser (when the user leaves a field, and on Submit, C13) and again on the server; the server decides. They run on **Submit, Resubmit and Approve** only; on Approve, a rule runs only at steps where its field is editable (a top-of-form rule: where any field it uses is editable) (C9); on Reject, Reject (final) and Send back nothing is validated and field changes are not saved (CV3). **M1:** all of this. **M2:** grid rules (with FRM-13) and **step rules**: a rule can be limited to chosen workflow steps (default: every step where its field is editable), and a field can be "required at this step" (CV2; the tick is set in the process designer, 005) | M1 / M2 |
| FRM-06 | **Calculated fields** use the expression language (003). A calculated field may use other calculated fields; loops are reported when the design is saved and block publishing (C10). The formula's result must fit the field's chosen data type. The server recalculates and ignores the browser's value (R1) | M1 |
| FRM-07 | **Show/hide rules:** a field or a section is visible only while an expression is true. Hidden fields are not validated and their values are not saved. **M2:** the same for fields on a hidden tab (T2) | M1 / M2 |
| FRM-08 | Forms are versioned only as part of their app (APP-02); there are no separate form versions | M1 |
| FRM-09 | Form data is saved with the app version used to enter it | M1 |
| FRM-10 | **File upload:** multiple files per field; per field the designer sets the maximum number of files (default 5), the maximum size per file (default 10 MB) and the allowed file types (F6). The server checks all three. A removed or replaced file is never hard-deleted; it stays in the history (R7) | M1 |
| FRM-11 | Every change to form data is **announced to the rest of the platform** in the same all-or-nothing step as the save. The announcement carries the record, the form, the app version, a **data version** that goes up by 1 with each change, and a **full copy** of the values (ADR-0002). It prepares a later read copy for lists and search | M1 |
| FRM-12 | **Layout:** per section, the designer chooses 1–4 fields per row and labels left or above; narrow screens fall back to one field per row (F1). **M2: tabs**: a form may be divided into tabs, each holding sections, not nested (T1); a tab may have a show/hide rule (T2); after Submit with errors, each tab with errors shows a red error count and the first such tab opens (T3); arrow keys move between tabs (T4); a tab with no visible fields for the current step is hidden (T5) | M1 / M2 |
| FRM-13 | **Grid** (G1): a table inside the form where the user adds, edits and deletes rows. Column types: Text, Number, Currency, Date, Dropdown, Yes/No, Calculated (GR1). Minimum and maximum rows (default maximum 100, GR2). A calculated column may use other columns of the same row and form fields (GR3). Form-level `SUM(Grid.Column)` and `COUNT(Grid)` (GR4; functions in 003). Column validation runs per row, and errors name the row number (GR5); grid-level rules check the grid as a whole (level 3). Per workflow step a grid is hidden, **view-only** (the v0.1 "Datalist view", G3) or editable; when editable, the designer also chooses whether rows can be added and deleted (GR6; set in the process designer, 005). Each row keeps a stable identity; the history records rows added, rows removed and cells changed (GR7). Grid and column keys are typed by hand; column keys are unique within their grid (GR9) | M2 |
| FRM-14 | **Step-wise saving** (S2): each workflow step saves only the fields (and grids) it may edit; anything else sent is ignored, and the rest of the record is kept. Parallel branches never share an editable field (publish check owned by 005, WF-04). A save based on an old copy is refused only if one of the step's own fields changed meanwhile; otherwise the changes are merged (ADR-0003) | M1 |
| NFR-09 | **Design format version:** every saved app, form and process carries its format version. When the platform is upgraded, older designs are upgraded automatically and keep working (constitution 8.7) | M1 |

### 4.1 Data types (DT1)
The **control** is how a field looks; the **data type** is what its value is. The designer picks a control; a
default data type is preselected, and the designer may change it within the allowed list.

| Data type | Settings | Used in expressions as |
|---|---|---|
| **Text** | Max length 1–4,000, or Unlimited | text |
| **Whole number** | none | number |
| **Decimal** | Decimal places 0–10 (C2) | number |
| **Date** | none | date |
| **Yes/No** | none | true/false |
| *User* (fixed for User picker) | none | text (user ID) |
| *File* (fixed for File upload) | none | only `ISBLANK` |

| Control | Allowed data types | Default |
|---|---|---|
| Text box | Text, Whole number, Decimal | Text (255) |
| Text area | Text | Text (Unlimited) (C1) |
| Number | Whole number, Decimal | Whole number |
| Currency | Decimal | Decimal (2) |
| Date | Date | Date |
| Dropdown, Radio | Text, Whole number | Text (100) |
| Checkboxes | Text, Whole number (a list) | Text (100) |
| Yes/No | Yes/No | Yes/No |
| Email | Text | Text (320) |
| User picker | *User* | *User* |
| File upload | *File* | *File* |
| Calculated | Any; the expression check verifies that the formula's result fits the chosen type | Decimal (2) |
| Grid column | Same rules as the control used in the column | |

Stored values of Dropdown, Radio and Checkboxes options must match the data type. Without a designer limit,
a Text box allows up to 255 characters and a Text area is unlimited (C1). Every Decimal field allows 0–10
decimal places; Currency defaults to 2 and the designer may change it (C2).

### 4.2 Type changes between versions (DT2)
- Within the same family, changes are always allowed: longer or shorter text, Whole number → Decimal, Decimal → Whole number (C3), more or fewer decimal places.
- A change to a different family (for example Decimal → Text) is **rejected by the publish check**, naming the field: "LoanAmount was Decimal in v2. To change its type, create a new field with a new key."
- Every key keeps one type family forever, so restart copying (APP-05) only ever converts within a family.

### 4.3 Clean type conversion on restart (A7, M3)
| From → To | Converts when |
|---|---|
| Number ↔ Currency | Always |
| Number/Currency → Text | Always (plain number text) |
| Text → Number/Currency | The text is a valid number |
| Text → Date | The text is a valid ISO date |
| Dropdown/Radio → Dropdown/Radio | The stored value exists in the new option list |
| Checkboxes → Checkboxes | The stored values that still exist in the new option list are kept; removed ones are noted in the history (C5) |
| Anything else | Not converted: left empty, noted in the history |

- A value that does not fit the new field's limits within the same family (text longer than the new limit, more decimals than allowed, including Decimal → Whole number with a fraction) is **not converted**: left empty and noted in the history (C3, C4).
- Some rows of this table cannot happen any more because of DT2. The table stays as decided; only the rows that can happen are built and tested (C6).

### 4.4 Validation levels (CV1–CV7)
| Level | Checks | Example | Set up by | When |
|---|---|---|---|---|
| 1. Field settings | required, "Required when" (CV1), length, min/max, decimals, data type, pattern | Mobile = 10 digits | Designer (ticks) | M1 |
| 2. Expression rules | any condition across fields, own message | `LoanAmount <= MonthlyIncome * 60` | Designer (formula) | M1 |
| 3. Grid rules | per row, and the grid as a whole | `Qty > 0`; `COUNT(CoApplicants) <= 3` | Designer | M2 |
| 4. Step rules | only at chosen workflow steps; "required at this step" (CV2) | Sanction amount required at Sanction | Designer (process) | M2 |

Levels 1–4 run in the browser and **again on the server**, with the same results (003, EXP-05). Levels 5–7
(lookups, outside systems, code plugins) are out of scope (section 9).

## 5. Acceptance criteria
Every criterion gets at least one automated test whose name contains its ID (constitution 1.4), unless marked
*manual*. Criteria marked *(M2)* or *(M3)* belong to that milestone.

| ID | Criterion |
|---|---|
| APP-01-AC1 | A designer creates an app with a unique code, a name and a description; a duplicate code is rejected; the code cannot change later (Q1) |
| APP-01-AC2 | Any user with App Designer can open and edit any app; a user without it is refused by the server (A2, NFR-01) |
| APP-01-AC3 | A second form or process with a key already used in the same app is rejected; the same key in another app is allowed (Q2) |
| APP-01-AC4 | An app code that does not start with a letter, contains other characters than letters, digits, `-` and `_`, is longer than 20 characters, or equals an existing code ignoring letter case is rejected (C8) |
| APP-02-AC1 | An app has at most one draft (A1); "New draft from version…" is refused while a draft exists |
| APP-02-AC2 | Publishing requires a note (A6) and checks all forms and processes; if any check fails, nothing is published and every error points to its form, field or node |
| APP-02-AC3 | A published version cannot be edited or discarded; versions are numbered 1, 2, 3 in publish order |
| APP-02-AC4 | A second save of the same form or process based on an old copy is rejected with who changed it and when (A8) |
| APP-02-AC5 | A draft can be discarded; it is only marked as deleted (Q3, NFR-08) |
| APP-02-AC6 | A new draft from version N contains the same forms and processes as version N; changing the draft leaves version N unchanged |
| APP-02-AC7 | A new app has an empty first draft; after that draft is discarded (and no version was published), the designer can create a new empty draft (C7) |
| APP-02-AC8 | A draft with design errors (e.g. a broken reference after a key rename) is saved and lists every error; publishing it is refused until all are fixed (C10) |
| APP-03-AC1 | After publishing, the designer can make the new version active in the same step; yes is preselected (A3) |
| APP-03-AC2 | Making an older version active (rollback) makes new requests use it; requests already started keep their version |
| APP-04-AC1 | A request started on v1 continues on v1 after v2 becomes active |
| APP-04-AC2 | A request reopened after a rollback keeps its own version (Q6) |
| APP-05-AC1 | *(M3)* The admin can restart one or several running requests only onto the **active** version (A5), with a required reason; a user without Platform Admin is refused |
| APP-05-AC2 | *(M3)* Values are copied by field key; type changes follow the conversion table (4.3); values that do not convert stay empty and are noted in the history (A7) |
| APP-05-AC3 | *(M3)* A request whose process does not exist in the active version is not restarted, and the result says why (Q4) |
| APP-05-AC4 | *(M3)* Bulk restart reports each request as restarted or failed with a reason; one failure does not stop the others (Q5) |
| APP-05-AC5 | *(M3)* A restarted request's open tasks are cancelled; it goes to its requester as "Waiting for resubmission" with the active version's form; new fields are empty or show their default; removed fields are not shown |
| APP-05-AC6 | *(M3)* After resubmission the request follows the active version's flow from the first step; it keeps its number and history, and the history shows who restarted it, when, why, onto which version, and the old values of removed fields |
| APP-05-AC7 | *(M3)* Grid rows are copied on restart; columns are matched by key using the same conversion rules (GR8) |
| APP-05-AC8 | *(M3)* Restarting a request that is already on the active version is refused with "already on the active version" (C12) |
| APP-05-AC9 | *(M3)* A text value longer than the new limit, or a number with more decimals than the new field allows (also 12.5 into a Whole number field), is left empty and noted in the history (C3, C4) |
| APP-05-AC10 | *(M3)* A Checkboxes value PAN, AADHAAR, VOTERID, restarted onto a version without VOTERID, keeps PAN and AADHAAR; the history notes that VOTERID was removed (C5) |
| APP-06-AC1 | A task created after an org change uses the new org data; a task created before keeps its assignee (same rule as ORG-06-AC-x2 in 002; one test may carry both IDs) |
| APP-07-AC1 | *(M3)* A disabled app's processes disappear from "Start a request" and no new request can start, also not by calling the server directly; its running requests continue normally |
| APP-07-AC2 | *(M3)* A disabled app can be enabled again; there is no way to delete an app |
| APP-07-AC3 | *(M3)* In a disabled app, designers can still edit, publish and change the active version, and a requester can reopen a rejected request (C11) |
| FRM-02-AC1 | The designer drags fields and sections onto the form, reorders them, and sees a live preview where expressions work; *(M2)* the same for tabs and grids |
| FRM-02-AC2 | The designer's drag-and-drop also works with the keyboard (constitution 12.2) |
| FRM-03-AC1 | Every field type listed for M1 in FRM-03 can be added, configured and filled; *(M2)* the same for Grid and Tabs |
| FRM-03-AC2 | Dropdown, Radio and Checkboxes show the designer's fixed list; each option has a stored value and a label; a stored value that does not fit the data type is rejected (F4) |
| FRM-03-AC3 | The User picker finds active users by name; inactive users cannot be chosen (F9) |
| FRM-03-AC4 | A Yes/No field starts blank; when required, the user must choose Yes or No (R5) |
| FRM-03-AC5 | Currency values are shown in the installation's one currency, in the user's locale format (F3) |
| FRM-03-AC6 | An Email field rejects an address without a basic valid format (e.g. `ravi@gmail`, `ravi.example.com`) in the browser and on the server; `ravi@example.com` is accepted (C13) |
| FRM-04-AC1 | A field key must be unique in the form (ignoring case), start with a letter and contain only letters, digits and `_`; reserved words are rejected (E1) |
| FRM-04-AC2 | Changing a key that expressions use makes the save check report each broken expression (F2) |
| FRM-04-AC3 | A default expression such as `TODAY()` fills the field when the form opens (F7) |
| FRM-04-AC4 | Each control offers only its allowed data types, with the default preselected (DT1) |
| FRM-04-AC5 | The server rejects a value that does not fit the data type (e.g. "abc" for Whole number, 3 decimals for Decimal (2)) |
| FRM-04-AC6 | Publishing a version where an existing key changes type family is rejected with a message naming the field (DT2); a change within the family is accepted |
| FRM-04-AC7 | A field marked read-only shows its value, and the user cannot change it |
| FRM-04-AC8 | Without a designer limit, a Text box allows 255 characters, a Text area is unlimited, and the other defaults of section 4.1 apply (C1) |
| FRM-04-AC9 | A Decimal field accepts 0–10 decimal places as its setting; a Currency field starts with 2 and the designer can change it (C2). Publishing a change from Decimal to Whole number for an existing key is accepted (C3) |
| FRM-05-AC1 | A ready-made pattern (e.g. 10-digit mobile) rejects `98765` in the browser and on the server (F8); a custom pattern works the same way |
| FRM-05-AC2 | A request that skips browser checks (direct call to the server) is still rejected by the server with field-level messages |
| FRM-05-AC3 | A "Required when" field is required only while its expression is true (CV1) |
| FRM-05-AC4 | *(M2)* A rule limited to the Sanction step is not checked at other steps; a field ticked "required at this step" is required only there (CV2) |
| FRM-05-AC5 | Reject and Send back succeed with invalid field values, and those field changes are not saved (CV3) |
| FRM-05-AC6 | A rule's message appears under its chosen field, or at the top of the form, as configured (CV7) |
| FRM-05-AC7 | Min/max length and min/max value are enforced in the browser and on the server |
| FRM-05-AC8 | When a field fails its own settings, an expression rule that uses it is not checked, so the user sees one error, not two |
| FRM-05-AC9 | On Approve, a rule runs only at steps where its field is editable (a top-of-form rule: where any field it uses is editable); an approver who cannot edit `FromDate` or `ToDate` is not blocked by `ToDate >= FromDate` (C9) |
| FRM-05-AC10 | Browser checks show a field's error when the user leaves the field and on Submit, not while the user is still typing in it (C13) |
| FRM-06-AC1 | The server's value for a calculated field wins over the browser's (R1) |
| FRM-06-AC2 | A calculated field may use another calculated field; a loop between calculated fields is reported when the design is saved and blocks publishing (C10) |
| FRM-06-AC3 | A formula whose result does not fit the calculated field's data type is reported by the save check |
| FRM-07-AC1 | A hidden field is not validated and its value is not saved; *(M2)* the same for a field on a hidden tab |
| FRM-07-AC2 | A section hidden by its rule hides all its fields, with the same effect as FRM-07-AC1 |
| FRM-08-AC1 | A form has no version of its own: publishing the app freezes the form, and a form in a published version cannot be edited |
| FRM-09-AC1 | Saved form data records the app version used to enter it; a request on v1 still shows the v1 form after v2 becomes active |
| FRM-10-AC1 | A field allowing 5 files rejects a 6th; a file over the size limit or of a type not allowed is rejected on the server (F6) |
| FRM-10-AC2 | A new File upload field allows 5 files of up to 10 MB each until the designer changes it (F6) |
| FRM-10-AC3 | A removed or replaced file stays in the request's history and is never hard-deleted (R7, NFR-08) |
| FRM-11-AC1 | Every saved change to form data increases its data version by exactly 1 and announces the record, form, app version, data version and a full copy of the values |
| FRM-11-AC2 | A save that fails or is cancelled announces nothing; a successful save is always announced |
| FRM-12-AC1 | A section shows 1–4 fields per row with labels left or above, as configured; on a narrow screen it shows one per row (F1) |
| FRM-12-AC2 | *(M2)* Tabs with errors show an error count after Submit, and the first one opens (T3) |
| FRM-12-AC3 | *(M2)* A tab hidden by its rule hides its fields; they are not validated and not saved (T2) |
| FRM-12-AC4 | *(M2)* A tab with no visible fields for the current step is not shown (T5) |
| FRM-12-AC5 | *(M2)* Arrow keys move between tabs (T4) |
| FRM-13-AC1 | *(M2)* The user adds, edits and deletes rows within the minimum and maximum row counts (GR2) |
| FRM-13-AC2 | *(M2)* `SUM(CoApplicants.MonthlyIncome)` updates as rows change, in the browser and on the server; blank cells are ignored and an empty grid sums to 0 (GR4) |
| FRM-13-AC3 | *(M2)* A row error names its row number (GR5) |
| FRM-13-AC4 | *(M2)* At a step where the grid is view-only, rows cannot be added, edited or deleted, also not through a direct call to the server (GR6) |
| FRM-13-AC5 | *(M2)* An editable grid allows adding or deleting rows only if the designer allowed it for that step (GR6) |
| FRM-13-AC6 | *(M2)* A calculated column such as `Qty * Rate` uses the values of its own row (GR3) |
| FRM-13-AC7 | *(M2)* A grid-level rule such as `COUNT(CoApplicants) <= 3` blocks Submit with its message when it fails |
| FRM-13-AC8 | *(M2)* The history shows rows added, rows removed and cells changed (GR7) |
| FRM-13-AC9 | *(M2)* A column key used twice in one grid is rejected (GR9) |
| FRM-14-AC1 | A step's save changes only the fields editable at that step; other submitted values are ignored (S2) |
| FRM-14-AC2 | Two parallel steps editing different fields at the same time both succeed, and both changes are kept |
| FRM-14-AC3 | Publishing a process where two branches of one parallel split can edit the same field is rejected (same check as WF-04-AC2 in 005; one test may carry both IDs) |
| FRM-14-AC4 | A save based on an old copy is refused with a clear message when one of the step's own fields changed meanwhile; when only other fields changed, it is merged |
| NFR-09-AC1 | Every saved app, form and process records its format version; a design saved in an older format version is upgraded automatically and works as before (constitution 8.7) |

## 6. Edge cases
- Two designers edit **different** forms of the same draft: both saves succeed; only the same form or process conflicts (A8).
- A designer renames a key used by expressions: the draft lists every broken reference, and publishing is blocked until they are fixed (F2).
- A key was Decimal in v2, removed in v3 and added back as Text in v4: publishing v4 is rejected, because a key keeps one family forever (DT2).
- The designer answers "no" to "Make this the active version now?" on the app's first publish: the app has no active version, so its processes do not appear in "Start a request" (006, INB-01).
- Versions are numbered in publish order, so a discarded draft leaves no gap in the numbers.
- A "Required when" expression gives blank (for example its input is empty): the field is not required, because a blank validation rule passes (003, EXP-07).
- A required field is hidden by a show/hide rule: it is not required while hidden (FRM-07).
- An approver sends back a request after typing invalid values: the send back succeeds and the typed values are discarded (CV3).
- A user sends extra values for fields their step may not edit, by calling the server directly: those values are ignored (FRM-14).
- A grid has a minimum of 1 row: deleting the last row is refused (GR2).
- A bulk restart includes one request whose process no longer exists: that one fails with a reason; the others are restarted (Q4, Q5).
- A field's new type in the active version cannot take the old value: it is left empty, the history notes it, and the requester fills it before resubmitting (A7).
- A disabled app still has tasks in people's inboxes: they are worked on normally until the requests finish (A4).

## 7. Non-functional requirements that apply
From the [v0.1 scope](../v0.1-scope.md) section 6: NFR-01 (designer screens need App Designer; restart needs Platform Admin; the server checks every action), NFR-03 (no lost announcements of form data changes; handling one twice is safe), NFR-04 (SQL Server 2019 or later, no cloud-only services, Windows without containers), NFR-05 (date-only fields have no time part), NFR-06 (latest Chrome, Edge, Firefox; desktop first, usable on a tablet), NFR-07 (no form values in logs, errors or telemetry), NFR-08 (drafts, files and form data are never hard-deleted), NFR-09 (design format version, see section 4), NFR-10 (screen text from translation files; numbers, dates and currency by locale), NFR-11 (keyboard and labels, including the designer), NFR-12 (coverage).

## 8. Key entities (business view, not database design)
| Entity | Meaning |
|---|---|
| **App** | A container for one business solution: code (never changes), name, description, status (Enabled/Disabled), active version |
| **App version** | A numbered snapshot of all the app's forms and processes: number, status (Draft/Published), publish note, published by and when, format version. At most one draft per app; one published version is active |
| **Form** | A data-entry screen inside an app version: key, name, tabs, sections, fields, grids, rules |
| **Field** | One input on a form: control, key, label, data type, help text, default, required, read-only, rules |
| **Data type** | What a field's value is: Text, Whole number, Decimal, Date, Yes/No (plus User and File, fixed by their controls); a key keeps one type family forever |
| **Option list** | The fixed list of a Dropdown, Radio or Checkboxes field: stored value and label per option |
| **Section / Tab** | Layout: a section has a title, 1–4 fields per row and a label position; a tab (M2) groups sections and may have a show/hide rule |
| **Rule** | A validation, "Required when", calculation or show/hide expression; a validation rule has a message and a place (under a field or at the top) and (M2) may be limited to steps |
| **Grid** | (M2) A table in a form: columns (each like a field), rows with a stable identity, minimum and maximum rows |
| **Form record** | The values entered for one request: app version, data version (goes up by 1 per change), values, files |
| **Uploaded file** | A file attached to a File upload field; removed or replaced files stay in the history |
| **Restart** | (M3) An admin's action that moves running requests onto the active version: reason, target version, result per request |

## 9. Out of scope
| Item | When |
|---|---|
| Per-app permissions for designers | Later (A2) |
| Compare versions screen | Later (A10) |
| Test environment with test → live promotion; app export and import | v0.2 (A9) |
| Restart onto a version other than the active one; moving a running request into the middle of another version | Not planned (A5, scope Q10) |
| Withdrawing a request, also one waiting for resubmission | v0.2 (WF-10) |
| Requester saves a draft request | Later (F5) |
| Option lists from org data or other records; cascading dropdowns | Later (F4, R6) |
| Sub-forms and multi-page (wizard) forms | Later (R6) |
| Date-and-time data type | Later (DT3) |
| Non-blocking warnings | Later (CV4) |
| "Must be unique" and lookup checks (validation level 5); Searchable fields | v0.2 (CV5, S3) |
| Checks against outside systems (level 6); rows from an outside system in a grid | v0.3 (G3) |
| Code plugins for validation (level 7) and plugin field types | v0.5 |
| "Datalist view" control listing other records | v0.2 (G3) |
| Grid columns of type File upload, User picker or Checkboxes; a grid inside a grid | Later (GR1) |
| Multi-language labels and help text | Later (R4) |
| Sensitive-field masking | Later (constitution 11.3) |
| A read copy of form data for lists and search; reporting tables per form | Later (ADR-0002, ADR-0003) |
| Deleting an app | Never (A4) |

## 10. Clarifications (answered by the owner, 2026-10-21)
| # | Question | Answer |
|---|---|---|
| C1 | Text area default limit (R3: 4,000; DT1: Unlimited) | **Unlimited**; the designer can set a limit |
| C2 | Decimal places (R3: 0–4; DT1: 0–10); can Currency differ from 2? | **0–10 for every Decimal field; Currency defaults to 2 and may be changed** |
| C3 | Is Decimal → Whole number within the number family? | **Yes, allowed at publish**; on restart a value with decimals is left empty and noted (A7) |
| C4 | Restart of values that no longer fit (too long, too many decimals) | **Left empty and noted in the history** (A7 as written) |
| C5 | Restart of a Checkboxes field whose options changed | **Keep the options that still exist; note the removed ones** |
| C6 | A7 conversion rows made impossible by DT2 | **Keep the table as decided; build and test only the rows that can happen** |
| C7 | First draft of a new app | **Created with the app**; an app with no published version and no draft can get a new empty draft |
| C8 | App code format | **Starts with a letter; letters, digits, `-`, `_`; up to 20 characters; unique ignoring letter case** |
| C9 | Which steps run a rule in M1 | **Submit and Resubmit; on Approve only at steps where the rule's field is editable** (CV2 default) |
| C10 | Saving a draft with design errors | **Saved with an error list; publishing blocked** (same answer as 003 C2) |
| C11 | What still works in a disabled app | **Designers can edit, publish and change the active version; requesters can reopen rejected requests** |
| C12 | Restart of a request already on the active version | **Refused** ("already on the active version") |
| C13 | Email format check and when browser checks run | **Basic address-format check in the browser and on the server; browser checks when leaving a field and on Submit** (R2) |

## 11. Spec quality checklist (Claude checks before asking for approval)
- [x] Every requirement has an ID, a milestone and at least one acceptance criterion
- [x] No technology choices in the spec (only customer constraints)
- [x] Every acceptance criterion is testable; none needs a manual check
- [x] Out-of-scope items are listed so nobody builds them by mistake
- [x] All clarifications answered (C1–C13, 2026-10-21)
- [x] Approved by the owner (2026-10-21)
