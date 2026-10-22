# Local setup: SQL Server (owner steps O1, O2, O4)

One-time setup of the development PC before the services run (see [tasks 001](../../specs/001-platform-foundation/tasks.md)).
This will later become part of the README (PLT-06).

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

## 5. What to tell Claude
Only these, never passwords:
- SQL: login name (e.g. `flowza_app`), server `localhost`
