using Flowza.Persistence;
using Flowza.Messaging;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>The Identity service's own database (`flowza_identity`); entities arrive with the Identity features.</summary>
public sealed class IdentityServiceDbContext(DbContextOptions<IdentityServiceDbContext> options) : FlowzaDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Wolverine's outbox/inbox tables, so business changes and outgoing events share one transaction.
        modelBuilder.MapWolverineEnvelopeStorage(FlowzaMessagingExtensions.EnvelopeSchema);
    }
}