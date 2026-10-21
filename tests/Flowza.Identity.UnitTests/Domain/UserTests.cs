using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.UnitTests.Domain;

public sealed class UserTests
{
    private static User NewUser(params SystemRole[] roles) =>
        User.Create(Guid.NewGuid(), "ravi.k", "Ravi Kumar", "ravi@bank.in", "+91 98765 43210", roles).Value;

    [Fact]
    public void A_new_user_is_active_must_set_a_password_and_has_the_User_role()
    {
        var user = NewUser();

        user.IsActive.ShouldBeTrue();
        user.MustChangePassword.ShouldBeTrue();
        user.Roles.ShouldBe([SystemRole.User]);
        user.DomainEvents.ShouldBe([new UserCreatedDomainEvent(user.Id)]);
    }

    [Fact]
    public void IAM_02_AC2_a_user_can_hold_several_system_roles()
    {
        var user = NewUser(SystemRole.AppDesigner, SystemRole.PlatformAdmin);

        user.Roles.ShouldBe([SystemRole.PlatformAdmin, SystemRole.AppDesigner, SystemRole.User], ignoreOrder: true);
        user.IsActivePlatformAdmin.ShouldBeTrue();
    }

    [Fact]
    public void IAM_02_AC2_the_User_role_cannot_be_removed()
    {
        var user = NewUser(SystemRole.AppDesigner);

        user.SetRoles([SystemRole.AppDesigner], activePlatformAdminCount: 1).IsSuccess.ShouldBeTrue();
        user.SetRoles([], activePlatformAdminCount: 1).IsSuccess.ShouldBeTrue();

        user.Roles.ShouldBe([SystemRole.User]);
    }

    [Fact]
    public void Changing_roles_raises_an_event_with_the_new_role_names_only_when_something_changed()
    {
        var user = NewUser();
        user.ClearDomainEvents();

        user.SetRoles([SystemRole.User], activePlatformAdminCount: 1);
        user.DomainEvents.ShouldBeEmpty();

        user.SetRoles([SystemRole.AppDesigner], activePlatformAdminCount: 1);
        var changed = user.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<UserRolesChangedDomainEvent>();
        changed.Roles.ShouldBe(["AppDesigner", "User"], ignoreOrder: true);
    }

    [Fact]
    public void IAM_02_AC2_the_last_active_Platform_Admin_cannot_lose_the_role()
    {
        var admin = NewUser(SystemRole.PlatformAdmin);

        var result = admin.SetRoles([SystemRole.User], activePlatformAdminCount: 1);

        result.Error.ShouldBe(UserErrors.LastActivePlatformAdmin);
        admin.HasRole(SystemRole.PlatformAdmin).ShouldBeTrue();
    }

