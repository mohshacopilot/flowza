using Flowza.Common.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowza.ServiceDefaults.Hosting;

/// <summary>
/// Runs the service's <see cref="IStartupStep"/>s (T034). When the database answers at start-up the steps run during
/// start, as before, and a failing step stops the start (a real error, e.g. wrong settings). When the database does
/// not answer, the service starts anyway: it is live but not ready, and the steps run in the background as soon as
/// the other readiness checks (database, message transport) are healthy, retried with a growing pause (1 s, 2 s,
/// 4 s … at most <see cref="MaxRetryDelayKey"/>). The readiness check "startup" reports which step it waits for.
/// A step that failed part-way and cannot be run again (<see cref="IStartupStep.CanRetry"/>) stops the service with
/// exit code 1, so the service manager starts it again cleanly; it never reports ready with that step half done.
/// </summary>
public sealed partial class StartupStepRunner(
    IEnumerable<IStartupStep> steps,
    HealthCheckService healthChecks,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<StartupStepRunner> logger) : IHostedService, IStartupStepRunner, IDisposable
{
    public const string HealthCheckName = "startup";

    /// <summary>Configuration key: longest pause between two attempts (default 30 seconds).</summary>
    public const string MaxRetryDelayKey = "Flowza:Startup:MaxRetryDelay";

    private readonly IStartupStep[] _steps = [.. steps.OrderBy(s => s.Order)];
    private readonly List<IStartupStep> _completed = [];
    private readonly List<IStartupStep> _entered = [];
    private readonly CancellationTokenSource _stopping = new();
    private Task? _background;
    private volatile string? _waitingFor;

    /// <summary>Null when every step has run; otherwise the step the service waits for.</summary>
    public string? WaitingFor => _waitingFor;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _waitingFor = _steps.Length > 0 ? _steps[0].Name : null;
        if (_steps.Length == 0)
        {
            return;
        }

        if (await FirstCheckAsync(cancellationToken))
        {
            // The usual case: everything is there, so start as always; a failure here is a real error.
            await RunPendingAsync(cancellationToken);
            return;
        }

        LogWaiting(logger, _waitingFor!);
        _background = Task.Run(() => RunInBackgroundAsync(_stopping.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        if (_background is not null)
        {
            try
            {
                await _background.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Still running when the host's stop time ran out: do not stop a step while it runs (finding 2).
                LogStopTimedOut(logger);
                return;
            }
        }

        // Every step that was entered, also one that failed or was cancelled part-way, newest first (finding 2).
        IStartupStep[] entered;
        lock (_completed)
        {
            entered = [.. _entered];
        }

        foreach (var step in entered.Reverse())
        {
            await step.StopAsync(cancellationToken);
        }
    }

    public void Dispose() => _stopping.Dispose();

    private async Task RunInBackgroundAsync(CancellationToken stopping)
    {
        var maxDelay = configuration.GetValue(MaxRetryDelayKey, TimeSpan.FromSeconds(30));
        var delay = TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(1).Ticks, maxDelay.Ticks));
        while (!stopping.IsCancellationRequested && _waitingFor is not null)
        {
            try
            {
                if (await DependenciesHealthyAsync(stopping))
                {
                    await RunPendingAsync(stopping);
                    LogRecovered(logger);
                    return;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stopping.IsCancellationRequested)
            {
                var step = _steps.FirstOrDefault(s => s.Name == _waitingFor);
                if (step is { CanRetry: false })
                {
                    // Finding 1: never report ready with a step half done; a clean restart is the only safe way on.
                    LogStepFailedFinally(logger, step.Name, exception);
                    Environment.ExitCode = 1;
                    lifetime.StopApplication();
                    return;
                }

                LogStepFailed(logger, _waitingFor ?? "?", exception);
            }

            try
            {
                await Task.Delay(delay, stopping);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, maxDelay.Ticks));
        }
    }

    /// <summary>Runs the steps not yet done, in order; stops at the first failure (it throws).</summary>
    private async Task RunPendingAsync(CancellationToken cancellationToken)
    {
        foreach (var step in _steps)
        {
            lock (_completed)
            {
                if (_completed.Contains(step))
                {
                    continue;
                }
            }

            _waitingFor = step.Name;
            lock (_completed)
            {
                if (!_entered.Contains(step))
                {
                    _entered.Add(step);
                }
            }

            await step.RunAsync(cancellationToken);
            lock (_completed)
            {
                _completed.Add(step);
            }
        }

        _waitingFor = null;
    }

    /// <summary>The first check at start-up waits at most a few seconds, so a missing database does not hold up the start.</summary>
    private async Task<bool> FirstCheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            return await DependenciesHealthyAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>The readiness checks other than this one: the database and the message transport answer.</summary>
    private async Task<bool> DependenciesHealthyAsync(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(check => check.Name != HealthCheckName, cancellationToken);
        return report.Status == HealthStatus.Healthy;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Start-up waits for the database or message transport (next step: {Step}); the service is not ready yet")]
    private static partial void LogWaiting(ILogger logger, string step);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Start-up step {Step} failed; it is tried again")]
    private static partial void LogStepFailed(ILogger logger, string step, Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Start-up step {Step} failed part-way and cannot be run again; the service stops (exit code 1) so it can be started again cleanly")]
    private static partial void LogStepFailedFinally(ILogger logger, string step, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The service stopped while a start-up step was still running; its steps were not stopped")]
    private static partial void LogStopTimedOut(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Start-up completed after the database or message transport returned; the service is ready")]
    private static partial void LogRecovered(ILogger logger);
}

/// <summary>"Ready" only when every start-up step has run (T034).</summary>
public sealed class StartupStepsHealthCheck(StartupStepRunner runner) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(runner.WaitingFor is { } step
            ? HealthCheckResult.Unhealthy($"Start-up is waiting for: {step}")
            : HealthCheckResult.Healthy());
}
