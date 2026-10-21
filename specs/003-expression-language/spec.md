# Feature 003: Expression language (EXP)

| | |
|---|---|
| **Status** | **Draft**, 2026-10-21 (clarifications C1–C11 open) |
| **Date** | 2026-10-21 |
| **Modules** | EXP (Expression language), shared by forms and workflow |
| **Scope IDs** | EXP-01 to EXP-07 ([v0.1 scope](../v0.1-scope.md)) |
| **Sources** | [EXP discussion](../../docs/discussions/expression-language.md) (E1–E12, D1–D7), [FRM discussion](../../docs/discussions/form-builder.md) (CV6, GR4, DT1), [v0.1 scope](../v0.1-scope.md) (Q2, section 8), [constitution](../../docs/constitution.md) (5.4, 7.5, 12) |
| **Next documents** | `plan.md` (how), `tasks.md` (steps); both only after this spec is approved |

> **This spec says what and why, never how.** Technology choices (parsers, libraries, programming languages)
> belong in `plan.md`. This feature has no customer technology constraint. The words **browser** and
> **server** only say *where* a result is shown (while the user types) and *where* it is decided (when the
> user submits).

## 1. Summary
App Designers need small formulas without writing code: a number of days, an EMI, a rule that one date is
after another, a rule that shows a field, a rule that sends a loan to a different approver. Flowza gives
them **one small, Excel-like formula language** that is used in four places:

| Where | Example | Result | Specified in |
|---|---|---|---|
| Calculated field (FRM-06) | `DAYS_BETWEEN(FromDate, ToDate) + 1` | number, text or date | [004-apps-and-form-builder](../004-apps-and-form-builder/) |
| Cross-field validation (FRM-05) | `ToDate >= FromDate` | true/false | [004-apps-and-form-builder](../004-apps-and-form-builder/) |
| Show/hide rule (FRM-07) | `LeaveType = "SICK" and NumberOfDays > 2` | true/false | [004-apps-and-form-builder](../004-apps-and-form-builder/) |
| Workflow Condition node (WF-02) | `LoanAmount <= 1000000` | true/false | [005-workflow-engine](../005-workflow-engine/) |

This spec defines **the language itself**: its values, operators, functions, blank values, design-time
checks and safety. How a form or a process uses an expression (where the designer types it, when it runs,
what happens to the field) is specified in features 004 and 005, not here.

**Why it matters:** the same formula runs twice. The browser computes it live while the user types, and the
server computes it again and decides (FRM-05). If the two ever differed (₹10,000.00 in the browser,
₹9,999.99 on the server), the user would see one value and the platform would save another. In a loan or a
payment that is a real harm. So the hardest requirement of this feature is **EXP-05: exactly the same result
in the browser and on the server**, proven by one shared set of test cases (constitution 5.4).

## 2. Users
| Persona | What they need from this feature |
|---|---|
| **App Designer** | Write formulas in a familiar, Excel-like way; get clear messages with a position and a suggestion when a formula is wrong, before anyone uses it |
| **Requester** | See calculated values and shown/hidden fields update while typing, and get the same values after submitting |
| **Approver** | Trust that the values on the task screen are the values the platform calculated and saved |
| **Platform Admin** | Set the installation's time zone, which `TODAY()` uses (the Settings screen comes in M3, PLT-08; see C3) |

## 3. User stories and acceptance scenarios
Priority **P1** = needed for Milestone 1; **P2** = Milestone 2.

### US1: Write formulas in a familiar way (P1, EXP-01, EXP-02)
As an App Designer, I write formulas like in a spreadsheet, using field keys, numbers, text and operators, so that I can build business rules without code.
1. **Given** a form with fields `FromDate` and `ToDate`, **when** the designer writes `ToDate >= FromDate`, **then** the rule compares the two dates; `todate >= fromdate` means the same (E1, E2).
2. **When** the designer writes `2 + 3 * 4`, **then** the result is 14, because `*` comes before `+`.
3. **When** the designer writes `0.1 + 0.2 = 0.3`, **then** the result is true: numbers are exact decimals, not approximations (E3).
4. **Given** a Dropdown with the stored value `SICK` and the label "Sick leave", **when** a rule says `LeaveType = "SICK"`, **then** it uses the stored value, not the label.
5. **When** text is compared, **then** letter case is ignored: `"Chennai" = "CHENNAI"` is true (E10).

