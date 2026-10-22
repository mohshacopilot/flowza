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
}
