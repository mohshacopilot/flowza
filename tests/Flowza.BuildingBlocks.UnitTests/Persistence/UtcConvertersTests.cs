using Flowza.Persistence.Conventions;

namespace Flowza.BuildingBlocks.UnitTests.Persistence;

public sealed class UtcConvertersTests
{
    [Fact]
    public void Constitution_8_4_local_time_is_saved_as_utc()
    {
        var converter = new UtcDateTimeConverter();
        var local = new DateTime(2026, 10, 21, 15, 30, 0, DateTimeKind.Local);

        var stored = (DateTime)converter.ConvertToProvider(local)!;

        stored.ShouldBe(local.ToUniversalTime());
        stored.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void Constitution_8_4_unspecified_time_is_treated_as_utc_and_read_back_as_utc()
    {
        var converter = new UtcDateTimeConverter();
        var unspecified = new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Unspecified);

        var stored = (DateTime)converter.ConvertToProvider(unspecified)!;
        var read = (DateTime)converter.ConvertFromProvider(stored)!;

        stored.Ticks.ShouldBe(unspecified.Ticks);
        read.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void T3_D2_offset_time_keeps_its_instant_and_comes_back_in_utc()
    {
        var converter = new UtcDateTimeOffsetConverter();
        var india = new DateTimeOffset(2026, 10, 21, 15, 30, 0, TimeSpan.FromHours(5.5));

        var stored = (DateTime)converter.ConvertToProvider(india)!;
        var read = (DateTimeOffset)converter.ConvertFromProvider(stored)!;

        stored.ShouldBe(new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc));
        read.ShouldBe(india);
        read.Offset.ShouldBe(TimeSpan.Zero);
    }
}
