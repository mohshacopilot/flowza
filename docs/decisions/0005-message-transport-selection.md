# ADR-0005: Three message transports coded now, selected by configuration

| | |
|---|---|
| **Status** | Accepted (2026-10-21). The selection rule in point 3 is Claude's proposal, awaiting the owner's confirmation (MQ3) |
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
   | `SqlServer` | On-premises without RabbitMQ (**default when the setting is missing**) | Yes: locally and in CI |
   | `RabbitMq` | On-premises with RabbitMQ (development PC uses this) | Yes: locally and in CI |
   | `AzureServiceBus` | Azure | **Code and configuration only.** Its integration tests exist but are skipped until an Azure subscription (or the Service Bus emulator) is available |
2. Service code never changes between transports: it only publishes and handles messages (Wolverine).
3. **Selection rule (proposed, MQ3):** the transport is chosen **explicitly by configuration at installation
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
