using Flowza.Cli;
using Flowza.Cli.DemoData;

namespace Flowza.Identity.IntegrationTests.Cli;

/// <summary>T028 / PLT-07-AC2: the demo data runner across services, with hand-written fake sets (no database).</summary>
public sealed class DemoDataRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PLT_07_AC2_when_any_service_has_data_nothing_is_loaded_anywhere()
    {
        var identity = new FakeSet("identity", hasData: false);
        var runtime = new FakeSet("runtime", hasData: true);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await DemoDataRunner.RunAsync([identity, runtime], output, error, Ct);

        exitCode.ShouldBe(FlowzaCli.Refused);
        identity.Loaded.ShouldBeFalse("checked before anything is loaded");
        runtime.Loaded.ShouldBeFalse();
        error.ToString().ShouldContain("runtime database already has data");
    }

    [Fact]
    public async Task PLT_07_empty_databases_are_loaded_in_order()
    {
        var order = new List<string>();
        var sets = new[] { new FakeSet("identity", false, order), new FakeSet("design", false, order) };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await DemoDataRunner.RunAsync(sets, output, error, Ct);

        exitCode.ShouldBe(FlowzaCli.Ok);
        order.ShouldBe(["identity", "design"]);
    }

    [Fact]
    public async Task A_failing_set_stops_the_run_and_says_which_service()
    {
        var order = new List<string>();
        var sets = new[] { new FakeSet("identity", false, order, fails: true), new FakeSet("design", false, order) };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await DemoDataRunner.RunAsync(sets, output, error, Ct);

        exitCode.ShouldBe(FlowzaCli.Refused);
        order.ShouldBe(["identity"]);
        error.ToString().ShouldStartWith("identity:");
    }

    [Fact]
    public async Task No_sets_is_a_successful_empty_run()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        (await DemoDataRunner.RunAsync([], output, error, Ct)).ShouldBe(FlowzaCli.Ok);
    }

    private sealed class FakeSet(string service, bool hasData, List<string>? order = null, bool fails = false) : IDemoDataSet
    {
        public string Service => service;

        public bool Loaded { get; private set; }

        public Task<bool> HasDataAsync(CancellationToken cancellationToken) => Task.FromResult(hasData);

        public Task<DemoDataResult> LoadAsync(CancellationToken cancellationToken)
        {
            Loaded = true;
            order?.Add(service);
            return Task.FromResult(fails ? new DemoDataResult(false, "failed") : new DemoDataResult(true, "loaded"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
