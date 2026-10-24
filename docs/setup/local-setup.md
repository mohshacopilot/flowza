# Local setup: SQL Server (owner steps O1, O2, O4)

One-time setup of the development PC before the services run (see [tasks 001](../../specs/001-platform-foundation/tasks.md)).
The setup guide in the [README](../../README.md) (PLT-06) links here for the SQL Server steps.

**No message broker is needed.** Messages between the services go through SQL Server (`flowza_messaging`); on-premises
installations use SQL Server only and the cloud uses Azure Service Bus. RabbitMQ was dropped and its code removed
on 2026-10-22 ([ADR-0005](../decisions/0005-message-transport-selection.md) amendments 2 and 3).

**Never put passwords in git or chat.** They go only into .NET user secrets (commands are given in task T006).

## 1. SQL Server (localhost, SQL Server 2025 Express)

### 1.1 Allow SQL logins (Express allows only Windows logins by default)
1. SSMS → connect to `localhost` with Windows authentication.
2. Right-click the server → **Properties** → **Security** → choose **SQL Server and Windows Authentication mode** → OK.
3. Restart the SQL Server service (SSMS: right-click the server → **Restart**, or Services → `SQL Server (MSSQLSERVER)`).

### 1.2 Create the application login
```sql
CREATE LOGIN [flowza_app] WITH PASSWORD = '<a strong password>', CHECK_POLICY = ON;
```
Keep the password safe; it goes into user secrets later.

### 1.3 Create the 10 databases
Open [create-databases.sql](create-databases.sql) in SSMS, turn on **Query → SQLCMD Mode**, and run it.
It creates the databases, sets compatibility level 150 (SQL Server 2019) and read-committed snapshot, and
makes `flowza_app` the owner (`db_owner`) of each. It is safe to run again.

| Database | Used by | Purpose |
|---|---|---|
| `flowza_identity` | Identity service | Users, roles, login, (later) org tree and settings |
| `flowza_design` | Design service | Apps, versions, form and process designs |
| `flowza_runtime` | Runtime service | Requests, form data, tasks, history, files |
| `flowza_worker` | Worker service | Email delivery log |
| `flowza_messaging` | SQL Server message transport | Message queues only (used when `Messaging:Transport = SqlServer`) |
| `flowza_test_identity` | Integration tests | Test copy of Identity |
| `flowza_test_design` | Integration tests | Test copy of Design |
| `flowza_test_runtime` | Integration tests | Test copy of Runtime |
| `flowza_test_worker` | Integration tests | Test copy of Worker |
| `flowza_test_messaging` | Integration tests | Test copy of the message transport |

If you create them by hand instead: same names, compatibility level 150, `flowza_app` as `db_owner` in each.

## 2. Store the password on this PC (owner step O2)
Flowza keeps passwords in **.NET user secrets**: a file in your Windows profile, outside the project folder, so
they never reach git. The app (AppHost) and the tests share one store called `flowza-dev`.

Run this **yourself** in a terminal (PowerShell or Command Prompt), replacing the placeholder with the real
password (or run `docs\setup\set-secrets.ps1`). Do not paste the password into chat.
```bat
dotnet user-secrets set "Sql:Password" "<flowza_app SQL password>" --id flowza-dev
```
Check (shows the keys and values on your screen only):
```bat
dotnet user-secrets list --id flowza-dev
```
The non-secret values (server `localhost`, user `flowza_app`) are in the project's configuration files.

## 3. smtp4dev (owner step O4)
```bat
dotnet tool install -g Rnwood.Smtp4dev
```

## 4. Start Flowza
Open `Flowza.slnx` in Visual Studio 2026, set **Flowza.AppHost** as the startup project and press **F5**. Development runs on plain HTTP (no certificate needed): the Aspire dashboard opens at http://localhost:15100 and the mailbox (smtp4dev) is at http://localhost:5000.

The web app needs **Node.js 24 LTS** (with npm) on the PC. The AppHost runs `npm install` and then the web
app's Vite dev server as a program (resource `web`, http://localhost:5105); no Docker. Details and the npm
scripts: [src/Web/README.md](../../src/Web/README.md).

### 4.1 The first users: an admin, or demo data
The databases start empty. Choose **one** of these (run in the repository folder; the tool reads the same
`flowza-dev` user secrets as the AppHost):

- **A real start:** `dotnet run --project tools/Flowza.Cli -- init-admin --email <your address>` creates the first
  Platform Admin (`admin`) and prints a one-time password that must be changed at the first login.
