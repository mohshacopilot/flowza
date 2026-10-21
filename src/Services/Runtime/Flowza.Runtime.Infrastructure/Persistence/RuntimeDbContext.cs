using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Runtime.Infrastructure.Persistence;

/// <summary>The Runtime service's own database (`flowza_runtime`); entities arrive with the Runtime features.</summary>
public sealed class RuntimeDbContext(DbContextOptions<RuntimeDbContext> options) : FlowzaDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
    }
}