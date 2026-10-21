using Flowza.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Persistence;

/// <summary>
/// Base class for every service's DbContext: UTC date handling and the Flowza model conventions
/// (audit columns, soft delete, row version, sequential Guid keys). Services describe their own model in
/// <see cref="ConfigureModel"/>.
/// </summary>
public abstract class FlowzaDbContext(DbContextOptions options) : DbContext(options)
{
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
