# ADR-0003: Form data storage in three layers, with step-wise saving

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-16 |
| **Decided by** | Owner (S1–S3) |
| **Extends** | [ADR-0002](0002-form-data-original-and-read-copy.md) (original in SQL Server, read copy synced by events) |

## Context
- Real forms are large. A loan origination form can have **more than 100 fields**, plus grids
  (co-applicants, income sources, collateral items).
- The owner raised two concerns about storing form data as JSON:
  1. **Querying:** JSON is hard to search, filter and report on.
  2. **Saving:** saving 100+ fields at once may be slow and hard to manage.
- Different people fill different parts of one record over several days: the branch enters the applicant,
  credit enters the appraisal, legal enters the collateral, sometimes **at the same time** (parallel steps).
- Form versions change fields between app versions (removed fields, changed types), and running requests
  stay on their version (APP-04) or are restarted (APP-05).

## Decision
### 1. Three layers, each with one job
```
 Layer 1  ORIGINAL        one JSON document per record        Runtime, SQL Server   (v0.1)
              │ same transaction
 Layer 2  SEARCH INDEX    only fields marked "Searchable",    Runtime, SQL Server   (v0.2)
                          typed and indexed
              │ FormDataSaved event (ADR-0002)
 Layer 3  REPORTING       one real table per form, real       Datalist/reporting    (later)
                          columns; grid rows as child tables  database
```
- **Layer 1, the original (write side).** One row per form record: fixed columns plus one JSON column with
  all field values, including grid rows as arrays. This is the source of truth (ADR-0002).
- **Layer 2, the search index.** The designer marks a few fields as **Searchable** (e.g. PAN, mobile, loan
  amount). In the same transaction, Runtime writes their values into one typed table
  (record, field key, text/number/date value) with indexes. "Find all loans with PAN X" becomes an index seek.
  No table structure changes when designers add searchable fields.
- **Layer 3, reporting tables.** The read copy of ADR-0002, in its SQL Server form: **one real table per
  form with typed columns**, grids as child tables, created and changed when a version is published. DBAs,
  BI tools and Excel see normal tables. Changing table structures is safe **here only**, because this database
  holds a copy that can always be **rebuilt** from Runtime, and approvals never depend on it. The MongoDB
  option of ADR-0002 remains possible for this layer.

### 2. Step-wise saving
- Each workflow step may edit **only the fields allowed for that step** (WF-03 field permissions).
- The server accepts **only those fields** from the request (anything else is ignored and logged by ID, constitution 7.2) and merges them into the latest record. The data version goes up by 1 (ADR-0002).
- So a save carries only the 10–30 fields of that step, never all 100+.
- **Parallel branches never share an editable field.** The publish check (WF-04) rejects a process where two
  branches of the same parallel split can edit the same field or grid. Parallel steps therefore never
  overwrite each other, and the merge needs no conflict resolution.
- If a step saves based on an old data version **and** one of its fields was changed meanwhile, the save is
  rejected with a clear message (constitution 8.5). Changes to other fields are merged without conflict.

### 3. Layer 3 type rules (added the same day, owner's DT1–DT3)
- Column types come from the **declared data type** of each field in the published form definition
  (received through the version-published event), **never** guessed from the JSON.
- Mapping: Text → `nvarchar(n)` or `nvarchar(max)`; Whole number → `bigint`; Decimal → `decimal(28, places)`;
  Date → `date`; Yes/No → `bit`; User → `nvarchar(64)`; Checkboxes, File upload and Grid → **child tables**.
- Fixed columns on every reporting table: `RecordId`, `RequestNumber`, `AppVersion`, `Status`, `CreatedBy`,
  `CreatedAt`, `UpdatedAt`, `DataVersion`. Column names are the field keys.
- Between versions: an added field adds a nullable column; a removed field's column is **kept**; a change
  within the same type family **widens** the column (it never shrinks). A change of type family cannot
  happen, because the publish check rejects it (DT2).

## Why not real columns in the original?
| Option | Why not chosen for the original |
|---|---|
| A table per form with real columns (Joget style) | Removing a field or changing its type in a new version would need `ALTER TABLE` on the live business table while requests run; history, restart (APP-05) and rollback (APP-03) would become much harder; bank DBAs often forbid runtime schema changes on production tables |
| One row per field (EAV) | 100 rows per save; very slow to read a whole record; complex queries |
| JSON only, nothing else | Simple, but slow searching (no index) and poor for BI tools |

Real columns are still provided, in Layer 3, where they are safe.

## Performance notes
- A 100-field record is about 10–20 KB of JSON: one row write, a few milliseconds on SQL Server. Larger
  values are stored off-row automatically.
- Showing a record needs the whole form, so reading one JSON row is faster than joining 100 EAV rows.
- Searching uses Layer 2 indexes; reporting uses Layer 3 tables. Neither touches the JSON in normal use.
- Numbers are stored as strings in JSON (`"12345.67"`) so decimal precision is never lost (EXP, E3).

## Consequences
**Good**
- Fast, simple, transactional saves; versions and restart stay easy.
- Fast search on chosen fields without schema changes.
- Real relational tables for reporting, without risk to the business data.
- Parallel steps cannot overwrite each other's work.

**Costs**
- Searchable values are stored twice (JSON and index table); only for fields the designer marks.
- Layer 3 needs a table generator and a rebuild tool.
- Designers must plan which step edits which fields (enforced by the publish check).

## Timing
| Version | What |
|---|---|
| **v0.1** | Layer 1, step-wise saving, the parallel-branch publish check, `FormDataSaved` |
| **v0.2** | Layer 2 (Searchable fields), used by datalists and the "Datalist view" control with records as its source |
| **Later** | Layer 3 reporting tables, when reporting is needed |
