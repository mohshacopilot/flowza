namespace Flowza.Cli.DemoData;

/// <summary>
/// The demo data of one service (PLT-07; Strategy + registry: a feature that adds demo data adds a class, the runner
/// does not change). Feature 001 has Identity's demo users; ORG, apps and processes follow with features 002–005.
/// </summary>
public interface IDemoDataSet : IAsyncDisposable
{
    /// <summary>The service whose database this set fills, for the console text (e.g. "identity").</summary>
    string Service { get; }

    /// <summary>Whether the service's database already holds business data; then the tool must refuse (PLT-07-AC2).</summary>
    Task<bool> HasDataAsync(CancellationToken cancellationToken);

    /// <summary>Loads the set in one transaction of its own service; returns a line for the console, or the reason it failed.</summary>
    Task<DemoDataResult> LoadAsync(CancellationToken cancellationToken);
}

public sealed record DemoDataResult(bool Loaded, string Message);
