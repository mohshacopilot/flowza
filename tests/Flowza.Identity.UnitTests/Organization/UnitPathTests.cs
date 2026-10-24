using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.UnitTests.Organization;

public sealed class UnitPathTests
{
    private static UnitPath PathOf(params int[] numbers)
    {
        var path = UnitPath.ForRoot(numbers[0]);
        foreach (var number in numbers.Skip(1))
        {
            path = path.Append(number).Value;
        }

        return path;
    }

    [Fact]
    public void The_root_alone_has_depth_one()
    {
        var root = UnitPath.ForRoot(1);

        root.Value.ShouldBe("/1/");
        root.Depth.ShouldBe(1);
        root.Numbers.ShouldBe([1]);
    }

    [Fact]
    public void A_child_path_adds_its_number_after_the_parent()
    {
        var path = PathOf(1, 4, 9, 23);

        path.Value.ShouldBe("/1/4/9/23/");
        path.Depth.ShouldBe(4);
        path.Numbers.ShouldBe([1, 4, 9, 23]);
    }

    [Fact]
    public void A_path_is_below_its_ancestors_and_itself_but_not_below_a_number_that_only_starts_the_same()
    {
        var unit = PathOf(1, 4, 9);

        unit.IsSameOrBelow(PathOf(1)).ShouldBeTrue();
        unit.IsSameOrBelow(PathOf(1, 4)).ShouldBeTrue();
        unit.IsSameOrBelow(unit).ShouldBeTrue();
        PathOf(1, 42).IsSameOrBelow(PathOf(1, 4)).ShouldBeFalse(); // the trailing '/' keeps 4 and 42 apart
        PathOf(1, 4).IsSameOrBelow(unit).ShouldBeFalse(); // an ancestor is not below its descendant
        PathOf(1, 5).IsSameOrBelow(PathOf(1, 4)).ShouldBeFalse(); // a sibling
    }

    [Fact]
    public void Rebase_replaces_the_moved_prefix_and_keeps_the_rest()
    {
        var grandchild = PathOf(1, 2, 3, 4);

        var moved = grandchild.Rebase(from: PathOf(1, 2, 3), to: PathOf(1, 6, 3));

        moved.Value.Value.ShouldBe("/1/6/3/4/");
    }

    [Fact]
    public void Rebase_of_a_path_outside_the_moved_unit_is_a_programming_error()
    {
        Should.Throw<ArgumentException>(() => PathOf(1, 5).Rebase(from: PathOf(1, 4), to: PathOf(1, 6, 4)));
    }

    [Fact]
    public void The_tree_has_at_most_fifty_levels()
    {
        var deepest = PathOf([.. Enumerable.Range(1, UnitPath.MaxDepth)]);

        deepest.Depth.ShouldBe(UnitPath.MaxDepth);
        deepest.Value.Length.ShouldBeLessThanOrEqualTo(UnitPath.MaxLength);
        deepest.Append(999).Error.ShouldBe(OrgErrors.TreeTooDeep);
    }

    [Fact]
    public void Rebase_that_would_end_deeper_than_fifty_levels_is_refused()
    {
        var deep = PathOf([.. Enumerable.Range(1, UnitPath.MaxDepth)]); // 50 levels: /1/2/…/50/
        var movedUnit = PathOf(1, 2); // its ancestor at level 2

        // Moving level 2 one level lower (under /1/99/) would push the deepest unit to level 51.
        deep.Rebase(from: movedUnit, to: PathOf(1, 99, 2)).Error.ShouldBe(OrgErrors.TreeTooDeep);
    }

    [Fact]
    public void Paths_with_the_largest_unit_numbers_fit_the_column()
    {
        var path = PathOf([.. Enumerable.Repeat(int.MaxValue, UnitPath.MaxDepth)]);

        path.Value.Length.ShouldBeLessThanOrEqualTo(UnitPath.MaxLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Unit_numbers_are_positive(int number)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => UnitPath.ForRoot(number));
        Should.Throw<ArgumentOutOfRangeException>(() => UnitPath.ForRoot(1).Append(number));
    }
}
