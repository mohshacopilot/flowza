namespace Flowza.Cli.DemoData;

/// <summary>
/// <c>flowza demo-data</c> (PLT-07, C5): loads the demo installation into empty databases. The registry of demo data
/// sets is here; each feature with demo data adds its set to the list.
/// </summary>
public static class DemoDataCommand
{
    public static async Task<int> RunAsync(TextWriter output, TextWriter error, IReadOnlyDictionary<string, string?>? settings)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        // C5 (owner, 2026-10-23): demo users share a public password, so a production installation must never get them,
        // not even while its databases are still empty. `dotnet run` uses the tool's launch profile (Development).
        if (!IsDevelopment(settings))
        {
            await error.WriteLineAsync(
                "Refused: demo-data runs only on a development installation (DOTNET_ENVIRONMENT=Development). " +
                "Demo users share a public password; on a real installation use init-admin.");
            return FlowzaCli.Refused;
        }

        IDemoDataSet[] sets = [new IdentityDemoData(settings)];
        try
        {
            var exitCode = await DemoDataRunner.RunAsync(sets, output, error);
            if (exitCode == FlowzaCli.Ok)
            {
                await output.WriteLineAsync($"Every demo user logs in with the demo password: {DemoUsers.Password}");
                await output.WriteLineAsync("(documented in docs/setup/local-setup.md; never use demo data on a real installation)");
            }

            return exitCode;
        }
        finally
        {
            foreach (var set in sets)
            {
                await set.DisposeAsync();
            }
        }
    }

    /// <summary>The environment as .NET reads it: DOTNET_ENVIRONMENT, else ASPNETCORE_ENVIRONMENT; nothing set is Production.</summary>
    private static bool IsDevelopment(IReadOnlyDictionary<string, string?>? settings)
    {
        string? Read(string name) => settings is not null && settings.TryGetValue(name, out var value) ? value : Environment.GetEnvironmentVariable(name);

        var environment = Read("DOTNET_ENVIRONMENT") ?? Read("ASPNETCORE_ENVIRONMENT");
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
