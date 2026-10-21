using Flowza.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Flowza.Persistence;

/// <summary>
/// Base class for every service's DbContext: UTC date handling and the Flowza model conventions
/// (audit columns, soft delete, row version, sequential Guid keys). Services describe their own model in
/// <see cref="ConfigureModel"/>.
/// </summary>
public abstract class FlowzaDbContext : DbContext
{
    protected FlowzaDbContext(DbContextOptions options)
        : base(options)
    {
        // Cascading deletes are worked out when saving, after the audit interceptor has turned the delete of a
        // soft-deletable entity into an update. Otherwise EF would already have marked its loaded children and
        // owned values as deleted, and they would be hard-deleted (constitution 8.6).
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureModel(modelBuilder);
        FlowzaModelConventions.Apply(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
    }

    /// <summary>The service's own entities and mappings.</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);
}
