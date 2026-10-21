using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` to create migrations; it never connects to a database.</summary>
public sealed class IdentityServiceDbContextFactory : IDesignTimeDbContextFactory<IdentityServiceDbContext>
{
    public IdentityServiceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityServiceDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=flowza_identity;Integrated Security=true;TrustServerCertificate=true",
                sql => sql.UseCompatibilityLevel(PersistenceServiceCollectionExtensions.SqlServerCompatibilityLevel))
            .Options;
        return new IdentityServiceDbContext(options);
    }
}