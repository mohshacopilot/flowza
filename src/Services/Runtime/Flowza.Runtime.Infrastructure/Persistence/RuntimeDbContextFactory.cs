using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowza.Runtime.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` to create migrations; it never connects to a database.</summary>
public sealed class RuntimeDbContextFactory : IDesignTimeDbContextFactory<RuntimeDbContext>
{
    public RuntimeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RuntimeDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=flowza_runtime;Integrated Security=true;TrustServerCertificate=true",
                sql => sql.UseCompatibilityLevel(PersistenceServiceCollectionExtensions.SqlServerCompatibilityLevel))
            .Options;
        return new RuntimeDbContext(options);
    }
}