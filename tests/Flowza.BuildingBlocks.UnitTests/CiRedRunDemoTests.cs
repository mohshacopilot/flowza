namespace Flowza.BuildingBlocks.UnitTests;

/// <summary>
/// PLT-09-AC1 demonstration only (branch ci/red-run-demo, never merged): a failing test must turn the CI run red.
/// </summary>
public sealed class CiRedRunDemoTests
{
    [Fact]
    public void PLT_09_AC1_demo_a_failing_test_turns_CI_red() => (1 + 1).ShouldBe(3);
}
