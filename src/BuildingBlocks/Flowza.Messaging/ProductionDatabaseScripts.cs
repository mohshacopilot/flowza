using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weasel.Core;
using Weasel.Core.Migrations;
using Weasel.SqlServer;
using Wolverine.Persistence.Durability;
using Wolverine.Runtime;
using Wolverine.SqlServer.Transport;

namespace Flowza.Messaging;

/// <summary>One SQL script for one database, for the DBA to review and run (PLT-03-AC2, PL4).</summary>
/// <param name="Database">Which database it is for: the service's own, or the shared messaging database.</param>
/// <param name="FileName">E.g. <c>identity.sql</c> or <c>identity.messaging.sql</c>.</param>
public sealed record DatabaseScript(string Database, string FileName, string Sql);

/// <summary>
/// Everything a service needs in its databases outside Development, where nothing creates tables by itself (T033;
/// tech discussion PL4): EF Core's migrations (idempotent) and Wolverine's tables, exported with Wolverine's own
/// schema tool (Weasel) in "create if missing" style. Every script can run twice without harm. With the SQL Server
/// transport a service has tables in two databases, so it gets two scripts (ADR-0005 amendment):
/// <list type="bullet">
/// <item><c>&lt;service&gt;.sql</c>, the service's database: its tables and its outbox/inbox (schema <c>wolverine</c>);</item>
/// <item><c>&lt;service&gt;.messaging.sql</c>, <c>flowza_messaging</c>: its node tables (schema <c>wolverine_&lt;service&gt;</c>) and
/// the queue tables it reads from or writes to (schema <c>queues</c>, shared, so the services' scripts overlap).</item>
/// </list>
/// Nothing here opens a database connection.
/// </summary>
public static class ProductionDatabaseScripts
{
    /// <summary>Command-line setting: <c>--database-scripts &lt;folder&gt;</c> writes the scripts there and exits.</summary>
    public const string CommandKey = "database-scripts";

    /// <summary>The folder asked for on the command line, or null when the service should run normally.</summary>
    public static string? RequestedFolder(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration[CommandKey] is { Length: > 0 } folder ? folder : null;
    }

    public static async Task<IReadOnlyList<DatabaseScript>> CreateAsync<TContext>(IServiceProvider services, string serviceName)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var runtime = services.GetRequiredService<IWolverineRuntime>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var sqlServerTransport = FlowzaMessagingExtensions.ReadTransport(configuration) == MessageTransport.SqlServer;
        var stores = await runtime.Stores.FindAllAsync();

        // With the SQL Server transport the main store is in flowza_messaging and the service's own store is the
        // ancillary one; with any other transport the service's database is the main store (FlowzaMessagingExtensions).
        var serviceStoreRole = sqlServerTransport ? MessageStoreRole.Ancillary : MessageStoreRole.Main;

