using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Design.Infrastructure.Persistence;

/// <summary>The Design service's own database (`flowza_design`); entities arrive with the Design features.</summary>
public sealed class DesignDbContext(DbContextOptions<DesignDbContext> options) : FlowzaDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
    }
}