### US2: Built-in functions for dates, rounding and choices (P1, EXP-03)
As an App Designer, I use a small set of ready functions, so that common business rules (days, age, rounding, "if", "is empty") are one short formula.
1. **When** the designer writes `DAYS_BETWEEN(FromDate, ToDate) + 1` for 2026-10-17 to 2026-10-20, **then** the result is 4.
2. **When** the designer writes `YEARS_BETWEEN(DateOfBirth, TODAY())`, **then** the result is the person's age in completed years (CV6).
3. **When** an amount is rounded with `ROUND(x, 2)`, **then** halves go away from zero (E4).
4. **When** a rule uses `CONTAINS(Documents, "PAN")`, **then** it is true when the multi-select field `Documents` has the value `PAN` (E9).
5. **When** a formula uses `TODAY()`, **then** it is today's date in the installation's time zone, the same in the browser and on the server (E7).

### US3: Totals over a grid (P2, EXP-03 `SUM` / `COUNT`)
As an App Designer, I total a grid column and count its rows, so that a form can show a total and limit the number of rows.
1. **Given** a grid `CoApplicants` with a currency column `MonthlyIncome`, **when** the form shows `SUM(CoApplicants.MonthlyIncome)`, **then** it is the total of the filled cells; blank cells are ignored; an empty grid gives 0 (GR4).
2. **When** a rule says `COUNT(CoApplicants) <= 3`, **then** it counts the grid's rows.

### US4: Empty fields behave predictably (P1, EXP-07)
As an App Designer, I want empty fields to give a clear, safe result, so that a form or a process never breaks because the user has not filled something yet.
1. **Given** `Fee` is empty, **when** the formula is `Amount + Fee`, **then** the result is blank (E6).
2. **Given** `Days` is empty, **when** a show rule says `Days > 2`, **then** the field stays hidden.
3. **Given** `FromDate` is empty, **when** the validation rule `ToDate >= FromDate` runs, **then** it passes; the field's "required" setting catches the missing value.
4. **Given** a Condition node whose branches are all blank or false, **when** the request reaches it, **then** it takes the default path; a Condition never fails at run time.

### US5: Mistakes are caught when the design is saved (P1, EXP-04)
As an App Designer, I get clear messages when a formula is wrong, so that I fix it before any request uses it.
1. **When** the designer writes `FromDte > TODAY()`, **then** the message names the unknown field, its column position, and asks "Did you mean FromDate?" (D5).
2. **When** the designer writes `"abc" > 5`, **then** the message says the two sides have different types.
3. **When** the designer names a field `and` or `ROUND`, **then** the key is rejected as a reserved word (E1).
4. **When** the designer writes `-Amount ^ 2`, **then** a warning explains that this means `-(Amount ^ 2)` (D6).

### US6: The same result everywhere (P1, EXP-05)
As the business, I need the browser and the server to calculate exactly the same, so that the value a user sees is the value that is saved and used for decisions.
1. **Given** the shared set of test cases, **when** it runs against the browser's calculation and against the server's calculation, **then** every case passes on both.

### US7: Formulas are safe (P1, EXP-06)
As customer IT, I need formulas to be harmless, so that a designer (or an attacker with designer rights) can never use a formula to reach data or systems.
1. **Then** a formula can only use the values, operators and functions listed in this spec; it can never run code, read files, use the network or query a database (constitution 7.5).

