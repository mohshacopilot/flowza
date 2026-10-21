namespace Flowza.BuildingBlocks.UnitTests.Common;

/// <summary>A clock the test controls (no extra library needed).</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}
