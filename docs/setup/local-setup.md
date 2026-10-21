# Local setup: SQL Server and RabbitMQ (owner steps O1–O3)

One-time setup of the development PC before the services run (see [tasks 001](../../specs/001-platform-foundation/tasks.md)).
This will later become part of the README (PLT-06).

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

## 2. RabbitMQ (native Windows install, no Docker)

### 2.1 Install Erlang, then RabbitMQ (needs admin rights)
1. Open RabbitMQ's **"Erlang Version Requirements"** page and note the Erlang/OTP version supported by the
   current RabbitMQ release.
2. Download and install **Erlang/OTP** (Windows 64-bit installer from erlang.org) in that version. Run the
   installer **as Administrator**.
3. Download and install **RabbitMQ Server** (Windows installer `rabbitmq-server-<version>.exe` from rabbitmq.com
   or its GitHub releases). Run it **as Administrator**. It installs and starts the Windows service **RabbitMQ**.

### 2.2 Enable the web console
Open **"RabbitMQ Command Prompt (sbin dir)"** from the Start menu **as Administrator** and run:
```bat
rabbitmq-plugins enable rabbitmq_management
```
Then open http://localhost:15672 and log in with `guest` / `guest` (works only from this PC).

### 2.3 Create Flowza's virtual hosts and user
In the same command prompt (a virtual host is like a separate RabbitMQ space; tests use their own so they
never touch development messages):
```bat
rabbitmqctl add_vhost flowza
rabbitmqctl add_vhost flowza_test
rabbitmqctl add_user flowza_app <a strong password>
rabbitmqctl set_permissions -p flowza flowza_app ".*" ".*" ".*"
rabbitmqctl set_permissions -p flowza_test flowza_app ".*" ".*" ".*"
```
(Or do the same in the web console: **Admin → Virtual Hosts** and **Admin → Users**.)

### 2.4 Check
```bat
rabbitmqctl status
rabbitmqctl list_vhosts
rabbitmqctl list_permissions -p flowza
```
Ports used: **5672** (applications) and **15672** (web console). Both stay on this PC; no firewall change is needed.

### 2.5 If `rabbitmqctl` says "authentication failed" or cannot connect
The Windows service and your user account must share the same Erlang cookie file. Copy
`C:\Windows\System32\config\systemprofile\.erlang.cookie` to your user folder (`C:\Users\<you>\.erlang.cookie`),
replacing the existing one, then try again.

## 3. Store the passwords on this PC (owner step O2)
Flowza keeps passwords in **.NET user secrets**: a file in your Windows profile, outside the project folder, so
they never reach git. The app (AppHost) and the tests share one store called `flowza-dev`.

Run these **yourself** in a terminal (PowerShell or Command Prompt), replacing the placeholders with the real
passwords. Do not paste the passwords into chat.
```bat
dotnet user-secrets set "Sql:Password" "<flowza_app SQL password>" --id flowza-dev
dotnet user-secrets set "RabbitMq:Password" "<flowza_app RabbitMQ password>" --id flowza-dev
```
Check (shows the keys and values on your screen only):
```bat
dotnet user-secrets list --id flowza-dev
```
The non-secret values (server `localhost`, user `flowza_app`, virtual hosts `flowza` / `flowza_test`) are in
the project's configuration files.

## 4. smtp4dev (owner step O4)
```bat
dotnet tool install -g Rnwood.Smtp4dev
```

## 5. What to tell Claude
Only these, never passwords:
- SQL: login name (e.g. `flowza_app`), server `localhost`
- RabbitMQ: user name (e.g. `flowza_app`), virtual hosts `flowza` and `flowza_test`
