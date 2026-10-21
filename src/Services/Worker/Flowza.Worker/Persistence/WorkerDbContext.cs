using Flowza.Persistence;
using Flowza.Messaging;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Worker.Persistence;

/// <summary>The Worker service's own database (`flowza_worker`); entities arrive with the Worker features.</summary>
public sealed class WorkerDbContext(DbContextOptions<WorkerDbContext> options) : FlowzaDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Wolverine's outbox/inbox tables, so business changes and outgoing events share one transaction.
        modelBuilder.MapWolverineEnvelopeStorage(FlowzaMessagingExtensions.EnvelopeSchema);
    }
}