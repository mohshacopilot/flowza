using Flowza.Persistence;
using Flowza.Messaging;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Design.Infrastructure.Persistence;

/// <summary>The Design service's own database (`flowza_design`); entities arrive with the Design features.</summary>
public sealed class DesignDbContext(DbContextOptions<DesignDbContext> options) : FlowzaDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Wolverine's outbox/inbox tables, so business changes and outgoing events share one transaction.
        modelBuilder.MapWolverineEnvelopeStorage(FlowzaMessagingExtensions.EnvelopeSchema);
    }
}