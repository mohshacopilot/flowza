# ADR-0005: Three message transports coded now, selected by configuration

| | |
|---|---|
| **Status** | Accepted (2026-10-21), including the selection rule in point 3 (MQ3). **Amended 2026-10-22: on-premises = SQL Server only, cloud = Azure Service Bus; RabbitMQ dropped and its code removed** (see the amendments at the end) |
| **Date** | 2026-10-21 |
| **Decided by** | Owner |
| **Amends** | [ADR-0004](0004-messaging-transports.md): Azure Service Bus is **coded now** instead of later; the default transport and the selection rule are defined here |

## Owner's requirement (2026-10-21)
1. If RabbitMQ is **not** there, the app works with **SQL Server** for messages.
2. If RabbitMQ **is** there, the app uses **RabbitMQ**.
3. On **Azure**, the app uses **Azure Service Bus**.
4. Write the code for all three now. Azure testing comes later; the focus now is on-premises.

## Decision
1. **Three transports, all implemented in v0.1** behind one setting, `Messaging:Transport`:
   | Value | Where | Tested now? |
   |---|---|---|
   | `SqlServer` | On-premises without RabbitMQ (**default when the setting is missing**; the development PC uses this from 2026-10-22) | Yes: locally and in CI |
   | ~~`RabbitMq`~~ | **Dropped and removed from the code 2026-10-22** (amendments 2 and 3); the value is now refused at start-up | — |
   | `AzureServiceBus` | Azure | **Code and configuration only.** Its integration tests exist but are skipped until an Azure subscription (or the Service Bus emulator) is available |
2. Service code never changes between transports: it only publishes and handles messages (Wolverine).
3. **Selection rule (MQ3):** the transport is chosen **explicitly by configuration at installation
   time**, the **same value for every service** of the installation, not by detecting at start-up whether
   RabbitMQ answers. If the configured broker is unreachable, the service reports **not ready** with a clear
   message, and events wait safely in each service's **outbox** until the broker is back.

   **Why not automatic fallback:** if RabbitMQ were briefly down while one service started, that service
   would switch to SQL Server while the others stay on RabbitMQ. Its messages would then never reach them
   (a "split brain"), and messages already waiting in RabbitMQ would be stranded. An explicit, installation-wide
   setting avoids both. "RabbitMQ is not there" therefore means "the installation is configured without it",
   and the default (`SqlServer`) covers that case.
4. The outbox and the durable inbox are on for all three transports (unchanged from ADR-0004).
5. A start-up check verifies that **all services use the same transport value** (each service publishes its
   transport in its health details; the AppHost and the setup guide set one value for all).

## Consequences
**Good**
- On-premises customers choose SQL Server (nothing extra to install) or RabbitMQ; Azure customers get Service Bus.
- No code changes when moving an installation between transports.

**Costs**
- A third transport to keep compiling and configured; its tests run only once Azure access exists.
- Changing an installation's transport needs a planned switch (drain queues first); documented in the setup guide.
- New package: `WolverineFx.AzureServiceBus` (MIT), approved with this requirement.

## Amendment 3 (owner, 2026-10-22): RabbitMQ code removed
- The RabbitMQ transport is **removed from the solution**: its configuration branch in `Flowza.Messaging`, the
  `MessageTransport.RabbitMq` value, the RabbitMQ readiness check, the `WolverineFx.RabbitMQ` package, the AppHost
  `RabbitMq` settings, the test helper and the RabbitMQ test runs. `Messaging:Transport = RabbitMq` is now refused at
  start-up like any unknown value.
- Two transports remain: `SqlServer` (on-premises, default) and `AzureServiceBus` (cloud). The pluggable design
  (one strategy per transport, ADR-0004) stays, so a broker can be added back later as a new decision.
- Git history keeps the removed code (last commit with it: `3080d4e`).

## Amendment 2 (owner, 2026-10-22): one transport per deployment type
| Deployment | Transport | `Messaging:Transport` |
|---|---|---|
| **On-premises** | **SQL Server only** (`flowza_messaging`); no message broker to install | `SqlServer` |
| **Cloud (Azure)** | **Azure Service Bus** | `AzureServiceBus` |

- **RabbitMQ is no longer a supported option** for any installation; owner requirement point 2 above is withdrawn.
  The code was removed the same day (amendment 3).
- Development PC = on-premises setup: SQL Server transport (amendment 1 below).
- Consequence for CI (T031): only a **SQL Server 2019** container is needed; no RabbitMQ container.

## Amendment 1 (owner, 2026-10-22): SQL Server only on the development PC
- RabbitMQ was **removed from the development PC**. Development and local tests use `SqlServer` only, with the
  microservices unchanged (each service keeps its own database; they talk through the queues in `flowza_messaging`).
- (Amendment 2 dropped RabbitMQ as an option altogether; amendment 3 removed its code.)
- **Topology fix (T018 finding):** Wolverine keeps SQL Server queue tables in the database of its *main* message store.
  With `SqlServer`, the main store is therefore the shared `flowza_messaging` database (queues in schema `queues`,
  each service's own envelope tables in schema `wolverine_<service>`), and the service's own database is an
  *ancillary* store enrolled with the service's DbContext. The outbox still commits in the same transaction as the
  business change (constitution 3.3), and handlers that use the DbContext record their inbox entry in that transaction
  too. Azure Service Bus keeps the service's own database as the main store.
