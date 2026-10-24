using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.UnitTests.Organization;

/// <summary>The shared walk up of rules d and e (O3, O5, O13, P4) on the spec's example branch → region → zone → root.</summary>
public sealed class HeadWalkTests
{
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Region = Guid.NewGuid();
    private static readonly Guid Zone = Guid.NewGuid();
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid Arun = Guid.NewGuid();
    private static readonly Guid Suresh = Guid.NewGuid();
    private static readonly Guid Priya = Guid.NewGuid();
    private static readonly Guid Lakshmi = Guid.NewGuid();
    private static readonly Guid Ravi = Guid.NewGuid();

    private static UnitHead Unit(Guid unit, Guid? head, bool active = true) => new(unit, head, active);

    [Fact]
    public void The_start_units_active_head_is_found_at_once()
    {
        var result = HeadWalk.FindHead([Unit(Branch, Arun), Unit(Region, Suresh)], Ravi);

        result.Found.ShouldBeTrue();
        result.UnitId.ShouldBe(Branch);
        result.HeadUserId.ShouldBe(Arun);
        result.Skipped.ShouldBeEmpty();
    }

    [Fact]
    public void A_vacant_unit_is_passed_on_the_way_up()
    {
        var result = HeadWalk.FindHead([Unit(Branch, null), Unit(Region, Suresh)], Ravi);

        result.HeadUserId.ShouldBe(Suresh);
        result.Skipped.ShouldBe([new HeadSkip(Branch, HeadSkipReason.Vacant)]);
    }

    [Fact]
    public void A_unit_with_an_inactive_head_is_passed_on_the_way_up()
    {
        var result = HeadWalk.FindHead([Unit(Branch, Arun, active: false), Unit(Region, Suresh)], Ravi);

        result.HeadUserId.ShouldBe(Suresh);
        result.Skipped.ShouldBe([new HeadSkip(Branch, HeadSkipReason.HeadInactive)]);
    }

    [Fact]
    public void The_requester_never_approves_themselves_and_the_walk_goes_on()
    {
        var result = HeadWalk.FindHead([Unit(Branch, Arun), Unit(Region, Suresh)], requesterId: Arun);

        result.HeadUserId.ShouldBe(Suresh);
        result.Skipped.ShouldBe([new HeadSkip(Branch, HeadSkipReason.HeadIsRequester)]);
    }

    [Fact]
    public void The_requester_heading_several_units_on_the_way_is_skipped_at_each()
    {
        var result = HeadWalk.FindHead(
            [Unit(Branch, Arun), Unit(Region, null), Unit(Zone, Arun), Unit(Root, Lakshmi)],
            requesterId: Arun);

        result.UnitId.ShouldBe(Root);
        result.HeadUserId.ShouldBe(Lakshmi);
        result.Skipped.Select(s => s.Reason).ShouldBe([HeadSkipReason.HeadIsRequester, HeadSkipReason.Vacant, HeadSkipReason.HeadIsRequester]);
    }

    [Fact]
    public void Nobody_is_found_when_the_walk_passes_the_root_without_an_active_head_who_is_not_the_requester()
    {
        var result = HeadWalk.FindHead(
            [Unit(Branch, null), Unit(Region, Suresh, active: false), Unit(Zone, Priya), Unit(Root, null)],
            requesterId: Priya);

        result.Found.ShouldBeFalse();
        result.UnitId.ShouldBeNull();
        result.Skipped.Select(s => s.UnitId).ShouldBe([Branch, Region, Zone, Root]);
    }

    [Fact]
    public void An_empty_chain_finds_nobody()
    {
        var result = HeadWalk.FindHead([], Ravi);

        result.Found.ShouldBeFalse();
        result.Skipped.ShouldBeEmpty();
    }
}