## 4. Requirements
| ID | Requirement | Milestone |
|---|---|---|
| EXP-01 | **Values:** numbers, text in double quotes (`\"` is a quote inside text, D4), `true` and `false`, dates (only from date fields and `TODAY()`; no date literals), and field references by the field's **bare key** (E1). Each field gives a value of a fixed type (section 4.2, see C1). Function names, keywords and field keys are **case-insensitive** (E2) | M1 |
| EXP-02 | **Operators** `( ) ^ - * / + - = != < <= > >= not and or` with the precedence in section 4.3. Arithmetic is **exact decimal arithmetic, giving the same result in the browser and on the server** (E3). `^` takes whole-number exponents only (E3). Division by zero gives blank (E5). `+` and `-` work on numbers only (E8, E11). Both sides of a comparison must have the same type; text comparison ignores case (E10) | M1 |
| EXP-03 | **Functions**, exactly those in section 4.4: `DAYS_BETWEEN`, `TODAY`, `YEARS_BETWEEN`, `ROUND`, `MIN`, `MAX`, `IF`, `ISBLANK`, `CONTAINS`. **M2:** `SUM` and `COUNT` over a grid, delivered with the grid (FRM-13, GR4) | M1 / M2 |
| EXP-04 | **Design-time checks** when a design is saved: syntax, unknown fields, unknown functions, wrong number of arguments, wrong types, a result type that does not fit its use, reserved words used as field keys (E1), at most **2,000 characters** and **50 nesting levels** per expression (D1). Messages come from translation files and include the column position, with a "Did you mean…?" suggestion for unknown fields (D5). A **warning** for the `-x^n` pattern (D6). Whether an error blocks the save or only the publish: see C2 | M1 |
| EXP-05 | The same expression with the same field values gives **exactly the same result in the browser and on the server**, proven by **one shared set of test cases** that both must pass (constitution 5.4) | M1 |
| EXP-06 | Expressions **cannot run code or access files, network or database**; only the listed operators and functions exist (constitution 7.5). The only inputs are form field values and today's date; requester facts are not available in v0.1 (E12) | M1 |
| EXP-07 | **Blank values pass through** (E6, D7) as in section 4.5. A blank final result means: show/hide rule → hidden; validation rule → passes; Condition branch → not taken; calculated field → empty | M1 |

### 4.1 Values (EXP-01)
| Kind | Written as |
|---|---|
| Number | `12`, `0.5`, `1000000` |
| Text | `"Chennai"`; `\"` is a quote inside text (D4) |
| True/false | `true`, `false` |
| Field reference | The field's key, bare: `FromDate` (E1) |
| Date | Only from date fields and `TODAY()`; no date literals in v0.1 |

### 4.2 Field types inside expressions (EXP-01, as recorded in the EXP discussion; see C1)
| Field type | Value in expressions |
|---|---|
| Text, Text area, Email | text |
| Number, Currency, Calculated (number) | number |
| Date | date (no time part) |
| Dropdown, Radio | text: the option's **stored value**, not its label |
| Yes/No | true/false |
| Checkboxes (multi-select) | list of values; usable only with `CONTAINS` and `ISBLANK` (E9) |
| User picker | the user's ID (text); in practice used with `ISBLANK` |
| File upload | usable only with `ISBLANK` |

### 4.3 Operators (EXP-02), highest precedence first
| Level | Operators | Notes |
|---|---|---|
| 1 | `( )` | |
| 2 | `^` | Right to left: `2^3^2 = 2^9`. The exponent must be a **whole number** (E3; see C8) |
| 3 | unary `-` | `-2^2 = -4` (maths convention; a spreadsheet gives 4). The design check warns about this pattern (D6) |
| 4 | `*` `/` | Division by zero gives blank (E5) |
| 5 | `+` `-` | Numbers only (no date arithmetic, E8; no text joining, E11) |
| 6 | `=` `!=` `<` `<=` `>` `>=` | Both sides must have the same type. Text comparison ignores case (E10; see C6) |
| 7 | `not` | |
| 8 | `and` | |
| 9 | `or` | |

Numbers are exact decimals with the same precision of 28 digits in the browser and on the server (E3; see C9).

### 4.4 Functions (EXP-03)
| Function | Returns | Rule | Milestone |
|---|---|---|---|
| `DAYS_BETWEEN(a, b)` | number | Calendar days from date `a` to date `b` (`b − a`; negative if `b` is earlier) (Q2) | M1 |
| `TODAY()` | date | Today in the **installation's time zone** (E7; see C3) | M1 |
| `YEARS_BETWEEN(a, b)` | number | **Completed** years from date `a` to date `b` (age). A 29 February start date counts its anniversary on 28 February in non-leap years (CV6; see C7) | M1 |
| `ROUND(x, digits)` | number | `digits` 0–10, default 0. **Half away from zero**: 2.5 → 3, −2.5 → −3 (E4; see C10) | M1 |
| `MIN(a, b, …)` / `MAX(a, b, …)` | number or date | Two or more values of the same type; a blank input gives blank (D3) | M1 |
| `IF(condition, a, b)` | type of `a` | `a` and `b` must have the same type; only the chosen branch is evaluated (D2; see C4) | M1 |
| `ISBLANK(x)` | true/false | True if the field is empty | M1 |
| `CONTAINS(list, "VALUE")` | true/false | True if a multi-select field contains that stored value (E9) | M1 |
| `SUM(Grid.Column)` | number | Sum of a number or currency column of a grid; blank cells ignored; empty grid → 0 (GR4) | M2 |
| `COUNT(Grid)` | number | Number of rows in a grid (GR4) | M2 |

