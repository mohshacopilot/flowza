using Flowza.Common.Hosting;
using Flowza.ServiceDefaults.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowza.BuildingBlocks.UnitTests.ServiceDefaults;

/// <summary>T034: the start-up steps run in order, wait for the database when it is down, and retry (hand-written fakes).</summary>
public sealed class StartupStepRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Steps_run_in_order_during_start_when_the_database_answers()
    {
        var log = new List<string>();
        using var runner = Runner(new FakeHealth(healthy: true), new Step("messaging", 100, log), new Step("migration", 0, log), new Step("deny list", 200, log));

        await runner.StartAsync(Ct);

        log.ShouldBe(["run migration", "run messaging", "run deny list"]);
        runner.WaitingFor.ShouldBeNull();
    }

    [Fact]
    public async Task A_step_that_fails_while_the_database_answers_stops_the_start()
    {
        using var runner = Runner(new FakeHealth(healthy: true), new Step("broken", 0, [], failures: int.MaxValue));

        await Should.ThrowAsync<InvalidOperationException>(() => runner.StartAsync(Ct));
    }

    [Fact]
    public async Task PLT_05_without_the_database_the_start_returns_at_once_and_the_steps_run_when_it_answers()
    {
        var log = new List<string>();
        var health = new FakeHealth(healthy: false);
        using var runner = Runner(health, new Step("migration", 0, log), new Step("messaging", 100, log));

        await runner.StartAsync(Ct);
        log.ShouldBeEmpty("nothing that needs the database runs while it is down");
        runner.WaitingFor.ShouldBe("migration");
        (await new StartupHealth(runner).CheckAsync()).Status.ShouldBe(HealthStatus.Unhealthy);

        health.Healthy = true;
        await WaitUntilAsync(() => runner.WaitingFor is null);

        log.ShouldBe(["run migration", "run messaging"]);
        (await new StartupHealth(runner).CheckAsync()).Status.ShouldBe(HealthStatus.Healthy);
        await runner.StopAsync(Ct);
    }

    [Fact]
    public async Task A_step_that_fails_after_an_outage_is_tried_again_until_it_succeeds()
    {
        var log = new List<string>();
        var health = new FakeHealth(healthy: false);
        using var runner = Runner(health, new Step("deny list", 200, log, failures: 2));

        await runner.StartAsync(Ct);
        health.Healthy = true;
        await WaitUntilAsync(() => runner.WaitingFor is null);

        log.ShouldBe(["fail deny list", "fail deny list", "run deny list"]);
        await runner.StopAsync(Ct);
    }

    [Fact]
    public async Task Stopping_stops_the_steps_that_ran_in_reverse_order()
    {
        var log = new List<string>();
        using var runner = Runner(new FakeHealth(healthy: true), new Step("migration", 0, log), new Step("messaging", 100, log));
        await runner.StartAsync(Ct);
        log.Clear();

        await runner.StopAsync(Ct);

        log.ShouldBe(["stop messaging", "stop migration"]);
    }

    [Fact]
    public async Task Without_steps_the_service_is_ready_at_once()
    {
        using var runner = Runner(new FakeHealth(healthy: false));

        await runner.StartAsync(Ct);

        runner.WaitingFor.ShouldBeNull();
    }

    [Fact]
    public async Task Review_1_a_step_that_failed_part_way_and_cannot_be_retried_stops_the_service_and_never_becomes_ready()
    {
        var log = new List<string>();
        var health = new FakeHealth(healthy: false);
        var lifetime = new FakeLifetime();
        using var runner = Runner(health, lifetime, new Step("messaging", 100, log, failures: 1, canRetryAfterFailure: false));
        var exitCodeBefore = Environment.ExitCode;
        try
        {
            await runner.StartAsync(Ct);
            health.Healthy = true;
            await WaitUntilAsync(() => lifetime.StopRequested);

            log.ShouldBe(["fail messaging"], "run once, never again");
            runner.WaitingFor.ShouldBe("messaging", "never ready with messaging half started");
            Environment.ExitCode.ShouldBe(1, "the service manager sees a failure and starts the service again");
        }
        finally
        {
            Environment.ExitCode = exitCodeBefore;
        }
    }

    [Fact]
    public async Task Review_2_stopping_also_stops_a_step_that_failed_part_way()
    {
        var log = new List<string>();
        var health = new FakeHealth(healthy: false);
        using var runner = Runner(health, new Step("migration", 0, log), new Step("messaging", 100, log, failures: int.MaxValue));
        await runner.StartAsync(Ct);
        health.Healthy = true;
        await WaitUntilAsync(() => log.Contains("fail messaging"));

        await runner.StopAsync(Ct);

        log.ShouldContain("stop messaging", customMessage: "what it started part-way is stopped too");
        log.IndexOf("stop messaging").ShouldBeLessThan(log.IndexOf("stop migration"), "newest first");
    }

    private static StartupStepRunner Runner(HealthCheckService health, params IStartupStep[] steps) =>
        Runner(health, new FakeLifetime(), steps);

    private static StartupStepRunner Runner(HealthCheckService health, IHostApplicationLifetime lifetime, params IStartupStep[] steps) =>
        new(steps, health, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [StartupStepRunner.MaxRetryDelayKey] = "00:00:00.050" })
            .Build(), lifetime, NullLogger<StartupStepRunner>.Instance);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(25, Ct);
        }

        condition().ShouldBeTrue();
    }

    private sealed class Step(string name, int order, List<string> log, int failures = 0, bool canRetryAfterFailure = true) : IStartupStep
    {
        private int _failuresLeft = failures;
        private bool _failed;

        public int Order => order;

        public string Name => name;

        public bool CanRetry => canRetryAfterFailure || !_failed;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            lock (log)
            {
                if (_failuresLeft > 0)
                {
                    _failuresLeft--;
                    _failed = true;
                    log.Add($"fail {name}");
                    throw new InvalidOperationException($"{name} failed");
                }

                log.Add($"run {name}");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (log)
            {
                log.Add($"stop {name}");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopRequested = true;
    }

    /// <summary>The database and transport checks: healthy or not, as the test says.</summary>
    private sealed class FakeHealth(bool healthy) : HealthCheckService
    {
        public bool Healthy { get; set; } = healthy;

        public override Task<HealthReport> CheckHealthAsync(Func<HealthCheckRegistration, bool>? predicate, CancellationToken cancellationToken = default)
        {
            var status = Healthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            var entries = new Dictionary<string, HealthReportEntry> { ["database"] = new(status, null, TimeSpan.Zero, null, null) };
            return Task.FromResult(new HealthReport(entries, TimeSpan.Zero));
        }
    }

    /// <summary>The "startup" readiness check as the host runs it.</summary>
    private sealed class StartupHealth(StartupStepRunner runner)
    {
        public Task<HealthCheckResult> CheckAsync() => new StartupStepsHealthCheck(runner).CheckHealthAsync(new HealthCheckContext(), Ct);
    }
}
