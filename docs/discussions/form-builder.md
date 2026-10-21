# Module discussion: FRM (Form builder)

| | |
|---|---|
| **Status** | Decided by the owner (F1–F10, G1, G3, S1–S3, tabs, DT1–DT3, CV1–CV7), 2026-10-16. Details R1–R7, GR1–GR9 and T1–T5 proposed by Claude |
| **Feeds** | `/speckit-specify 004-apps-and-form-builder` (sections 1–7) and `/speckit-plan` (section 8) |
| **Scope IDs** | FRM-02 to FRM-14 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) |
| **Related** | [ADR-0002](../decisions/0002-form-data-original-and-read-copy.md), [ADR-0003](../decisions/0003-form-data-storage-layers.md), [EXP](expression-language.md), [APP](apps-and-versions.md) |
| **Owning services** | Design (form definitions), Runtime (form data) |

## What this file is
The result of the detailed discussion of the FRM module. Sections 1–7 are **what and why**, the input for
`spec.md`. Section 8 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
Designers build data-entry screens by drag-and-drop, with no code. One form definition is used three ways:
the requester **fills** it, approvers **see or edit** the parts allowed at their step (WF-03), and the server
**checks** it again before saving (the server decides, FRM-05).

## 2. Building blocks
| Kind | Items |
|---|---|
| **Fields** | Text, Text area, Number, Currency, Date, Dropdown, Radio, Checkboxes, Yes/No, Email, File upload (multiple files), User picker, Calculated |
| **Layout** | **Tabs**, Section (title; 1–4 fields per row; label left or above), Label/help text |
| **Grid** | A table inside the form; the user adds, edits and deletes rows (section 5) |

### Example (RS-2 style, with tabs and a grid)
```
[ Applicant ] [ Income ] [ Documents ]                       ← tabs
┌─ Applicant details ──────────────────────────────────────┐ ← section, 2 fields per row, label left
│ Applicant name * [............]   Mobile *  [..........] │
│ Email *          [............]   PAN *     [..........] │
└──────────────────────────────────────────────────────────┘
┌─ Co-applicants ──────────────────────────────────────────┐ ← grid
│ # │ Name        │ Relation  │ Monthly income │           │
│ 1 │ Lakshmi     │ Spouse    │ 45,000.00      │ ✎ 🗑      │
│ 2 │ Kumar       │ Father    │ 20,000.00      │ ✎ 🗑      │
│ [+ Add row]                     Total: 65,000.00          │ ← SUM(CoApplicants.MonthlyIncome)
└──────────────────────────────────────────────────────────┘
```