Function names and the words `and`, `or`, `not`, `true`, `false` are **reserved** and cannot be field keys (E1;
for `SUM` and `COUNT` in M1 see C11). Everything is **case-insensitive**: `round` = `ROUND`, `fromdate` =
`FromDate`; field keys are therefore unique ignoring case (E2).

How a grid's row-level calculated columns and row rules refer to the columns of the same row (GR3) is
specified with the grid in [004-apps-and-form-builder](../004-apps-and-form-builder/); the language rules here apply to them unchanged.

### 4.5 Blank values (EXP-07, E6)
1. Any maths, comparison or function with a blank input gives **blank**, except `ISBLANK`, `IF` (only its condition; see C4) and `CONTAINS` (blank list → false).
2. Division by zero gives blank (E5).
3. `and` / `or` treat blank as **false**; `not blank` is blank (D7).
4. What a blank **final** result means depends on where the expression is used:

| Used in | Blank result means |
|---|---|
| Show/hide rule | **Hidden** (false) |
| Validation rule | **Passes**; the field's "required" setting catches missing values |
| Workflow Condition branch | **Not taken**; if no branch is true, the default path is used |
| Calculated field | **Empty** value |

So a workflow Condition never fails at run time: it always ends on a branch or the default path.

## 5. Acceptance criteria
Every criterion gets at least one automated test whose name contains its ID (constitution 1.4), unless marked *manual*.
Every criterion that names an expression and its result is also a case in the shared test set (EXP-05-AC2).

