namespace Flowza.Cli;

/// <summary>
/// The <c>flowza</c> command-line tool (plan 001 PA5): installation steps that must not be web pages. Commands:
/// <c>init-admin</c> (IAM-03, task T019); <c>demo-data</c> arrives in task T028.
/// </summary>
public static class FlowzaCli
{
    public const int Ok = 0;
    public const int Refused = 1;
    public const int UsageError = 2;

    /// <param name="args">Command and options, e.g. <c>init-admin --email admin@bank.example</c>.</param>
    /// <param name="output">Where results go (the console); the only place a generated password is ever written.</param>
    /// <param name="error">Where problems go.</param>
    /// <param name="settings">Extra configuration (tests); otherwise environment variables and the flowza-dev user secrets.</param>
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, IReadOnlyDictionary<string, string?>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            await WriteUsageAsync(output);
            return args.Length == 0 ? UsageError : Ok;
        }

        var options = CliOptions.Parse(args.AsSpan(1));
        if (options is null)
        {
            await error.WriteLineAsync("Options are written as --name value.");
            return UsageError;
        }

        switch (args[0])
        {
            case "init-admin":
                return await InitAdminCommand.RunAsync(options, output, error, settings);
            case "demo-data":
                await error.WriteLineAsync("demo-data is not available yet (task T028).");
                return UsageError;
            default:
                await error.WriteLineAsync($"Unknown command '{args[0]}'.");
                await WriteUsageAsync(error);
                return UsageError;
        }
    }

    private static async Task WriteUsageAsync(TextWriter writer)
    {
        await writer.WriteLineAsync("Usage: flowza <command> [options]");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("Commands:");
        await writer.WriteLineAsync("  init-admin --email <address> [--username admin] [--display-name \"Platform Admin\"]");
        await writer.WriteLineAsync("      Creates the first Platform Admin of a new installation and prints a one-time password.");
        await writer.WriteLineAsync("  demo-data   (task T028)");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("The database comes from ConnectionStrings__identity-db (and the message transport settings of the services),");
        await writer.WriteLineAsync("or on a developer PC from the flowza-dev user secrets (docs/setup/set-secrets.ps1).");
    }
}

/// <summary><c>--name value</c> options, names without the dashes, ignoring letter case.</summary>
public sealed class CliOptions
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public string? this[string name] => _values.TryGetValue(name, out var value) ? value : null;

    public static CliOptions? Parse(ReadOnlySpan<string> args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                return null;
            }

            options._values[args[i][2..]] = args[i + 1];
        }

        return options;
    }
}
