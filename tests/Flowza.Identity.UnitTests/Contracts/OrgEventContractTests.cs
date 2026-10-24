using System.Reflection;
using Flowza.Contracts;
using Flowza.Contracts.Identity.Events.V1;

namespace Flowza.Identity.UnitTests.Contracts;

/// <summary>The organization's integration events (plan 002 §6, PA3): IDs only, standard fields from the base.</summary>
public sealed class OrgEventContractTests
{
    private static readonly string[] s_orgEventNames =
    [
        nameof(UnitTypeCreated), nameof(UnitTypeRenamed), nameof(UnitTypeDeactivated), nameof(UnitTypeActivated),
        nameof(OrgUnitCreated), nameof(OrgUnitRenamed), nameof(OrgUnitHeadChanged), nameof(OrgUnitMoved),
        nameof(OrgUnitDeactivated), nameof(OrgUnitActivated),
        nameof(BusinessRoleCreated), nameof(BusinessRoleRenamed), nameof(BusinessRoleDeactivated), nameof(BusinessRoleActivated),
        nameof(UserOrgPlacementChanged), nameof(UserBusinessRolesChanged),
    ];

    private static IEnumerable<Type> OrgEvents() =>
        typeof(IntegrationEvent).Assembly.GetTypes().Where(t => t.Namespace == typeof(OrgUnitCreated).Namespace && s_orgEventNames.Contains(t.Name));

    [Fact]
    public void PA3_the_sixteen_org_events_exist_and_are_integration_events()
    {
        var events = OrgEvents().ToList();

        events.Select(t => t.Name).ShouldBe(s_orgEventNames, ignoreOrder: true);
        events.ShouldAllBe(t => t.IsSealed && typeof(IntegrationEvent).IsAssignableFrom(t));
    }

    [Fact]
    public void PA3_org_events_carry_ids_only()
    {
        foreach (var type in OrgEvents())
        {
            var own = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.DeclaringType == type && p.Name != "EqualityContract");
            foreach (var property in own)
            {
                var allowed = property.PropertyType == typeof(Guid)
                    || property.PropertyType == typeof(Guid?)
                    || property.PropertyType == typeof(IReadOnlyList<Guid>);
                allowed.ShouldBeTrue($"{type.Name}.{property.Name} is {property.PropertyType.Name}; org events carry IDs only (constitution 11.2)");
                property.Name.ShouldMatch("Ids?$", $"{type.Name}.{property.Name} names an ID");
            }
        }
    }
}
