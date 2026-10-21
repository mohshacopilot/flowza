using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowza.Worker.Persistence;

/// <summary>Used only by `dotnet ef` to create migrations; it never connects to a database.</summary>
public sealed class WorkerDbContextFactory : IDesignTimeDbContextFactory<WorkerDbContext>
{
    public WorkerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=flowza_worker;Integrated Security=true;TrustServerCertificate=true",
                sql => sql.UseCompatibilityLevel(PersistenceServiceCollectionExtensions.SqlServerCompatibilityLevel))
            .Options;
        return new WorkerDbContext(options);
    }
}