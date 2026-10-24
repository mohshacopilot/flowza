using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.UnitTests.Organization;

public sealed class ManagerChainTests
{
    private static readonly Guid Ravi = Guid.NewGuid();
    private static readonly Guid Arun = Guid.NewGuid();
    private static readonly Guid Suresh = Guid.NewGuid();
    private static readonly Guid Priya = Guid.NewGuid();

    private static ManagerChain Walk(Guid start, Dictionary<Guid, Guid?> managers) =>
        ManagerChain.Walk(start, id => managers.GetValueOrDefault(id));

    private static readonly Dictionary<Guid, Guid?> Lines = new()
    {
        [Ravi] = Arun,
        [Arun] = Suresh,
        [Suresh] = Priya,
        [Priya] = null,
    };

    [Fact]
    public void A_user_without_a_manager_has_an_empty_chain()
    {
        var chain = Walk(Priya, Lines);

        chain.Managers.ShouldBeEmpty();
        chain.IsBroken.ShouldBeFalse();
        chain.ManagerAt(1).ShouldBeNull();
        chain.Contains(Priya).ShouldBeTrue(); // the start belongs to its own chain
    }

    [Fact]
    public void The_chain_lists_the_managers_nearest_first()
    {
        var chain = Walk(Ravi, Lines);

        chain.Managers.ShouldBe([Arun, Suresh, Priya]);
        chain.IsBroken.ShouldBeFalse();
        chain.ManagerAt(1).ShouldBe(Arun);
        chain.ManagerAt(3).ShouldBe(Priya);
    }

    [Fact]
    public void A_level_beyond_the_top_of_the_chain_has_no_manager()
    {
        Walk(Ravi, Lines).ManagerAt(4).ShouldBeNull();
    }

    [Fact]
    public void Levels_start_at_one()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Walk(Ravi, Lines).ManagerAt(0));
    }

    [Fact]
    public void A_loop_in_the_data_ends_the_walk_and_marks_the_chain_broken()
    {
        var looping = new Dictionary<Guid, Guid?> { [Ravi] = Arun, [Arun] = Suresh, [Suresh] = Arun };

        var chain = Walk(Ravi, looping);

        chain.Managers.ShouldBe([Arun, Suresh]);
        chain.IsBroken.ShouldBeTrue();
    }

    [Fact]
    public void A_user_who_is_their_own_manager_in_the_data_is_a_broken_chain()
    {
        Walk(Ravi, new() { [Ravi] = Ravi }).IsBroken.ShouldBeTrue();
    }

    [Fact]
    public void A_chain_longer_than_the_limit_stops_and_is_broken()
    {
        var users = Enumerable.Range(0, ManagerChain.MaxLength + 2).Select(_ => Guid.NewGuid()).ToList();
        var managers = users.Zip(users.Skip(1), (user, manager) => (user, manager)).ToDictionary(p => p.user, p => (Guid?)p.manager);

        var chain = Walk(users[0], managers);

        chain.Managers.Count.ShouldBe(ManagerChain.MaxLength);
        chain.IsBroken.ShouldBeTrue();
    }

    [Fact]
    public void A_chain_exactly_at_the_limit_is_complete()
    {
        var users = Enumerable.Range(0, ManagerChain.MaxLength + 1).Select(_ => Guid.NewGuid()).ToList();
        var managers = users.Zip(users.Skip(1), (user, manager) => (user, manager)).ToDictionary(p => p.user, p => (Guid?)p.manager);

        var chain = Walk(users[0], managers);

        chain.Managers.Count.ShouldBe(ManagerChain.MaxLength);
        chain.IsBroken.ShouldBeFalse();
    }

    [Fact]
    public void The_builder_stops_after_the_walk_has_ended()
    {
        var builder = new ManagerChain.Builder(Ravi);

        builder.TryAdd(Arun).ShouldBeTrue();
        builder.Current.ShouldBe(Arun);
        builder.TryAdd(null).ShouldBeFalse(); // the top
        builder.TryAdd(Suresh).ShouldBeFalse(); // nothing is added after the end

        builder.Build().Managers.ShouldBe([Arun]);
    }
}