- **A demo:** `dotnet run --project tools/Flowza.Cli -- demo-data` loads the demo users. It runs **only on a
  development installation** (the tool's launch profile sets `DOTNET_ENVIRONMENT=Development`) and **only on empty
  databases** and refuses, changing nothing, when any database already has data (PLT-07).

**Demo password (documented on purpose, C5):** every demo user logs in with `Flowza-Demo-2026` and is not asked to
change it. Demo data is for demos and development only; never load it into a real installation.

| Demo user | System roles |
|---|---|
| `demo.admin` | Platform Admin, User |
| `demo.designer` | App Designer, User |
| `priya.sharma`, `rahul.verma`, `meena.iyer`, `arjun.nair`, `kavita.rao`, `vikram.singh` | User |

Their places in the demo organization and the two reference apps (HR leave, loan approval) arrive with features
002–005. To start again from empty databases, delete the `flowza_*` development databases yourself (for example in
SQL Server Management Studio) and run `docs/setup/create-databases.sql` again; the script creates only missing
databases and never deletes one.

## 5. The look: installation name, design values, approved screenshots (feature 007)
Background: [spec 007](../../specs/007-visual-design/spec.md) (what the look must be) and
[plan 007](../../specs/007-visual-design/plan.md) §5 and §10 (how it is built and checked).

### 5.1 The installation's name (UI-08, C7)
The name appears on the login pages, in the side navigation and in the browser tab; its initials make the mark
(two words "State Bank" → `SB`, one word "Flowza" → `FL`). It is a setting of the **Identity** service:
`Installation:Name`, default `Flowza`.

- **A real installation** sets it on the Identity service, in either of these:
  - a file `appsettings.Production.json` next to Identity's program files:
    ```json
    { "Installation": { "Name": "State Bank of Example" } }
    ```
  - or the environment variable `Installation__Name` (two underscores) of the Identity service.
- **To try a name on this PC:** change `"Name"` in
  [src/Services/Identity/Flowza.Identity.Api/appsettings.json](../../src/Services/Identity/Flowza.Identity.Api/appsettings.json)
  and restart the AppHost. Put it back to `Flowza` before anything is committed (`git status` shows the file as
  changed); the repository is public, so a customer's name must never go into git.
- **Rules:** 1 to 100 characters, not blank. A wrong value stops Identity at start-up with the message
  `Installation:Name: The installation name must have 1 to 100 characters…`; the Aspire dashboard shows it in
  Identity's log.
- The web app keeps the name for up to 5 minutes, so after a change reload the page (or wait) to see it in the frame.
- From M3 the name moves to the Settings screen (PLT-08); this setting is then no longer used.

### 5.2 Changing a colour, font or size (UI-02)
Every colour, font, size, spacing and radius lives in **one file**:
[src/Web/src/app/theme/designValues.json](../../src/Web/src/app/theme/designValues.json). The web app's theme is made
from it; screens and CSS files never hold a colour of their own (a test fails if one does).

The login pages belong to Identity, which is built without Node, so they read a **generated copy**,
`src/Services/Identity/Flowza.Identity.Api/wwwroot/account/assets/design-values.css`. After every change to
`designValues.json`, regenerate it and commit both files together:
```powershell
cd src\Web
npm run design:css
```
Never edit `design-values.css` by hand. If someone forgets this step, the test
`UI_02_AC2_the_committed_login_css_equals_a_fresh_generation` fails (`npm test`, and CI), so the login pages and the
app can never drift apart.

A changed value usually changes the look of the main screens too, so CI's look check (5.3) turns red until the new
look is accepted.

### 5.3 Accepting a new look (UI-10, PA5)
On every push, CI's end-to-end job compares the main screens at desktop, tablet and phone width with the **approved
screenshots** in `tests/EndToEnd/specs/07-visual-design.spec.ts-snapshots/`. Any visible difference makes the job red.
The red run's artifact `playwright-report` holds, for each screen that differs, the approved picture, the new one and
a picture of the difference.

- **A red run you did not expect** means something changed the look by mistake: fix the code, do not accept the new
  pictures.
- **A change of the look on purpose** (a new colour, a new screen, a moved button) is accepted like this:
  1. Push the change (with the owner's OK, as always). CI's end-to-end job will be red: expected.
  2. On GitHub: **Actions → Update screenshots → Run workflow** (branch `main`, or the branch with the change). It
     runs the same tests on the same kind of Linux machine as CI and makes every picture anew; it never commits anything.
  3. When it is green, download its artifact **screenshot-baselines** (a zip file) from the run's page.
  4. The owner **looks at every new picture** and decides whether this is the approved look. Nothing is accepted
     without that look (O2).
  5. After the owner's OK, the pictures replace the ones in `07-visual-design.spec.ts-snapshots/` and are committed as
     `test(ui): accept new screenshots (<what changed>)` and pushed. The next CI run is green.

The pictures are always made on GitHub's Linux machine, never on this PC: Windows draws fonts slightly differently,
so pictures from here would never match CI. For the same reason a local end-to-end run (`run-e2e.ps1`) skips the
picture comparison and checks everything else; `FLOWZA_E2E_SCREENSHOTS=1` turns it on (CI sets it).

## 6. What to tell Claude
Only these, never passwords:
- SQL: login name (e.g. `flowza_app`), server `localhost`
