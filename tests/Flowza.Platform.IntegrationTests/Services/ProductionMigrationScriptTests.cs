using Flowza.Persistence;
using Flowza.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Flowza.Platform.IntegrationTests.Services;

/// <summary>
/// PLT-03-AC2: production gets one reviewable script per service (tech discussion PL4). The script is idempotent,
/// so a DBA can run it twice without harm.
/// </summary>
public sealed class ProductionMigrationScriptTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Services => ["identity", "design", "runtime", "worker"];

    [Theory]
    [MemberData(nameof(Services))]
    public async Task PLT_03_AC2_the_production_script_can_be_run_twice_safely(string service)
    {
        await using var context = CreateContext(service);
        var script = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        script.ShouldContain("__EFMigrationsHistory");
        var before = (await context.Database.GetAppliedMigrationsAsync(Ct)).ToList();

        for (var run = 0; run < 2; run++)
        {
            foreach (var batch in SqlBatches.Split(script))
            {
                await context.Database.ExecuteSqlRawAsync(batch, Ct);
            }
        }

        var after = (await context.Database.GetAppliedMigrationsAsync(Ct)).ToList();
        after.ShouldBe(context.Database.GetMigrations().ToList());
        before.ShouldAllBe(migration => after.Contains(migration));
    }

    private static DbContext CreateContext(string service)
    {
        static DbContextOptions<T> Options<T>(string database)
            where T : DbContext =>
            new DbContextOptionsBuilder<T>()
                .UseSqlServer(
                    TestSqlServer.ConnectionString(database),
                    sql => sql.UseCompatibilityLevel(PersistenceServiceCollectionExtensions.SqlServerCompatibilityLevel))
                .Options;

        return service switch
        {
            "identity" => new Flowza.Identity.Infrastructure.Persistence.IdentityServiceDbContext(
                Options<Flowza.Identity.Infrastructure.Persistence.IdentityServiceDbContext>("flowza_test_identity")),
            "design" => new Flowza.Design.Infrastructure.Persistence.DesignDbContext(
                Options<Flowza.Design.Infrastructure.Persistence.DesignDbContext>("flowza_test_design")),
            "runtime" => new Flowza.Runtime.Infrastructure.Persistence.RuntimeDbContext(
                Options<Flowza.Runtime.Infrastructure.Persistence.RuntimeDbContext>("flowza_test_runtime")),
            _ => new Flowza.Worker.Persistence.WorkerDbContext(
                Options<Flowza.Worker.Persistence.WorkerDbContext>("flowza_test_worker")),
        };
    }
}
