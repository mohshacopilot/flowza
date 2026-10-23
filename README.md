# Flowza

A low-code platform: **form builder + workflow engine + organization hierarchy**. Business processes (leave
requests, loan approvals, …) are built by configuration, never coded into the platform. Version 0.1 is being built
feature by feature with spec-driven development: [specs/v0.1-scope.md](specs/v0.1-scope.md),
[CLAUDE.md](CLAUDE.md) (decisions and conventions) and [docs/constitution.md](docs/constitution.md).

| Part | What it does |
|---|---|
| **Gateway** | The only address the browser uses: the login session, routes to the services, the web app |
| **Identity** | Users, passwords, system roles, login pages, tokens |
| **Design**, **Runtime**, **Worker** | App designs; requests and the workflow engine; emails (features 002–006) |
| **Web** | The React app (`src/Web`) |
| **flowza** tool | `init-admin` (the first Platform Admin) and `demo-data` (`tools/Flowza.Cli`) |

---

## Set up a development PC (about 30 minutes)

Windows 10/11; no Docker or other container software is needed. Follow the steps in order; the checklist
[specs/001-platform-foundation/checklists/setup.md](specs/001-platform-foundation/checklists/setup.md) tracks them.

### 1. Install the tools
| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10 (see `global.json`) | https://dotnet.microsoft.com |
| Node.js | 24 LTS, with npm | For the web app and the end-to-end tests |
| SQL Server | 2019 or later; Express or Developer edition is fine | With SQL Server Management Studio (SSMS) |
| git | any recent | |
| Visual Studio | 2026 (optional) | Or any editor; everything also runs from the command line |
| smtp4dev | latest | `dotnet tool install -g Rnwood.Smtp4dev` (catches emails; none leave the PC) |

### 2. Get the code
```bat
git clone https://github.com/mohshacopilot/flowza.git
cd flowza
```

### 3. SQL Server: login and databases
Follow [docs/setup/local-setup.md](docs/setup/local-setup.md) §1: allow SQL logins, create the login `flowza_app`
and run `docs/setup/create-databases.sql` (ten databases: five for the app, five `flowza_test_*` for the tests).

### 4. Store the SQL password (never in git)
```bat
dotnet user-secrets set "Sql:Password" "<flowza_app password>" --id flowza-dev
```
Or run `docs\setup\set-secrets.ps1`. The AppHost, the tests and the `flowza` tool all read this one store.

### 5. Start everything
Visual Studio: open `Flowza.slnx`, set **Flowza.AppHost** as the startup project, press **F5**. Or:
```bat
dotnet run --project src/AppHost/Flowza.AppHost --launch-profile http
```
The first start runs `npm install` for the web app and creates the tables (development only). Then:

| Address | What |
|---|---|
| http://localhost:5100 | **Flowza** (through the Gateway) |
| http://localhost:15100 | Aspire dashboard: every part, its logs and traces (the login link is printed at start) |
| http://localhost:5000 | smtp4dev mailbox |

### 6. The first users
With Flowza running, choose **one** (details in [local-setup.md](docs/setup/local-setup.md) §4.1):
```bat
dotnet run --project tools/Flowza.Cli -- init-admin --email you@example.org
```
prints a one-time password for `admin`, to be changed at the first login; or
```bat
dotnet run --project tools/Flowza.Cli -- demo-data
```
loads the demo users (password `Flowza-Demo-2026`, documented on purpose; empty databases on a development PC only:
`dotnet run` sets `DOTNET_ENVIRONMENT=Development` through the tool's launch profile, and the tool refuses anything
else, so a real installation can never get the demo password).

Open http://localhost:5100 and log in. **Done.**

### 7. Run the tests
```bat
dotnet test --solution Flowza.slnx
```
```bat
cd src/Web && npm test
```
End-to-end (the whole platform in a browser, about 3 minutes; stop your running AppHost first):
[tests/EndToEnd/README.md](tests/EndToEnd/README.md). CI runs all of them on every push
([.github/workflows/ci.yml](.github/workflows/ci.yml)).

---

## Install on a server (on-premises)

> **Status:** feature 001's production pieces are in place; the final check of the feature (T032) and a first real
> installation are still to come. Azure (Blob Storage, Key Vault, Service Bus) comes later.

### What runs where
Five programs (Gateway, Identity, Design, Runtime, Worker), each with its own database on SQL Server 2019 or later,
plus `flowza_messaging` for the messages between them (no message broker). Behind them a load balancer or reverse
proxy that ends HTTPS; **only the Gateway** is reachable from users' browsers.

### Build
```bat
cd src/Web && npm ci && npm run build && cd ../..
dotnet publish src/Gateway/Flowza.Gateway -c Release -o publish/gateway
dotnet publish src/Services/Identity/Flowza.Identity.Api -c Release -o publish/identity
```
(and the same for Design, Runtime and Worker). Publishing the Gateway copies the built web app into its `wwwroot`;
the Gateway serves it (GW2).

### Prepare the databases (the DBA)
Outside Development no service creates or changes a table; the DBA does, with scripts the services write
themselves (PLT-03, PL4):

1. Create five empty databases on SQL Server 2019 or later: `flowza_identity`, `flowza_design`, `flowza_runtime`,
   `flowza_worker`, `flowza_messaging` (compatibility level 150, `READ_COMMITTED_SNAPSHOT ON`; the five
   `flowza_test_*` databases of `docs/setup/create-databases.sql` are not needed on a server). The services never
   create tables there, so their login needs read, write and execute rights; until that is tested with such a
   login, give it `db_owner` as in development.