## 3. Owner's decisions
| # | Question | Decision |
|---|---|---|
| F1 | Layout | **The designer chooses per section: 1–4 fields per row, label left or above.** Narrow screens fall back to one field per row |
| F2 | Field keys | **Typed by hand.** A key is the name inside the record's JSON, not a database column (ADR-0003). Renaming a key in a draft does not update expressions; the save check (EXP-04) reports every broken reference |
| F3 | Currency | **One currency per installation** (a setting), shown in the user's locale format |
| F4 | Option lists | **A fixed list typed by the designer**, each option with a stored value and a label. Lists from org data or other records come later |
| F5 | Requester saves a draft request | **Later** |
| F6 | Files per File upload field | **Multiple files**: the designer sets the maximum number (default 5), maximum size per file (default 10 MB) and allowed types |
| F7 | Default values | **A fixed value or an expression** (e.g. `TODAY()`), evaluated once when the form opens |
| F8 | Pattern validation | **Ready-made patterns** (10-digit mobile, PIN code, PAN, IFSC, digits only) **plus a custom pattern** |
| F9 | User picker | **Any active user, searched by name** |
| F10 | Drag-and-drop library | **dnd-kit** (MIT licence), **approved** under constitution 1.6 |
| G1 | Grid control | **In v0.1.** To make room, ORG-07, INB-06, WF-10 and NTF-03 move to v0.2 |
| G3 | "Datalist view" (read-only rows) | **v0.1:** a grid shown **view-only** at a step (rows from the same request). **v0.2:** a Datalist view control listing **other records** (needs Searchable fields). **v0.3:** rows from an **outside system** (connectors) |
| S1 | Form data storage | **Three layers** ([ADR-0003](../decisions/0003-form-data-storage-layers.md)): JSON original, search index, reporting tables |
| S2 | Step-wise saving | **Yes, v0.1.** Each step saves only its editable fields; parallel branches never share an editable field |
| S3 | Searchable fields | **v0.2**, with datalists |
| Tabs | Tab-like controls (owner's request) | **In v0.1** as a layout item (about half a day; see section 6) |
| DT1 | Data type of a field | **Every control has an explicit Data type property**, chosen when the control is created (section 4a) |
| DT2 | Different type family for an existing key in a later version | **Blocked at publish.** To change the type, the designer creates a new field with a new key. Every key keeps one type family forever |
| DT3 | Date-and-time data type | **Later** (no v0.1 control needs it; needs time-zone rules) |
| CV1 | Conditional required | **"Required when" expression in v0.1** (e.g. PAN required when `LoanAmount > 50000`) |
| CV2 | Rules per workflow step | Each rule can be **limited to chosen steps** (default: every step where its field is editable). Per-step field permissions (WF-03) get a **"required at this step"** tick |
| CV3 | Validation by action | Rules run on **Submit, Resubmit and Approve**. On **Reject, Reject (final) and Send back** nothing is validated and **field changes are not saved**; only the comment rule applies |
| CV4 | Non-blocking warnings | **Later** |
| CV5 | Uniqueness and lookup checks | **v0.2**: "Must be unique" (among open requests, or all records) on Searchable fields |
| CV6 | Age rules | Add **`YEARS_BETWEEN`** to v0.1 (EXP-03) |
| CV7 | Where messages appear | The designer attaches each rule **to one field** (message under it, counted in the tab's error badge) **or to the top of the form** |

## 4. Details proposed by Claude (no objection from the owner)
| # | Detail |
|---|---|
| R1 | The server **recalculates** calculated fields and ignores the browser's values for them |
| R2 | Browser validation runs when the user leaves a field and on Submit; the server always validates again |
| R3 | Defaults: Text up to 255 characters, Text area up to 4,000 (unless the designer sets a limit); Currency shows 2 decimals; Number decimals set per field (0–4) |
| R4 | Labels and help text are entered in **one language** in v0.1; multi-language labels come later (UI text still comes from translation files, constitution 12) |
| R5 | A Yes/No field starts blank; "required" means the user must choose Yes or No |
| R6 | Cascading dropdowns, sub-forms and multi-page (wizard) forms come **later** |
| R7 | Uploaded files are never hard-deleted; a removed or replaced file stays in the history (constitution 8.6) |

## 4a. Data types (DT1, DT2)
The **control** is how a field looks; the **data type** is what its value is. The designer picks a control,
a default data type is preselected, and the designer may change it within the allowed list.

| Data type | Settings | Expression type |
|---|---|---|
| **Text** | Max length 1–4,000, or Unlimited | text |
| **Whole number** | none | number |
| **Decimal** | Decimal places 0–10 | number |
| **Date** | none | date |
| **Yes/No** | none | true/false |
| *User* (fixed for User picker) | none | text (user ID) |
| *File* (fixed for File upload) | none | only `ISBLANK` |

| Control | Allowed data types | Default |
|---|---|---|
| Text box | Text, Whole number, Decimal | Text (255) |
| Text area | Text | Text (Unlimited) |
| Number | Whole number, Decimal | Whole number |
| Currency | Decimal | Decimal (2) |
| Date | Date | Date |
| Dropdown, Radio | Text, Whole number | Text (100) |
| Checkboxes | Text, Whole number (a list) | Text (100) |
| Yes/No | Yes/No | Yes/No |
| Email | Text | Text (320) |
| User picker | *User* | *User* |
| File upload | *File* | *File* |
| Calculated | Any; the expression checker verifies the formula's result fits the chosen type | Decimal (2) |
| Grid column | Same rules as the control used in the column | |

**Uses of the declared type:** exact server validation (e.g. "abc" in a Whole number, 3 decimals in a
2-decimal field); the expression checker (EXP-04); the search index (ADR-0003 Layer 2); the reporting
column type (ADR-0003 Layer 3). Dropdown/Radio/Checkbox stored values must match the data type.

**Type changes between versions (DT2):** within the same family (longer or shorter text, Whole number →
Decimal, more or fewer decimal places) always allowed. A different family (e.g. Decimal → Text) is
**rejected by the publish check**: "LoanAmount was Decimal in v2. To change its type, create a new field
with a new key." Restart copying (APP A7) therefore only ever converts within a family.

## 4b. Validation levels (CV1–CV7)
| Level | Checks | Example | Set up by | When |
|---|---|---|---|---|
| 1. Field settings | required, required when (CV1), length, min/max, decimals, data type, pattern | Mobile = 10 digits | Designer (ticks) | v0.1 |
| 2. Expression rules | any condition across fields, own message | `LoanAmount <= MonthlyIncome * 60` | Designer (formula) | v0.1 |
| 3. Grid rules | per row, and the grid as a whole | `Qty > 0`; `COUNT(CoApplicants) <= 3` | Designer | v0.1 |
| 4. Step rules | only at chosen workflow steps; "required at this step" (CV2) | Sanction amount required at Sanction | Designer (process) | v0.1 |
| 5. Data lookups | compare with other records | "This PAN already has an open application" | Designer ("Must be unique" on a Searchable field) | v0.2 |
| 6. Outside systems | call an external service | PAN verification, bank account check | Connector setup | v0.3 |
| 7. Code plugins | anything else, in C# | A bank's own scoring rule | Developer (plugin SDK) | v0.5 |

Levels 5–7 run **only on the server**, because expressions cannot access data or the network (EXP-06).

**How rules run**
1. Levels 1–4 run in the browser while typing and **again on the server**; the server decides (FRM-05). Both use the same evaluator (EXP-05).
2. Field settings run first; an expression rule runs only when its fields passed their own checks (no double errors).
3. A rule whose result is blank passes (EXP-07); "required" catches missing values.
4. Rules run only on Submit, Resubmit and Approve (CV3).

## 5. Grid (G1)
| # | Rule (proposed by Claude) |
|---|---|
| GR1 | Column types: Text, Number, Currency, Date, Dropdown, Yes/No, Calculated. Not in v0.1: File upload, User picker, Checkboxes, a grid inside a grid |
| GR2 | The designer sets the minimum and maximum number of rows (default maximum 100) |
| GR3 | A calculated column may use other columns of the **same row** and form fields, e.g. `Qty * Rate` |
| GR4 | Form-level functions over a grid: `SUM(Grid.Column)` and `COUNT(Grid)`. `SUM` ignores blank cells; an empty grid sums to 0. (Added to EXP-03) |
| GR5 | Column validation (required, min/max, pattern) runs per row; errors name the row number |
| GR6 | Per workflow step a grid is **hidden**, **view-only** or **editable**; when editable, the designer also chooses whether rows can be **added** and **deleted** |
| GR7 | Each row has a stable row ID; the history records rows added, rows removed and cells changed (WF-11) |
| GR8 | Restart (APP-05) copies rows; columns are matched by key with the APP type-conversion rules (A7) |
| GR9 | Grid and column keys are typed by hand (F2); column keys are unique within their grid |

## 6. Tabs
| # | Rule (proposed by Claude) |
|---|---|
| T1 | A form may be divided into **tabs**; each tab holds sections. Tabs are not nested |
| T2 | A tab can have a **show/hide rule**; fields on a hidden tab are hidden (not validated, not saved, FRM-07) |
| T3 | On Submit with errors, each tab with errors shows a red **error count**, and the first such tab opens |
| T4 | Tabs work with the keyboard (arrow keys move between tabs; constitution 12.2) |
| T5 | A tab with no visible fields for the current step is hidden automatically |

## 7. Acceptance criteria (draft for spec.md)
**FRM-02 Designer**
- FRM-02-AC1: The designer drags fields, sections, tabs and grids onto the form, reorders them, and sees a live preview where expressions work.
- FRM-02-AC2: The designer's drag-and-drop also works with the keyboard (constitution 12.2).

**FRM-03 / FRM-04 Fields and properties**
- FRM-03-AC1: Every field type in section 2 can be added, configured and filled.
- FRM-04-AC1: A field key must be unique in the form (ignoring case), start with a letter and contain only letters, digits and `_`; reserved words are rejected (EXP E1).
- FRM-04-AC2: Changing a key that expressions use makes the save check report each broken expression (F2).
- FRM-04-AC3: A default expression such as `TODAY()` fills the field when the form opens (F7).
- FRM-04-AC4: Each control offers only its allowed data types, with the default preselected (DT1).
- FRM-04-AC5: The server rejects a value that does not fit the data type (e.g. "abc" for Whole number, 3 decimals for Decimal (2)).
- FRM-04-AC6: Publishing a version where an existing key changes type family is rejected with a message naming the field (DT2).

**FRM-05 Validation**
- FRM-05-AC1: A ready-made pattern (e.g. 10-digit mobile) rejects `98765` in the browser and on the server (F8).
- FRM-05-AC2: A request that skips browser checks (direct API call) is still rejected by the server with field-level messages.
- FRM-05-AC3: A "Required when" field is required only while its expression is true (CV1).
- FRM-05-AC4: A rule limited to the Sanction step is not checked at other steps; a field ticked "required at this step" is required only there (CV2).
- FRM-05-AC5: Reject and Send back succeed with invalid field values, and those field changes are not saved (CV3).
- FRM-05-AC6: A rule's message appears under its chosen field, or at the top of the form, as configured (CV7).

**FRM-06 / FRM-07 Calculations and show/hide**
- FRM-06-AC1: The server's value for a calculated field wins over the browser's (R1).
- FRM-07-AC1: A hidden field (or a field on a hidden tab) is not validated and its value is not saved.

**FRM-10 Files**
- FRM-10-AC1: A field allowing 5 files rejects a 6th; a file over the size limit or of a type not allowed is rejected on the server (F6).

**FRM-12 Layout (new scope ID)**
- FRM-12-AC1: A section shows 1–4 fields per row with labels left or above, as configured; on a narrow screen it shows one per row (F1).
- FRM-12-AC2: Tabs with errors show an error count after Submit, and the first one opens (T3).

**FRM-13 Grid (new scope ID)**
- FRM-13-AC1: The user adds, edits and deletes rows within the minimum and maximum row counts (GR2).
- FRM-13-AC2: `SUM(CoApplicants.MonthlyIncome)` updates as rows change, in the browser and on the server (GR4).
- FRM-13-AC3: A row error names its row number (GR5).
- FRM-13-AC4: At a step where the grid is view-only, rows cannot be added, edited or deleted, also not through the API (GR6).

**FRM-14 Step-wise saving (new scope ID)**
- FRM-14-AC1: A step's save changes only the fields editable at that step; other submitted values are ignored (S2).
- FRM-14-AC2: Two parallel steps editing different fields at the same time both succeed, and both changes are kept.
- FRM-14-AC3: Publishing a process where two branches of one parallel split can edit the same field is rejected (WF-04).

## 8. Design notes (input for plan.md, not for spec.md)
- **Form definition** = JSON document: format version (constitution 8.7), tabs → sections (fields per row,
  label position) → fields; grids with columns; each field with key, type, properties and rules (expressions).
- **Field type registry (Strategy pattern):** one class per type with `Validate`, `Convert`,
  `ToExpressionValue`; the same registry in React for rendering. A new type = a new class (Open/Closed),
  ready for plugin field types (v0.5).
- **Form data JSON** (ADR-0003 Layer 1): numbers as strings (`"12345.67"`), dates as `"2026-10-17"`, grids as
  arrays of rows, each with a stable row ID; file fields as lists of file IDs.
- **Evaluation order:** calculations and show/hide rules (fields and tabs) form one dependency graph, sorted
  with Kahn's algorithm when the form is saved; a hidden field counts as blank for the fields after it.
- **Step-wise merge:** the server takes the step's editable-field list from the published process, copies
  only those values onto the latest record, recalculates calculated fields, validates, and saves with the
  data version check (ADR-0003).
- **Patterns (regex):** .NET `RegexOptions.ECMAScript` to match the browser; a match timeout protects the
  server (ReDoS).
- **Files:** stored through `IFileStorage` (local disk in v0.1; Azure Blob later); type checked by content as
  well as extension.
- **Libraries:** dnd-kit (approved, F10); Ant Design `Form`, `Tabs`, `Table` for rendering.
