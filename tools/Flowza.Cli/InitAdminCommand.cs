using Flowza.Common.Results;
using Flowza.Identity.Application.Users.InitializeAdmin;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Flowza.Cli;

/// <summary>
/// <c>flowza init-admin</c> (IAM-03, C2; plan 001 §5.4): creates the Platform Admin if none exists, with a strong
/// random password printed <b>once</b> to the console (never to a log) that must be changed at the first login.
/// </summary>
public static class InitAdminCommand
{
    public const string DefaultUsername = "admin";
    public const string DefaultDisplayName = "Platform Admin";

    public static async Task<int> RunAsync(CliOptions options, TextWriter output, TextWriter error, IReadOnlyDictionary<string, string?>? settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (string.IsNullOrWhiteSpace(options["email"]))
        {
            await error.WriteLineAsync("init-admin needs --email <address> (every user has an email, C3).");
            return FlowzaCli.UsageError;
        }

        var command = new InitializeAdminCommand(
            options["username"] ?? DefaultUsername,
            options["display-name"] ?? DefaultDisplayName,
            options["email"]);

        using var host = CliHost.Build(settings);
        await host.StartAsync();
        try
        {
            var bus = host.Services.GetRequiredService<IMessageBus>();
            var result = await bus.InvokeAsync<Result<InitializedAdmin>>(command);
            if (result.IsFailure)
            {
                await error.WriteLineAsync(result.Error.Message);
                return FlowzaCli.Refused;
            }

            await output.WriteLineAsync($"Platform Admin '{result.Value.Username}' created.");
            await output.WriteLineAsync($"One-time password: {result.Value.TemporaryPassword}");
            await output.WriteLineAsync("Write it down now: it is not stored or shown again. It must be changed at the first login.");
            return FlowzaCli.Ok;
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
