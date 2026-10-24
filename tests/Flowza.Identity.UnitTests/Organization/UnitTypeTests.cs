using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.UnitTests.Organization;

public sealed class UnitTypeTests
{
    private static UnitType NewZone() => UnitType.Create(Guid.NewGuid(), "zone", "Zone").Value;

    [Fact]
    public void ORG_01_AC1_a_unit_type_is_created_active_with_its_code_and_name()
    {
        var zone = NewZone();

        zone.Code.Value.ShouldBe("ZONE");
        zone.Name.ShouldBe("Zone");
        zone.IsActive.ShouldBeTrue();
        zone.DomainEvents.ShouldBe([new UnitTypeCreatedDomainEvent(zone.Id)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ORG_01_AC1_a_unit_type_needs_a_name(string? name)
    {
        var result = UnitType.Create(Guid.NewGuid(), "ZONE", name);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(OrgErrors.InvalidName);
    }

    [Fact]
    public void A_name_has_at_most_100_characters()
    {
        UnitType.Create(Guid.NewGuid(), "ZONE", new string('a', OrgName.MaxLength)).IsSuccess.ShouldBeTrue();
        UnitType.Create(Guid.NewGuid(), "ZONE", new string('a', OrgName.MaxLength + 1)).Error.ShouldBe(OrgErrors.InvalidName);
    }

    [Fact]
    public void ORG_01_AC5_a_unit_type_code_outside_the_format_is_rejected()
    {
        UnitType.Create(Guid.NewGuid(), "SOUTH ZONE", "Zone").Error.ShouldBe(OrgErrors.InvalidCode);
    }

    [Fact]
    public void ORG_01_AC2_renaming_changes_the_name_and_keeps_the_code()
    {
        var zone = NewZone();
        zone.ClearDomainEvents();

        zone.Rename("  Zonal Office ").IsSuccess.ShouldBeTrue();

        zone.Name.ShouldBe("Zonal Office");
        zone.Code.Value.ShouldBe("ZONE");
        zone.DomainEvents.ShouldBe([new UnitTypeRenamedDomainEvent(zone.Id)]);
    }

    [Fact]
    public void ORG_01_AC2_nothing_outside_the_aggregate_can_change_the_code()
    {
        typeof(UnitType).GetProperty(nameof(UnitType.Code))!.SetMethod!.IsPublic.ShouldBeFalse();
        typeof(UnitType).GetMethods().ShouldNotContain(m => m.IsPublic && m.GetParameters().Any(p => p.ParameterType == typeof(OrgCode)));
    }

    [Fact]
    public void Renaming_to_the_same_name_or_an_invalid_name_raises_no_event()
    {
        var zone = NewZone();
        zone.ClearDomainEvents();

        zone.Rename("Zone").IsSuccess.ShouldBeTrue();
        zone.Rename("").Error.ShouldBe(OrgErrors.InvalidName);

        zone.Name.ShouldBe("Zone");
        zone.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ORG_01_AC3_a_unit_type_used_by_active_units_cannot_be_deactivated()
    {
        var zone = NewZone();
        zone.ClearDomainEvents();

        var result = zone.Deactivate(activeUnitsOfType: 2);

        result.Error.ShouldBe(OrgErrors.UnitTypeInUse);
        zone.IsActive.ShouldBeTrue();
        zone.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ORG_01_AC3_a_unit_type_without_active_units_can_be_deactivated()
    {
        var zone = NewZone();
        zone.ClearDomainEvents();

        zone.Deactivate(activeUnitsOfType: 0).IsSuccess.ShouldBeTrue();

        zone.IsActive.ShouldBeFalse();
        zone.DomainEvents.ShouldBe([new UnitTypeDeactivatedDomainEvent(zone.Id)]);
    }

    [Fact]
    public void ORG_01_AC6_a_deactivated_unit_type_can_be_reactivated()
    {
        var zone = NewZone();
        zone.Deactivate(activeUnitsOfType: 0);
        zone.ClearDomainEvents();

        zone.Activate().IsSuccess.ShouldBeTrue();

        zone.IsActive.ShouldBeTrue();
        zone.DomainEvents.ShouldBe([new UnitTypeActivatedDomainEvent(zone.Id)]);
    }

    [Fact]
    public void Deactivating_or_activating_twice_changes_nothing_the_second_time()
    {
        var zone = NewZone();
        zone.ClearDomainEvents();

        zone.Activate().IsSuccess.ShouldBeTrue();
        zone.Deactivate(activeUnitsOfType: 0);
        zone.Deactivate(activeUnitsOfType: 5).IsSuccess.ShouldBeTrue();

        zone.IsActive.ShouldBeFalse();
        zone.DomainEvents.ShouldBe([new UnitTypeDeactivatedDomainEvent(zone.Id)]);
    }
}
