namespace Flowza.Cli.DemoData;

/// <summary>
/// Runs the demo data sets of all services (PLT-07). First every service is asked whether it has data; if any has,
/// the tool refuses and changes nothing (PLT-07-AC2). Only then are the sets loaded, in order. Services have their
/// own databases and there are no distributed transactions (AR1): if a later set fails, the earlier ones stay and a
/// second run refuses; start again from empty databases.
/// </summary>
public static class DemoDataRunner
{
    public static async Task<int> RunAsync(
        IReadOnlyList<IDemoDataSet> sets,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var withData = new List<string>();
        foreach (var set in sets)
        {
            if (await set.HasDataAsync(cancellationToken))
            {
                withData.Add(set.Service);
            }
        }

        if (withData.Count > 0)
        {
            await error.WriteLineAsync(
                $"Refused: the {string.Join(", ", withData)} database already has data. Demo data is only loaded into empty databases; nothing was changed.");
            return FlowzaCli.Refused;
        }

        foreach (var set in sets)
        {
            var result = await set.LoadAsync(cancellationToken);
            if (!result.Loaded)
            {
                await error.WriteLineAsync($"{set.Service}: {result.Message}");
                return FlowzaCli.Refused;
            }

            await output.WriteLineAsync($"{set.Service}: {result.Message}");
        }

        return FlowzaCli.Ok;
    }
}