2. With the settings below in place, let each published service write its scripts (it does not connect to any
   database for this, and Identity needs no certificates yet):
   ```bat
   dotnet publish\identity\Flowza.Identity.Api.dll --database-scripts scripts
   dotnet publish\design\Flowza.Design.Api.dll --database-scripts scripts
   dotnet publish\runtime\Flowza.Runtime.Api.dll --database-scripts scripts
   dotnet publish\worker\Flowza.Worker.dll --database-scripts scripts
   ```
3. Review and run them (SSMS in SQLCMD mode, or `sqlcmd -i`): `<service>.sql` in that service's database, and
   `<service>.messaging.sql` in `flowza_messaging`. The order between services does not matter; the scripts only add
   what is missing, so running one twice changes nothing.
4. After every upgrade: write the scripts with the new version and run them again before starting it.

### Settings
Environment variables (`Section__Key`) or `appsettings.Production.json` next to each program; secrets in
environment variables or the machine's secret store, **never** in files that go to git.

| Program | Setting | Value |
|---|---|---|
| all | `ASPNETCORE_ENVIRONMENT` | `Production` (HTTPS only, strict security headers, log files) |
| all | `ConnectionStrings__<service>-db`, `ConnectionStrings__messaging-db` | SQL Server connection strings |
| all | `Messaging__Transport` | `SqlServer` |
| all | `Flowza__Identity__Authority` | Identity's address as the services reach it |
| Identity | `Identity__Issuer` | Identity's fixed public address (the same for every service) |
| Identity | `Identity__Certificates__SigningThumbprint`, `…EncryptionThumbprint` | Two certificates in LocalMachine\My (token signing and encryption); the encryption certificate also protects Identity's own login keys in its database |
| Identity | `Identity__Clients__Gateway__Secret`, `…RedirectUris__0`, `…PostLogoutRedirectUris__0` | The Gateway's client secret and `https://<host>/signin-oidc`, `https://<host>/signout-callback-oidc` |
| Identity | `ForwardedHeaders__KnownProxies__0` (…`__1`) | The Gateway's address(es), so Identity sees https; set `ForwardedHeaders__ForwardLimit` to 2 when a load balancer sits in front of the Gateway |
| Gateway | `Gateway__Oidc__ClientSecret` | The same secret as Identity's `Gateway__Secret` |
| Gateway | `AllowedHosts` | The installation's host name(s), e.g. `flowza.example.org` (the Gateway warns when it is `*`) |
| Gateway | `ForwardedHeaders__KnownProxies__0` or `ForwardedHeaders__KnownNetworks__0` | The load balancer's address or network (e.g. `10.0.0.0/24`); only its `X-Forwarded-For/-Proto` are believed |
| Gateway | `Gateway__DataProtection__KeysFolder` | Folder for the session keys; a **file share** when there are several Gateways |
| Gateway | `Gateway__DataProtection__Protection` | `Certificate` (one or several Gateways) or `Dpapi` (one Windows server) |
| Gateway | `Gateway__DataProtection__CertificateThumbprint` | With `Certificate`: a certificate with private key in CurrentUser\My or LocalMachine\My |
| Gateway | `ReverseProxy__Clusters__<service>__Destinations__<service>__Address` | Each service's address (identity, design, runtime, worker) |
| all | `Flowza__Logging__Folder` (optional) | Where the daily log files go (kept 30 days) |

The Gateway **refuses to start** without a key folder or with unprotected keys outside Development; Identity
refuses without its certificates. Both say which setting is missing.

**If SQL Server is down when a service starts**, the service starts anyway: `/health/live` answers 200 and
`/health/ready` 503, with the check `startup` saying what it waits for. As soon as the database answers it finishes
its start (messaging, loading the deny list, …) by itself and becomes ready; no restart is needed. The load balancer
sees only the Gateway: point its health probe at the Gateway's `/health/ready`. The services behind the Gateway report
their own state on their `/health/ready`; watch those with your monitoring, as the Gateway does not check them before
forwarding a request. If a part of the start fails after it has begun (for example the messaging), the service stops
with exit code 1 instead of running half-started: let the service manager (Windows service recovery, systemd)
restart it on failure.

### Renewing a certificate
Install the new certificate, change the thumbprint setting, and **keep the old certificate installed for 90 days**:
keys it protected (sessions, Identity's login keys) stay readable while it is in the store, and they are replaced
within 90 days. Removing it earlier logs users out. (Token signing certificates follow OpenIddict's own rollover.)

### Several Gateways
Run them behind the load balancer with **sticky sessions** (the same browser stays on the same Gateway): a token
refresh is shared between parallel requests inside one Gateway only. All Gateways use the **same key folder**
(file share) and the same certificate, so any of them can read any session cookie after a failover.

### Back up
| What | Why |
|---|---|
| Databases `flowza_identity`, `flowza_design`, `flowza_runtime`, `flowza_worker` | All business data (full + log backups) |
| Database `flowza_messaging` | Messages on their way between services |
| The Gateway's key folder | Without it every user is logged out |
| The certificates (with private keys) | Token signing/encryption and the Gateway's key protection |
| The settings of each program (without secrets) and the secrets themselves, kept separately | To rebuild a server |

The `flowza_test_*` databases are never needed on a server.
