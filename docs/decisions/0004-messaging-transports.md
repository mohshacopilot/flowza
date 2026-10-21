# ADR-0004: Pluggable message transport: RabbitMQ and SQL Server in v0.1, Azure Service Bus later

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-21 |
| **Decided by** | Owner (MQ1, MQ2) |
| **Related** | Constitution Article 3 (events, outbox, idempotency) and Article 6 (portability); [ADR-0001](0001-audit-service-pluggable-store.md) (same pluggable pattern) |

## Context
- The platform is built as **microservices** that talk mainly through **asynchronous events** (constitution 3.2).
- It must run **on-premises and on Azure** (constitution 6.1) and must never lose an event or send one for a
  cancelled change (constitution 3.3, NFR-03).
- On-premises customers (banks, NBFCs, hospitals) differ: some accept an extra message-broker server, some
  allow only SQL Server.
- Azure offers **Azure Service Bus** as its managed broker; it has no first-party managed RabbitMQ.
- The owner wants **RabbitMQ** in v0.1 (industry-standard broker, career skill, scale).

## Decision
1. Services use **Wolverine** for messaging. Service code only publishes and handles messages; it never
   references a specific broker.
2. The transport is chosen **at startup from configuration** (`Messaging:Transport`), one per installation:
   | Value | Where | When |
   |---|---|---|
   | `RabbitMq` | On-premises; **default on the development PC** | v0.1 |
   | `SqlServer` | On-premises without an extra broker; also Azure SQL | v0.1 |
   | `AzureServiceBus` | Azure | Later, when an Azure subscription is available for testing |
3. **The transactional outbox is always on**, whatever the transport: an event is stored in the service's
   own database in the same transaction as the business change, then forwarded to the broker. If the broker
   is down, events wait in the outbox; nothing is lost.
4. **Handlers are idempotent** (constitution 3.4): a durable inbox and/or the event ID prevent double processing.
5. **Both v0.1 transports are tested:** the same messaging integration tests run against RabbitMQ and against
   SQL Server (locally and in CI), so either can replace the other.
6. The `<code>_messaging` database is used **only** by the SQL Server transport.

## How a message travels (RabbitMQ)
```
Runtime: approve  →  ONE transaction in Runtime's DB: business change + event in the outbox table
                  →  outbox sender publishes to RabbitMQ (exchange → queue per subscribing service)
Worker:           →  receives from its queue, handles the event (idempotent), acknowledges
If Worker is down →  the message waits in its RabbitMQ queue
If RabbitMQ is down → the event waits in Runtime's outbox and is sent when RabbitMQ returns
```

## Options considered
| Option | Why not chosen |
|---|---|
| SQL Server only | Works and is simplest to install, but the owner wants a real broker for scale and skills; kept as an option |
| RabbitMQ only | Every customer would have to run RabbitMQ; Azure would need a self-run RabbitMQ |
| Redis (Streams / Pub/Sub) | Primarily a cache; durability needs careful setup; not a fit for "never lose an approval event" |
| Kafka | Built for very high-volume event streaming; heavy to run on-premises; more than approvals need |
| A broker library with a commercial licence (e.g. MassTransit v9+, NServiceBus) | Licensing rule (constitution 9.3) |

## Consequences
**Good**
- Each customer picks the transport that fits their IT; the code is the same.
- Microservices stay independent; RabbitMQ gives fan-out and scale; the outbox guarantees no lost events.

**Costs**
- Two transports to configure and test in v0.1 (about half a day to one day).
- Developers and RabbitMQ customers must install **Erlang and RabbitMQ** (natively on Windows; no Docker needed)
  and secure them (own user, no default `guest` account outside localhost).
- RabbitMQ is server software under the MPL 2.0 licence that the customer runs; it is not a library inside our code.
  The Wolverine RabbitMQ and SQL Server transport packages are MIT.
