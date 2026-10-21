using System.Data.SqlTypes;
using Flowza.Common.Ids;
using Flowza.Testing;

namespace Flowza.BuildingBlocks.UnitTests.Common;

public sealed class SequentialGuidGeneratorTests
{
    private static readonly DateTimeOffset s_start = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void T3_D3_guids_sort_in_creation_order_the_way_SQL_Server_sorts_them()
    {
        var created = Enumerable.Range(0, 20_000).Select(_ => SequentialGuidGenerator.Shared.NewGuid()).ToList();

        var sortedLikeSqlServer = created.OrderBy(g => new SqlGuid(g)).ToList();

        sortedLikeSqlServer.ShouldBe(created);
    }

    [Fact]
    public void T3_D3_guids_made_in_the_same_millisecond_still_sort_in_creation_order()
    {
        var generator = new SequentialGuidGenerator(new ManualTimeProvider(s_start));

        var created = Enumerable.Range(0, 1_000).Select(_ => generator.NewGuid()).ToList();

        created.OrderBy(g => new SqlGuid(g)).ShouldBe(created);
    }

    [Fact]
    public void T3_D3_order_holds_when_more_than_65536_guids_are_made_in_one_millisecond()
    {
        var generator = new SequentialGuidGenerator(new ManualTimeProvider(s_start));

        var created = Enumerable.Range(0, 70_000).Select(_ => generator.NewGuid()).ToList();

        created.OrderBy(g => new SqlGuid(g)).ShouldBe(created);
    }

    [Fact]
    public void T3_D3_order_holds_when_the_clock_moves_backwards()
    {
        var clock = new ManualTimeProvider(s_start);
        var generator = new SequentialGuidGenerator(clock);

        var first = generator.NewGuid();
        clock.Now = s_start.AddSeconds(-5);
        var second = generator.NewGuid();

        new SqlGuid(second).CompareTo(new SqlGuid(first)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void T3_D3_guids_are_unique()
    {
        var created = Enumerable.Range(0, 50_000).Select(_ => SequentialGuidGenerator.Shared.NewGuid()).ToList();

        created.Distinct().Count().ShouldBe(created.Count);
    }

    [Fact]
    public void T3_D3_later_time_sorts_after_earlier_time()
    {
        var clock = new ManualTimeProvider(s_start);
        var generator = new SequentialGuidGenerator(clock);

        var earlier = generator.NewGuid();
        clock.Now = s_start.AddMilliseconds(1);
        var later = generator.NewGuid();

        new SqlGuid(later).CompareTo(new SqlGuid(earlier)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Constructor_rejects_a_missing_clock()
    {
        Should.Throw<ArgumentNullException>(() => new SequentialGuidGenerator(null!));
    }
}
