# Module discussion: EXP (Expression language)

| | |
|---|---|
| **Status** | Decided by the owner (E1–E12), 2026-10-16. Details D1–D7 proposed by Claude; the owner raised no objection |
| **Feeds** | `/speckit-specify 003-expression-language` (sections 1–6) and `/speckit-plan` (section 7) |
| **Scope IDs** | EXP-01 to EXP-06 in [specs/v0.1-scope.md](../../specs/v0.1-scope.md) |
| **Owning code** | Shared building block `BuildingBlocks/Expressions` (C#) and a matching TypeScript package in the web app |

## What this file is
The result of the detailed discussion of the EXP module. Sections 1–6 are **what and why**, the input for
`spec.md`. Section 7 is **how**, the input for `plan.md`; it must **not** be copied into `spec.md`.

## 1. Purpose
Designers need small formulas without writing code. One language is used in four places:

| Where | Example | Result type |
|---|---|---|
| Calculated field (FRM-06) | `DAYS_BETWEEN(FromDate, ToDate) + 1` | number, text or date |
| Cross-field validation (FRM-05) | `ToDate >= FromDate` | true/false |
| Show/hide rule (FRM-07) | `LeaveType = "SICK" and NumberOfDays > 2` | true/false |
| Workflow Condition node (WF-02) | `LoanAmount <= 1000000` | true/false |

The hardest requirement is **EXP-05**: the browser (TypeScript) and the server (C#) must give **exactly the
same result**. The browser computes live while the user types; the server recalculates and decides
(FRM-05). Any difference (₹10,000.00 vs ₹9,999.99) would show the user one value and save another.

## 2. The language
Excel-like, because designers are business people:
```
EMI:      ROUND(LoanAmount * r * (1 + r) ^ Months / ((1 + r) ^ Months - 1), 2)
Days:     DAYS_BETWEEN(FromDate, ToDate) + 1
Show if:  LeaveType = "SICK" and NumberOfDays > 2
Rule:     IF(ISBLANK(Discount), Price, Price - Discount) > 0
Multi:    CONTAINS(Documents, "PAN")
```

### 2.1 Values (EXP-01)
| Kind | Written as |
|---|---|
| Number | `12`, `0.5`, `1000000` |
| Text | `"Chennai"`; `\"` is a quote inside text (D4) |
| True/false | `true`, `false` |
| Field reference | The field's key, bare: `FromDate` (E1) |
| Date | Only from date fields and `TODAY()`; no date literals in v0.1 |

### 2.2 Field types inside expressions
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

### 2.3 Operators (EXP-02), highest precedence first
| Level | Operators | Notes |
|---|---|---|
| 1 | `( )` | |
| 2 | `^` | Right to left: `2^3^2 = 2^9`. Exponent must be a **whole number** (E3) |
| 3 | unary `-` | `-2^2 = -4` (maths convention; Excel gives 4). The design checker warns about this pattern (D6) |
| 4 | `*` `/` | Division by zero gives blank (E5) |
| 5 | `+` `-` | Numbers only (no date arithmetic, E8; no text joining, E11) |
| 6 | `=` `!=` `<` `<=` `>` `>=` | Both sides must have the same type. Text comparison ignores case (E10) |
| 7 | `not` | |
| 8 | `and` | |
| 9 | `or` | |

### 2.4 Functions (EXP-03)
| Function | Returns | Rule |
|---|---|---|
| `DAYS_BETWEEN(a, b)` | number | Calendar days from date `a` to date `b` (`b − a`; negative if `b` is earlier) (Q2) |
| `TODAY()` | date | Today in the **installation's time zone** (E7) |
| `YEARS_BETWEEN(a, b)` | number | **Completed** years from date `a` to date `b` (age). A 29 February start date counts its anniversary on 28 February in non-leap years. Added by the owner's CV6 |
| `ROUND(x, digits)` | number | `digits` 0–10, default 0. **Half away from zero**: 2.5 → 3, −2.5 → −3 (E4) |
| `MIN(a, b, …)` / `MAX(a, b, …)` | number or date | Two or more values of the same type (D3) |
| `IF(condition, a, b)` | type of `a` | `a` and `b` must have the same type; only the chosen branch is evaluated (D2) |
| `ISBLANK(x)` | true/false | True if the field is empty |
| `CONTAINS(list, "VALUE")` | true/false | True if a multi-select field contains that stored value (E9) |
| `SUM(Grid.Column)` | number | Sum of a number/currency column of a grid; blank cells ignored; empty grid → 0 (added with the grid, [FRM](form-builder.md) GR4) |
| `COUNT(Grid)` | number | Number of rows in a grid (GR4) |

Function names and the words `and`, `or`, `not`, `true`, `false` are **reserved** and cannot be field keys (E1).
Everything is **case-insensitive**: `round` = `ROUND`, `fromdate` = `FromDate`; field keys are therefore
unique ignoring case (E2).

## 3. Owner's decisions
| # | Question | Decision |
|---|---|---|
| E1 | How fields are referenced | **Bare key** (`FromDate`). Keys start with a letter, contain only letters, digits and `_`; reserved words are not allowed |
| E2 | Upper/lower case | **Case-insensitive** everywhere; field keys unique ignoring case |
| E3 | Number arithmetic | **Decimal on both sides**: C# `decimal`; TypeScript **decimal.js** (MIT licence, **approved** under constitution 1.6). Same 28-digit precision. `^` allows whole-number exponents only |
| E4 | Rounding | **Half away from zero** (2.5 → 3, −2.5 → −3) |
| E5 | Division by zero | Result is **blank** |
| E6 | Blank fields | **Blank passes through** (see section 4) |
| E7 | `TODAY()` | Uses the **installation's time zone** (a setting), the same in browser and server |
| E8 | Date arithmetic | **Only comparisons and `DAYS_BETWEEN`** in v0.1; `ADD_DAYS` etc. later |
| E9 | Multi-select fields | Add **`CONTAINS(Field, "VALUE")`** |
| E10 | Text comparison | **Case-insensitive** |
| E11 | Text joining | **Not in v0.1** |
| E12 | Requester facts in workflow conditions | **Not in v0.1**: form fields and `TODAY()` only (comes with the authority matrix, v0.3) |

## 4. Blank values (E6)
1. Any maths, comparison or function with a blank input gives **blank**, except `ISBLANK`, `IF` (only its
   condition) and `CONTAINS` (blank list → false).
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

## 5. Details proposed by Claude (no objection from the owner)
| # | Detail |
|---|---|
| D1 | Limits: at most **2,000 characters** and **50 nesting levels** per expression (protects the server, EXP-06) |
| D2 | `IF` evaluates only the branch it needs; both branches must have the same type |
| D3 | `MIN` / `MAX` take two or more numbers or dates of one type; a blank input gives blank |
| D4 | Text literals use double quotes; `\"` is a quote inside text |
| D5 | Messages shown to designers come from translation files (constitution 12) and include the column position, with a "Did you mean…?" suggestion for unknown fields |
| D6 | `-x^n` follows the maths convention (`-2^2 = -4`); the design checker warns when it sees this pattern |
| D7 | `and` / `or` treat blank as false; `not blank` is blank |

## 6. Acceptance criteria (draft for spec.md)
**EXP-01 / EXP-02 Values and operators**
- EXP-02-AC1: `2 + 3 * 4` = 14; `(2 + 3) * 4` = 20; `2 ^ 3 ^ 2` = 512; `-2 ^ 2` = −4.
- EXP-02-AC2: `0.1 + 0.2 = 0.3` is true (decimal arithmetic, E3).
- EXP-02-AC3: `10 / 0` is blank (E5).
- EXP-02-AC4: `"Chennai" = "CHENNAI"` is true (E10).
- EXP-02-AC5: `2 ^ 0.5` is rejected when the design is saved, or gives blank at run time if the exponent comes from a field (E3).

**EXP-03 Functions**
- EXP-03-AC1: `DAYS_BETWEEN` of 2026-10-17 and 2026-10-20 is 3; reversed it is −3.
- EXP-03-AC2: `ROUND(2.5)` = 3, `ROUND(-2.5)` = −3, `ROUND(1.005, 2)` = 1.01 (E4, decimal).
- EXP-03-AC3: `TODAY()` returns the date in the installation's time zone, in the browser and on the server (E7).
- EXP-03-AC4: `CONTAINS(Docs, "PAN")` is true when the multi-select `Docs` has `PAN`; false when `Docs` is empty.
- EXP-03-AC5: `IF(true, 1, 10 / 0)` is 1, and the unused branch is not evaluated.

**EXP-07 Blank values (E6, new scope ID)**
- EXP-07-AC1: `Amount + Fee` with `Fee` empty is blank.
- EXP-07-AC2: A validation rule `ToDate >= FromDate` with `FromDate` empty passes.
- EXP-07-AC3: A show rule `Days > 2` with `Days` empty hides the field.
- EXP-07-AC4: A Condition node whose branches are all blank or false takes the default path.

**EXP-04 Design-time checks**
- EXP-04-AC1: An unknown field is reported with its position and a suggestion.
- EXP-04-AC2: `"abc" > 5` is rejected (type mismatch); `IF(x, 1, "a")` is rejected (branch types differ).
- EXP-04-AC3: A field key equal to a reserved word is rejected.
- EXP-04-AC4: Expressions longer than 2,000 characters or deeper than 50 levels are rejected (D1).

**EXP-05 Same result everywhere**
- EXP-05-AC1: Every case in the shared conformance file passes in **both** C# and TypeScript.

**EXP-06 Safety**
- EXP-06-AC1: No expression can call code, read files, use the network or query a database; only the
  listed functions exist.

## 7. Design notes (input for plan.md, not for spec.md)
- **Pipeline:** text → **lexer** → **parser** (Pratt / precedence climbing) → **syntax tree** →
  **type checker** (EXP-04) → **evaluator**. Patterns: **Interpreter** + **Visitor** (checker and evaluator
  are separate visitors over one tree). Each stage is O(n) in the expression length.
- **Built twice by hand** (C# and TypeScript) from one written grammar. Ready-made libraries (NCalc,
  expr-eval) would give each side different rules and break EXP-05.
- **Numbers:** C# `decimal`; TypeScript `decimal.js` configured to 28 significant digits and the same
  rounding. `^` by repeated squaring (O(log n)) for whole-number exponents.
- **Conformance suite (constitution 5.4):** `tests/conformance/expressions.json`, hundreds of cases
  `{ expression, fields, expected }`; xUnit and Vitest both run every case.
- **Calculated fields depending on each other (FRM-06):** build a dependency graph from field references;
  sort it with **Kahn's algorithm** (O(V+E)); a cycle is reported at save time and names the fields in it;
  at run time fields are evaluated in sorted order.
- **Installation time zone (E7):** one platform setting (e.g. `Platform:TimeZone = Asia/Kolkata`), exposed to
  the browser through an API, so both sides compute `TODAY()` the same way. Date handling uses IANA zone IDs on both sides.
- **Schedule risk:** two implementations plus the conformance suite take about **1 day** of the 4, which is
  why EXP is feature 003, built before forms and the workflow.
