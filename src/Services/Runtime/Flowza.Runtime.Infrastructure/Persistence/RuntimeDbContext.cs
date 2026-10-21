using Flowza.Persistence;
using Flowza.Messaging;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Runtime.Infrastructure.Persistence;

/// <summary>The Runtime service's own database (`flowza_runtime`); entities arrive with the Runtime features.</summary>
public sealed class RuntimeDbContext(DbContextOptions<RuntimeDbContext> options) : FlowzaDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Wolverine's outbox/inbox tables, so business changes and outgoing events share one transaction.
        modelBuilder.MapWolverineEnvelopeStorage(FlowzaMessagingExtensions.EnvelopeSchema);
    }
}