| ID | Criterion |
|---|---|
| EXP-01-AC1 | A field is referenced by its bare key in any letter case: with `FromDate` = 2026-10-17, `FromDate`, `fromdate` and `FROMDATE` all give 2026-10-17 (E1, E2) |
| EXP-01-AC2 | `"Say \"yes\""` is the text `Say "yes"` (D4); `true` and `TRUE` are the same value (E2) |
| EXP-01-AC3 | For a Dropdown option with stored value `SICK` and label "Sick leave", `LeaveType = "SICK"` is true and `LeaveType = "Sick leave"` is false |
| EXP-01-AC4 | A Yes/No field gives true/false: with `Urgent` = Yes, `Urgent and 1 < 2` is true |
| EXP-01-AC5 | A Checkboxes field used anywhere other than `CONTAINS` or `ISBLANK`, or a File upload field used anywhere other than `ISBLANK`, is reported when the design is saved (E9) |
| EXP-01-AC6 | There are no date literals: `FromDate > 2026-10-21` is reported as a type mismatch when the design is saved, because `2026-10-21` is a number calculation, not a date |
| EXP-02-AC1 | `2 + 3 * 4` = 14; `(2 + 3) * 4` = 20; `2 ^ 3 ^ 2` = 512; `-2 ^ 2` = −4 |
| EXP-02-AC2 | `0.1 + 0.2 = 0.3` is true (decimal arithmetic, E3) |
| EXP-02-AC3 | `10 / 0` is blank (E5) |
| EXP-02-AC4 | `"Chennai" = "CHENNAI"` is true (E10) |
| EXP-02-AC5 | `2 ^ 0.5` is rejected when the design is saved, or gives blank at run time if the exponent comes from a field (E3; see C2) |
| EXP-02-AC6 | `true or false and false` is true (`and` before `or`); `not 1 = 2` is true (comparison before `not`) |
| EXP-02-AC7 | `FromDate + 1` (date arithmetic, E8) and `"Ab" + "c"` (text joining, E11) are reported when the design is saved |
| EXP-03-AC1 | `DAYS_BETWEEN` of 2026-10-17 and 2026-10-20 is 3; reversed it is −3 |
| EXP-03-AC2 | `ROUND(2.5)` = 3, `ROUND(-2.5)` = −3, `ROUND(1.005, 2)` = 1.01 (E4, decimal) |
| EXP-03-AC3 | `TODAY()` returns the date in the installation's time zone, in the browser and on the server (E7). Example: with the time zone India (UTC+5:30) at 2026-10-20 20:00 UTC, `TODAY()` is 2026-10-21 on both |
| EXP-03-AC4 | `CONTAINS(Docs, "PAN")` is true when the multi-select `Docs` has `PAN`; false when `Docs` is empty |
| EXP-03-AC5 | `IF(true, 1, 10 / 0)` is 1, and the unused branch is not evaluated |
| EXP-03-AC6 | `YEARS_BETWEEN` from 2000-10-21 to 2026-10-20 is 25 and to 2026-10-21 is 26; from 2004-02-29 to 2025-02-28 is 21 and to 2025-02-27 is 20 (CV6) |
| EXP-03-AC7 | `MAX(3, 7, 5)` = 7; `MIN` of two date fields gives the earlier date; `MIN(1, Amount)` with `Amount` empty is blank; `MAX(1, FromDate)` and `MIN(1)` are reported when the design is saved (D3) |
| EXP-03-AC8 | `ROUND(2.345, 2)` = 2.35 and `ROUND(-2.345, 2)` = −2.35; `ROUND(1.5, 11)` is reported when the design is saved (digits 0–10) |
| EXP-03-AC9 | `ISBLANK` is true for an empty field (also an empty multi-select, a File upload with no files and an empty User picker) and false when it has a value; `IF(ISBLANK(Discount), Price, Price - Discount)` gives `Price` when `Discount` is empty |
| EXP-03-AC10 | *(M2)* For a grid with `MonthlyIncome` cells 40,000, blank and 25,000, `SUM(CoApplicants.MonthlyIncome)` is 65,000 and `COUNT(CoApplicants)` is 3; for a grid with no rows they are 0 and 0 (GR4) |
| EXP-03-AC11 | *(M2)* `SUM` over a grid column that is not a number or currency column is reported when the design is saved |
| EXP-04-AC1 | An unknown field is reported with its position and a suggestion (`FromDte` → "Did you mean FromDate?") (D5) |
| EXP-04-AC2 | `"abc" > 5` is rejected (type mismatch); `IF(x, 1, "a")` is rejected (branch types differ) |
| EXP-04-AC3 | A field key equal to a reserved word is rejected |
| EXP-04-AC4 | Expressions longer than 2,000 characters or deeper than 50 levels are rejected (D1) |
| EXP-04-AC5 | A syntax error (e.g. `(1 + 2` without its closing bracket) is reported with its column position |
| EXP-04-AC6 | An unknown function (e.g. `ADD_DAYS(FromDate, 1)`) and a wrong number of arguments (e.g. `DAYS_BETWEEN(FromDate)`) are reported |
| EXP-04-AC7 | A result that does not fit its use is reported: a show/hide, validation or Condition expression that does not give true/false (e.g. `Amount + 1`), or a calculated field whose formula type does not fit the field's data type |
| EXP-04-AC8 | `-Amount ^ 2` gives a **warning**, not an error, explaining that it means `-(Amount ^ 2)` (D6) |
| EXP-04-AC9 | Every message shown to designers comes from the translation files (D5, constitution 12) |
| EXP-05-AC1 | Every case in the shared conformance file passes in **both** the browser calculation and the server calculation |
| EXP-05-AC2 | The shared set contains every expression example in this section, and at least one case for every operator, every function, every blank rule and every limit in section 4 |
| EXP-06-AC1 | No expression can call code, read files, use the network or query a database; only the listed functions exist |
| EXP-06-AC2 | A name that is not a field of the form or process, such as a requester fact (`RequesterUnit`), is reported as unknown when the design is saved (E12) |
| EXP-06-AC3 | *(manual review checklist)* A review confirms that evaluating an expression has no path to code, files, network or database |
| EXP-07-AC1 | `Amount + Fee` with `Fee` empty is blank |
| EXP-07-AC2 | A validation rule `ToDate >= FromDate` with `FromDate` empty passes |
| EXP-07-AC3 | A show rule `Days > 2` with `Days` empty hides the field |
| EXP-07-AC4 | A Condition node whose branches are all blank or false takes the default path |
| EXP-07-AC5 | With the Yes/No field `Urgent` empty: `Urgent or 1 < 2` is true, `Urgent and 1 < 2` is false, and `not Urgent` is blank (D7) |
| EXP-07-AC6 | A calculated field whose formula gives blank (e.g. `Amount / Count` with `Count` = 0) is empty |
| EXP-07-AC7 | A blank inside the chosen `IF` branch passes through: `IF(true, Fee, 1)` with `Fee` empty is blank |

