using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.UnitTests.Organization;

public sealed class BusinessRoleTests
{
    private static BusinessRole NewRole() => BusinessRole.Create(Guid.NewGuid(), "credit_officer", "Credit Officer").Value;

    [Fact]
    public void ORG_05_AC1_a_role_is_created_active_with_its_code_and_name()
    {
        var role = NewRole();

        role.Code.Value.ShouldBe("CREDIT_OFFICER");
        role.Name.ShouldBe("Credit Officer");
        role.IsActive.ShouldBeTrue();
        role.DomainEvents.ShouldBe([new BusinessRoleCreatedDomainEvent(role.Id)]);
    }

    [Fact]
    public void ORG_05_AC1_renaming_keeps_the_code_and_nothing_outside_can_change_it()
    {
        var role = NewRole();
        role.ClearDomainEvents();

        role.Rename("Loan Officer").IsSuccess.ShouldBeTrue();

        role.Code.Value.ShouldBe("CREDIT_OFFICER");
        role.DomainEvents.ShouldBe([new BusinessRoleRenamedDomainEvent(role.Id)]);
        typeof(BusinessRole).GetProperty(nameof(BusinessRole.Code))!.SetMethod!.IsPublic.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CREDIT OFFICER")]
    [InlineData("CREDIT.OFFICER")]
    [InlineData("123456789012345678901234567890123456789012345678901")] // 51 characters
    public void ORG_05_AC3_a_role_code_outside_the_format_is_rejected(string? code)
    {
        BusinessRole.Create(Guid.NewGuid(), code, "Credit Officer").Error.ShouldBe(OrgErrors.InvalidCode);
    }

    [Fact]
    public void ORG_05_AC3_a_role_code_entered_in_lower_case_is_stored_in_upper_case()
    {
        NewRole().Code.ShouldBe(OrgCode.Create("CREDIT_OFFICER").Value);
    }

    [Fact]
    public void A_role_needs_a_name()
    {
        BusinessRole.Create(Guid.NewGuid(), "CREDIT_OFFICER", " ").Error.ShouldBe(OrgErrors.InvalidName);
        NewRole().Rename(null).Error.ShouldBe(OrgErrors.InvalidName);
    }

    [Fact]
    public void ORG_05_AC5_a_deactivated_role_can_be_reactivated()
    {
        var role = NewRole();
        role.ClearDomainEvents();

        role.Deactivate().IsSuccess.ShouldBeTrue();
        role.IsActive.ShouldBeFalse();

        role.Activate().IsSuccess.ShouldBeTrue();
        role.IsActive.ShouldBeTrue();

        role.DomainEvents.ShouldBe([new BusinessRoleDeactivatedDomainEvent(role.Id), new BusinessRoleActivatedDomainEvent(role.Id)]);
    }

    [Fact]
    public void Deactivating_or_activating_twice_raises_one_event()
    {
        var role = NewRole();
        role.ClearDomainEvents();

        role.Activate();
        role.Deactivate();
        role.Deactivate();

        role.DomainEvents.ShouldBe([new BusinessRoleDeactivatedDomainEvent(role.Id)]);
    }
}
