using Flowza.Cli;
using Flowza.Testing;

// Prepares the flowza_test_* databases for the end-to-end run (T029; tech discussion TE5: "against the app started
// by Aspire with demo data"). Only ever touches flowza_test_* databases (TestSqlServer), never the development ones.
//   reset      before the platform starts: drop every service table, so each service starts as on a new installation
//   demo-data  once the platform runs (its migrations made the tables): load the demo users with the real tool
const int UsageError = 2;

string[] databases =
[
    "flowza_test_identity", "flowza_test_design", "flowza_test_runtime", "flowza_test_worker", "flowza_test_messaging",
];

switch (args.FirstOrDefault())
{
    case "reset":
        // Integration tests reset the same databases; hold their lock so a test run in parallel is not disturbed.
        await using (await TestDatabaseLock.AcquireAsync(databases[0], CancellationToken.None))
        {
            foreach (var database in databases)
            {
                await TestDatabase.DropServiceTablesAsync(database, CancellationToken.None);
                Console.WriteLine($"{database}: emptied");
            }
        }

        return 0;

    case "demo-data":
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:identity-db"] = TestSqlServer.ConnectionString("flowza_test_identity"),
            ["ConnectionStrings:messaging-db"] = TestSqlServer.ConnectionString("flowza_test_messaging"),
            ["Messaging:Transport"] = "SqlServer",
        };
        return await FlowzaCli.RunAsync(["demo-data"], Console.Out, Console.Error, settings);

    default:
        await Console.Error.WriteLineAsync("Usage: Flowza.EndToEnd.Setup reset | demo-data");
        return UsageError;
}
