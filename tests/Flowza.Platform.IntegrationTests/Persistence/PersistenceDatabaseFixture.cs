using Flowza.Persistence;
using Flowza.Testing;
using Flowza.Testing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Platform.IntegrationTests.Persistence;

/// <summary>
/// Creates the test tables in their own schema inside <c>flowza_test_worker</c> (a real SQL Server, constitution 5.2),
/// once per test class. Each test cleans the rows before it starts.
/// </summary>
public sealed class PersistenceDatabaseFixture : IAsyncLifetime
{
    public const string Database = "flowza_test_worker";

    private const string DropTablesSql = $"""
        IF OBJECT_ID(N'[{PersistenceTestDbContext.Schema}].[Attachments]') IS NOT NULL DROP TABLE [{PersistenceTestDbContext.Schema}].[Attachments];
        IF OBJECT_ID(N'[{PersistenceTestDbContext.Schema}].[Documents]') IS NOT NULL DROP TABLE [{PersistenceTestDbContext.Schema}].[Documents];
        IF OBJECT_ID(N'[{PersistenceTestDbContext.Schema}].[Notes]') IS NOT NULL DROP TABLE [{PersistenceTestDbContext.Schema}].[Notes];
        """;

    private const string ClearTablesSql =
        $"DELETE FROM [{PersistenceTestDbContext.Schema}].[Attachments]; DELETE FROM [{PersistenceTestDbContext.Schema}].[Documents]; DELETE FROM [{PersistenceTestDbContext.Schema}].[Notes];";

    public string ConnectionString { get; } = TestSqlServer.ConnectionString(Database);

    public PersistenceTestDbContext CreateContext(TimeProvider clock, FixedCurrentUser user)
    {
        var options = new DbContextOptionsBuilder<PersistenceTestDbContext>()
            .UseSqlServer(
                ConnectionString,
                sql => sql.UseCompatibilityLevel(PersistenceServiceCollectionExtensions.SqlServerCompatibilityLevel))
            .AddInterceptors(new AuditSaveChangesInterceptor(clock, user))
            .Options;
        return new PersistenceTestDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext(TimeProvider.System, new FixedCurrentUser(null));
        await context.Database.ExecuteSqlRawAsync(DropTablesSql);

        foreach (var batch in SqlBatches.Split(context.Database.GenerateCreateScript()))
        {
            await context.Database.ExecuteSqlRawAsync(batch);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateContext(TimeProvider.System, new FixedCurrentUser(null));
        await context.Database.ExecuteSqlRawAsync(ClearTablesSql, cancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
