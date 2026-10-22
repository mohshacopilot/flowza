namespace Flowza.Common.Hosting;

/// <summary>
/// Work a service does once at start-up that needs its database or message transport: the Development migration,
/// starting the messaging, loading the deny list, registering clients (T034; spec 001 edge case "a service starts
/// while its database is unreachable"). The host runs the steps in <see cref="Order"/> as soon as the database
/// answers, and again after a failure until they succeed; until then the service is up but not ready.
/// </summary>
public interface IStartupStep
{
    /// <summary>Lower runs first; see <see cref="StartupStepOrder"/>.</summary>
    int Order { get; }

    /// <summary>A short name for logs and the readiness answer, e.g. "messaging".</summary>
    string Name { get; }

    /// <summary>Does the work; an exception means "not yet", and the step is run again later.</summary>
    Task RunAsync(CancellationToken cancellationToken);

    /// <summary>Undoes what <see cref="RunAsync"/> started (e.g. stops the messaging) when the service stops.</summary>
    Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Present when the host runs the start-up steps itself (the services' runner waits for the database). Hosts without
/// it (the flowza tool, test hosts) run them during start, in order, as before T034.
/// </summary>
public interface IStartupStepRunner;

/// <summary>The order of the start-up steps: tables first, then what uses them.</summary>
public static class StartupStepOrder
{
    /// <summary>Creating or updating the tables (Development only).</summary>
    public const int Schema = 0;

    /// <summary>Starting the messaging (outbox, inbox, listeners).</summary>
    public const int Messaging = 100;

    /// <summary>Reading or writing data the service needs from the start.</summary>
    public const int Data = 200;
}
