using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowza.Design.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` to create migrations; it never connects to a database.</summary>
public sealed class DesignDbContextFactory : IDesignTimeDbContextFactory<DesignDbContext>
{
    public DesignDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DesignDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=flowza_design;Integrated Security=true;TrustServerCertificate=true",
                sql => sql.UseCompatibilityLevel(PersistenceServiceCollectionExtensions.SqlServerCompatibilityLevel))
            .Options;
        return new DesignDbContext(options);
    }
}