## 6. Edge cases
- The browser computes `TODAY()` just before midnight and the user submits just after: the server's value wins (calculated fields are recalculated by the server, FRM-06, feature 004).
- A user works in a different time zone from the installation: `TODAY()` still uses the installation's time zone (E7), although times on screen are shown in the browser's time zone (IAM L7).
- A designer renames a field key in a draft: expressions are not updated; the save check reports every broken reference (F2, feature 004).
- A designer writes a date as `2026-10-21`: this is the number 1995 (2026 − 10 − 21), so comparing it with a date is reported as a type mismatch (EXP-01-AC6).
- The exponent of `^` comes from a field and is not a whole number: the result is blank, not an error (E3, EXP-02-AC5).
- `0 / 0` gives blank, like any division by zero (E5).
- Two Condition branches are both true: the first in the designer's order is taken (W6, feature 005).
- A Dropdown option's label is changed in a new version: rules still work, because they use the stored value.
- Two fields whose keys differ only in letter case (`Amount` and `amount`) cannot exist in one form (E2).
- An expression refers to a field that is currently hidden: hidden fields are not saved (FRM-07). What value an expression sees for a hidden field is settled in the spec of feature 004.

## 7. Non-functional requirements that apply
From the [v0.1 scope](../v0.1-scope.md) section 6: NFR-05 (dates have no time part), NFR-06 (the same results in the latest Chrome, Edge and Firefox), NFR-07 (privacy: designer messages and logs never contain form values), NFR-10 (designer messages from translation files), NFR-12 (coverage). From the [constitution](../../docs/constitution.md): 5.4 (one shared set of test cases for both sides) and 7.5 (no code, files, network or database).

## 8. Key entities (business view, not database design)
| Entity | Meaning |
|---|---|
| **Expression** | A short formula written by a designer, at most 2,000 characters and 50 nesting levels; it gives one value of a known type |
| **Value** | A number, text, true/false, date, list (multi-select) or **blank** |
| **Field reference** | A field's key written bare in an expression; it gives the field's current value |
| **Function** | One of the built-in functions in section 4.4; no others exist |
| **Reserved word** | A function name or `and`, `or`, `not`, `true`, `false`; never usable as a field key |
| **Design message** | An error or warning about an expression, with a column position and, for unknown fields, a suggestion |
| **Installation time zone** | The time zone of one installation; `TODAY()` uses it in the browser and on the server |
| **Shared test set** | The one list of expressions, field values and expected results that both the browser and the server calculation must pass |

## 9. Out of scope
| Item | When |
|---|---|
| Date literals; date arithmetic such as `ADD_DAYS` | Later (E8) |
| Text joining | Not in v0.1 (E11) |
| Requester facts (unit, role, grade) in workflow conditions | v0.3, with the authority matrix (E12) |
| Working days and holidays in `DAYS_BETWEEN` | Later (Q2) |
| Date-and-time values | Later (DT3) |
| Full expression language with autocomplete | v0.4 |
| Lookups to other records or outside systems from an expression | Never in the language (EXP-06); v0.2 "Must be unique" and v0.3 connectors are separate features |

