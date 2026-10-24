using Flowza.Common.Domain;
using Flowza.Identity.Application.Events;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.UnitTests.Events;

/// <summary>Every domain event of Identity leaves the service as its integration event (plan 001 §7, plan 002 §6).</summary>
public sealed class IdentityEventMapperTests
{
    private static readonly Guid s_id = Guid.NewGuid();

    public static TheoryData<Type> DomainEventTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in typeof(User).Assembly.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false }))
        {
            data.Add(type);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DomainEventTypes))]
    public void Every_domain_event_maps_to_the_integration_event_of_the_same_name_with_the_same_ids(Type domainEventType)
    {
        var domainEvent = Create(domainEventType);

        var mapped = IdentityEventMapper.Map(domainEvent);

        mapped.GetType().Name.ShouldBe(domainEventType.Name.Replace("DomainEvent", string.Empty, StringComparison.Ordinal));
        foreach (var property in domainEventType.GetProperties().Where(p => p.Name != "EqualityContract"))
        {
            var target = mapped.GetType().GetProperty(property.Name);
            target.ShouldNotBeNull($"{mapped.GetType().Name} lacks {property.Name}");
            target.GetValue(mapped).ShouldBe(property.GetValue(domainEvent));
        }
    }

    [Fact]
    public void The_sixteen_org_domain_events_are_among_them()
    {
        DomainEventTypes().Count.ShouldBe(7 + 16); // the 7 user events of feature 001 and the 16 of plan 002 §6
    }

    [Fact]
    public void An_unknown_domain_event_is_a_programming_error()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => IdentityEventMapper.Map(new UnknownDomainEvent()));
    }

    // Builds an event with sample values: the same ID for Guid parameters, a one-item list for lists.
    private static IDomainEvent Create(Type type)
    {
        var constructor = type.GetConstructors().Single();
        var arguments = constructor.GetParameters().Select(p => p.ParameterType switch
        {
            var t when t == typeof(Guid) || t == typeof(Guid?) => (object?)s_id,
            var t when t == typeof(IReadOnlyList<Guid>) => s_ids,
            var t when t == typeof(IReadOnlyList<string>) => s_names,
            var t => throw new InvalidOperationException($"No sample value for {t.Name}."),
        }).ToArray();
        return (IDomainEvent)constructor.Invoke(arguments);
    }

    private static readonly IReadOnlyList<Guid> s_ids = [s_id];
    private static readonly IReadOnlyList<string> s_names = ["User"];

    private sealed record UnknownDomainEvent : IDomainEvent;
}