        var serviceSql = new StringBuilder();
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            serviceSql.AppendLine("-- 1. The service's tables (EF Core migrations, idempotent)")
                .AppendLine(context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent))
                .AppendLine("GO")
                .AppendLine();
        }

        serviceSql.AppendLine("-- 2. Outbox, inbox and deduplication (Wolverine)");
        foreach (var store in stores.Where(s => s.Role == serviceStoreRole))
        {
            serviceSql.AppendLine(CreationScript(store));
        }

        var scripts = new List<DatabaseScript>
        {
            new(serviceName, $"{serviceName}.sql", Header(serviceName, $"the {serviceName} service's own database") + serviceSql),
        };

        if (sqlServerTransport)
        {
            var messagingSql = new StringBuilder()
                .AppendLine("-- 1. This service's node and envelope tables (Wolverine)");
            foreach (var store in stores.Where(s => s.Role == MessageStoreRole.Main))
            {
                messagingSql.AppendLine(CreationScript(store));
            }

            messagingSql.AppendLine("-- 2. The queues this service reads from or writes to (shared with the other services)")
                .AppendLine(QueueScript(runtime));
            scripts.Add(new DatabaseScript(
                "messaging",
                $"{serviceName}.messaging.sql",
                Header(serviceName, "the shared messaging database (flowza_messaging)") + messagingSql));
        }

        return scripts;
    }

    /// <summary>
    /// For a service's Program, right after <c>Build()</c>: when started with <c>--database-scripts &lt;folder&gt;</c> it
    /// writes the scripts, lists them on the console and returns true (the service then stops without running).
    /// </summary>
    public static async Task<bool> TryWriteDatabaseScriptsAsync<TContext>(this IHost app, string serviceName)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(app);
        if (RequestedFolder(app.Services.GetRequiredService<IConfiguration>()) is not { } folder)
        {
            return false;
        }

        foreach (var file in await WriteAsync<TContext>(app.Services, serviceName, folder))
        {
            await Console.Out.WriteLineAsync(file);
        }

        return true;
    }

    /// <summary>Writes the scripts to <paramref name="folder"/> (created if missing); returns the files written.</summary>
    public static async Task<IReadOnlyList<string>> WriteAsync<TContext>(IServiceProvider services, string serviceName, string folder)
        where TContext : DbContext
    {
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        foreach (var script in await CreateAsync<TContext>(services, serviceName))
        {
            var path = Path.Combine(folder, script.FileName);
            await File.WriteAllTextAsync(path, script.Sql, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            written.Add(path);
        }

        return written;
    }

    private static string Header(string serviceName, string database) => string.Create(CultureInfo.InvariantCulture, $"""
        -- Flowza {serviceName}: production database script for {database}.
        -- Generated by the service itself (--{CommandKey}); review it, then run it with SQLCMD or SSMS (GO separates
        -- batches). It only creates what is missing, so running it twice changes nothing (PLT-03-AC2).
        SET QUOTED_IDENTIFIER ON;
        GO


        """);

    private static string CreationScript(IMessageStore store)
    {
        if (store is not IDatabase database)
        {
            throw new InvalidOperationException($"The message store '{store.Uri}' cannot write a database script.");
        }

        // Never "drop then create": a script for an existing database must not touch data.
        database.Migrator.TableCreation = CreationStyle.CreateIfNotExists;
        return database.ToDatabaseScript() + Environment.NewLine + "GO" + Environment.NewLine;
    }

    private static string QueueScript(IWolverineRuntime runtime)
    {
        var transport = runtime.Options.Transports.OfType<SqlServerTransport>().SingleOrDefault();
        if (transport is null)
        {
            return string.Empty;
        }

        var queues = transport.Endpoints().OfType<SqlServerQueue>().OrderBy(q => q.Name, StringComparer.Ordinal).ToList();
        var migrator = new SqlServerMigrator { TableCreation = CreationStyle.CreateIfNotExists };
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        migrator.WriteSchemaCreationSql([transport.TransportSchemaName], writer);
        foreach (var table in queues.SelectMany(q => new[] { QueueTableOf(q, "QueueTable"), QueueTableOf(q, "ScheduledTable") }))
        {
            table.WriteCreateStatement(migrator, writer);
            writer.WriteLine();
        }

        writer.WriteLine("GO");
        return writer.ToString();
    }

    /// <summary>
    /// A queue's table as Wolverine defines it. Wolverine keeps these definitions internal, so they are read by name;
    /// a Wolverine update that renames them fails here with this message, and the production-start tests fail too,
    /// instead of producing a script that no longer matches.
    /// </summary>
    private static ISchemaObject QueueTableOf(SqlServerQueue queue, string property) =>
        typeof(SqlServerQueue).GetProperty(property, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(queue) as ISchemaObject
        ?? throw new InvalidOperationException(
            $"Wolverine's SqlServerQueue no longer has '{property}'; update ProductionDatabaseScripts for this Wolverine version.");
}