## 10. Clarifications (open, for the owner)
| # | Question | Options | Recommendation |
|---|---|---|---|
| C1 | The EXP discussion maps **controls** to expression types (Dropdown and Radio give text; Text box gives text). The later FRM decision DT1 gives every control an explicit **data type** (a Dropdown, Radio or Checkboxes may be Whole number; a Text box may be Whole number or Decimal). Which decides the type inside expressions? | a) The field's **data type** (FRM section 4a): Text → text, Whole number and Decimal → number, Date → date, Yes/No → true/false, User → user ID text, File → `ISBLANK` only; a Checkboxes list holds values of its data type · b) The control, as in section 4.2 | ⭐ a): DT1 is the later decision, and the form designer already says "the expression checker verifies" against the data type |
| C2 | Does an expression error **block saving** the draft, or only **publishing**? (The draft criteria say "rejected when the design is saved"; F2 says "the save check reports every broken reference".) | a) The draft is saved and all errors are listed; **publishing** is refused until they are fixed · b) Saving is refused while any expression has an error | ⭐ a): a designer who renames a key does not lose work, and nothing broken can reach users |
| C3 | Where does the installation time zone for `TODAY()` come from in **M1**, before the Settings screen (PLT-08, M3)? | a) In M1 one fixed time zone set at installation (technical configuration); M3 makes it editable on the Settings screen · b) Move the time zone setting forward into M1 | ⭐ a): the same approach as the fixed defaults for sessions in feature 001 (C1 there) |
| C4 | What does `IF` give when its **condition is blank**? (Section 4.5 says the condition is an exception to "blank passes through", but not what happens.) | a) A blank condition counts as false, so `IF` gives `b` (like `and` / `or`, D7) · b) `IF` gives blank | ⭐ a): it matches D7 and lets designers write safe defaults |
| C5 | Is **text with no characters** blank? E.g. is `Name = ""` the same as `ISBLANK(Name)`? | a) Yes: an empty text value, and the literal `""`, count as blank · b) No: empty text is an ordinary text value, different from blank | ⭐ a): one meaning of "empty" for designers; users cannot see a difference between empty text and no value |
| C6 | May `<` `<=` `>` `>=` compare **text** or **true/false**? (Ordering text by letters may differ by language and threaten EXP-05.) | a) Only `=` and `!=` for text and true/false; ordering only for numbers and dates · b) Allow text ordering by one fixed character order on both sides | ⭐ a): no business example needs it, and it removes a risk to EXP-05 |
| C7 | What does `YEARS_BETWEEN(a, b)` give when `b` is **earlier** than `a`? | a) Negative completed years, like `DAYS_BETWEEN` (e.g. −1) · b) Blank · c) 0 | ⭐ a): the same rule as `DAYS_BETWEEN` |
| C8 | Are **negative** whole-number exponents allowed (`2 ^ -1`)? | a) Yes: `2 ^ -2` = 0.25; `0 ^ -1` is a division by zero and gives blank · b) No: only 0 and positive exponents; others are reported at save or give blank from a field | ⭐ a): "whole number" (E3) then has its plain maths meaning |
| C9 | What happens when a result is **too large** for 28-digit decimals (e.g. `10 ^ 40`)? | a) Blank, like division by zero (no run-time error) · b) A run-time error | ⭐ a): keeps the rule that expressions never fail at run time, so a Condition always ends on a path |
| C10 | `ROUND`'s `digits` comes from a field and is outside 0–10 or not whole: what happens? | a) Blank, like a non-whole exponent from a field (E3); a written number outside 0–10 is reported at save · b) `digits` must always be a written number, never a field | ⭐ a): the same rule as `^` |
| C11 | `SUM` and `COUNT` arrive in M2. Are they **reserved** from M1, so that no field key made in M1 (e.g. `Count`) breaks in M2? | a) Yes, all function names in section 4.4 are reserved from M1 · b) Reserve them only in M2 | ⭐ a): avoids published M1 apps becoming invalid in M2 |

## 11. Spec quality checklist (Claude checks before asking for approval)
- [x] Every requirement has an ID, a milestone and at least one acceptance criterion
- [x] No technology choices in the spec (no customer constraint applies to this feature)
- [x] Every acceptance criterion is testable; the one manual check is marked (EXP-06-AC3)
- [x] Out-of-scope items are listed so nobody builds them by mistake
- [x] Every owner decision (E1–E12, D1–D7, CV6, GR4, Q2) is kept as recorded, and the draft criteria keep their IDs
- [x] Uses owned by other features (form rules in 004, Condition nodes in 005) are referenced, not specified twice
- [ ] All clarifications answered (C1–C11)
- [ ] Approved by the owner
