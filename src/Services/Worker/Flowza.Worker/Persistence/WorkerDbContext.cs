using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Worker.Persistence;

/// <summary>The Worker service's own database (`flowza_worker`); entities arrive with the Worker features.</summary>
public sealed class WorkerDbContext(DbContextOptions<WorkerDbContext> options) : FlowzaDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
    }
}