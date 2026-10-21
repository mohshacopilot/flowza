using System.Collections.Concurrent;
using System.Diagnostics;
using Flowza.Contracts;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Platform.IntegrationTests.Messaging;

/// <summary>A test event published by the "sender" test service.</summary>
public sealed record TestOrderPlaced(Guid OrderId) : IntegrationEvent;

/// <summary>The business change saved together with the event.</summary>
public sealed class TestOrder
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

/// <summary>The sender's database context: a business table plus Wolverine's outbox tables.</summary>
public sealed class MessagingTestDbContext(DbContextOptions<MessagingTestDbContext> options) : FlowzaDbContext(options)
{
    public const string Schema = "msgtest";
    public const string EnvelopeSchema = "wolverine_msgtest_sender";

    public DbSet<TestOrder> Orders => Set<TestOrder>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestOrder>(order =>
        {
            order.ToTable("Orders", Schema);
            order.Property(o => o.Name).HasMaxLength(100);
        });
        modelBuilder.MapWolverineEnvelopeStorage(EnvelopeSchema);
    }
}

/// <summary>What the receiving test service saw.</summary>
public sealed class ReceivedLog
{
    private readonly ConcurrentQueue<(Guid EventId, Guid OrderId, string? TraceId)> _entries = new();

    public void Add(TestOrderPlaced message) =>
        _entries.Enqueue((message.EventId, message.OrderId, Activity.Current?.TraceId.ToHexString()));

    public int CountFor(Guid orderId) => _entries.Count(e => e.OrderId == orderId);

    public string? TraceIdFor(Guid orderId) => _entries.FirstOrDefault(e => e.OrderId == orderId).TraceId;
}

/// <summary>The receiving handler. No attribute needed: the messaging setup deduplicates by EventId.</summary>
public static class TestOrderPlacedHandler
{
    public static void Handle(TestOrderPlaced message, ReceivedLog log)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(log);
        log.Add(message);
    }
}