    [Fact]
    public void IAM_02_AC2_the_last_active_Platform_Admin_cannot_be_deactivated()
    {
        var admin = NewUser(SystemRole.PlatformAdmin);

        var result = admin.Deactivate(activePlatformAdminCount: 1);

        result.Error.ShouldBe(UserErrors.LastActivePlatformAdmin);
        admin.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void IAM_02_AC2_an_admin_can_be_deactivated_or_lose_the_role_while_another_active_admin_exists()
    {
        var first = NewUser(SystemRole.PlatformAdmin);
        var second = NewUser(SystemRole.PlatformAdmin);

        first.Deactivate(activePlatformAdminCount: 2).IsSuccess.ShouldBeTrue();
        second.SetRoles([SystemRole.User], activePlatformAdminCount: 2).IsSuccess.ShouldBeTrue();

        first.IsActive.ShouldBeFalse();
        second.HasRole(SystemRole.PlatformAdmin).ShouldBeFalse();
    }

    [Fact]
    public void An_inactive_admin_can_lose_the_role_even_if_no_other_admin_is_active()
    {
        // Only active admins count for the lock-out protection (L3).
        var inactiveAdmin = NewUser(SystemRole.PlatformAdmin);
        inactiveAdmin.Deactivate(activePlatformAdminCount: 2);

        inactiveAdmin.SetRoles([], activePlatformAdminCount: 1).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IAM_04_AC5_a_deactivated_user_can_be_activated_again_with_roles_unchanged()
    {
        var user = NewUser(SystemRole.AppDesigner);
        user.Deactivate(activePlatformAdminCount: 1);
        user.ClearDomainEvents();

        var result = user.Activate();

        result.IsSuccess.ShouldBeTrue();
        user.IsActive.ShouldBeTrue();
        user.Roles.ShouldBe([SystemRole.AppDesigner, SystemRole.User], ignoreOrder: true);
        user.DomainEvents.ShouldBe([new UserActivatedDomainEvent(user.Id)]);
    }

    [Fact]
    public void Deactivating_or_activating_twice_changes_nothing_the_second_time()
    {
        var user = NewUser();
        user.Deactivate(1);
        user.ClearDomainEvents();

        user.Deactivate(1).IsSuccess.ShouldBeTrue();
        user.DomainEvents.ShouldBeEmpty();

        user.Activate();
        user.ClearDomainEvents();
        user.Activate().IsSuccess.ShouldBeTrue();
        user.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void IAM_04_AC1_the_profile_can_be_edited_but_the_username_stays()
    {
        var user = NewUser();
        user.ClearDomainEvents();

        var result = user.UpdateProfile("Ravi K.", "Ravi.K@Bank.in", null);

        result.IsSuccess.ShouldBeTrue();
        user.Username.Value.ShouldBe("ravi.k");
        user.DisplayName.ShouldBe("Ravi K.");
        user.Email.Value.ShouldBe("Ravi.K@Bank.in");
        user.NormalizedEmail.ShouldBe("RAVI.K@BANK.IN");
        user.Mobile.ShouldBeNull();
        user.DomainEvents.ShouldBe([new UserUpdatedDomainEvent(user.Id)]);
    }

    [Fact]
    public void Saving_an_unchanged_profile_raises_no_event()
    {
        var user = NewUser();
        user.ClearDomainEvents();

        user.UpdateProfile(" Ravi Kumar ", "ravi@bank.in", "+91 98765 43210").IsSuccess.ShouldBeTrue();

        user.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "ravi@bank.in", null, "displayName")]
    [InlineData("Ravi", "not-an-email", null, "email")]
    [InlineData("Ravi", "ravi@bank.in", "call me", "mobile")]
    [InlineData("Ravi", "ravi@bank.in", "+++", "mobile")]
    [InlineData("Ravi", "ravi@bank.in", "123456789012345678901", "mobile")]
    public void IAM_04_AC4_invalid_profile_values_are_rejected_with_the_field_named(
        string displayName, string email, string? mobile, string field)
    {
        var created = User.Create(Guid.NewGuid(), "ravi.k", displayName, email, mobile, []);

        created.IsFailure.ShouldBeTrue();
        created.Error.FieldErrors!.Keys.ShouldBe([field]);
    }

    [Fact]
    public void A_display_name_longer_than_the_limit_is_rejected()
    {
        var created = User.Create(Guid.NewGuid(), "ravi.k", new string('x', User.DisplayNameMaxLength + 1), "ravi@bank.in", null, []);

        created.Error.ShouldBe(UserErrors.InvalidDisplayName);
    }

    [Fact]
    public void IAM_04_AC2_a_reset_requires_a_password_change_and_a_change_clears_it()
    {
        var user = NewUser();
        var changedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.FromHours(5.5));

        user.PasswordChanged(changedAt);
        user.MustChangePassword.ShouldBeFalse();
        user.PasswordChangedAt.ShouldBe(changedAt);
        user.PasswordChangedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);

        user.ClearDomainEvents();
        user.PasswordReset();
        user.MustChangePassword.ShouldBeTrue();
        user.DomainEvents.ShouldBe([new UserPasswordResetDomainEvent(user.Id)]);
    }

    [Fact]
    public void System_roles_have_fixed_names_and_ids()
    {
        SystemRole.FromName("platformadmin").ShouldBe(SystemRole.PlatformAdmin);
        SystemRole.FromName("Unknown").ShouldBeNull();
        SystemRole.FromId(SystemRole.AppDesigner.Id).ShouldBe(SystemRole.AppDesigner);
        Should.Throw<ArgumentOutOfRangeException>(() => SystemRole.FromId(Guid.NewGuid()));
        SystemRole.All.Select(r => r.Id).Distinct().Count().ShouldBe(3);
    }
}
