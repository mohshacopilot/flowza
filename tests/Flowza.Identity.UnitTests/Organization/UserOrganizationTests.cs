using Flowza.Common.Domain;
using Flowza.Common.Results;
using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.UnitTests.Organization;

/// <summary>A user's primary unit, reporting manager and business roles (ORG-04, ORG-05; spec 001 amendment 1).</summary>
public sealed class UserOrganizationTests
{
    private readonly UnitType _branch = UnitType.Create(Guid.NewGuid(), "BRANCH", "Branch").Value;
    private readonly OrgUnit _root;
    private readonly OrgUnit _tNagar;
    private readonly OrgUnit _adyar;
    private readonly BusinessRole _creditOfficer = BusinessRole.Create(Guid.NewGuid(), "CREDIT_OFFICER", "Credit Officer").Value;
    private readonly BusinessRole _reviewer = BusinessRole.Create(Guid.NewGuid(), "REVIEWER", "Reviewer").Value;

    public UserOrganizationTests()
    {
        _root = OrgUnit.CreateRoot(OrgRoot.UnitId, OrgRoot.UnitNumber, OrgCode.Create(OrgRoot.Code).Value, OrgRoot.Name, OrgRoot.UnitTypeId);
        _tNagar = OrgUnit.Create(Guid.NewGuid(), 2, "BR-TNAGAR", "T Nagar Branch", _branch, _root).Value;
        _adyar = OrgUnit.Create(Guid.NewGuid(), 3, "BR-ADYAR", "Adyar Branch", _branch, _root).Value;
    }

    private User NewUser(string username, OrgUnit? unit = null) =>
        User.Create(Guid.NewGuid(), username, username, $"{username}@example.org", null, [], unit ?? _tNagar).Value;

    /// <summary>The chain above <paramref name="user"/>, read from the given users' managers.</summary>
    private static ManagerChain ChainAbove(User user, params User[] users)
    {
        var managers = users.Append(user).DistinctBy(u => u.Id).ToDictionary(u => u.Id, u => u.ManagerId);
        return ManagerChain.Walk(user.Id, id => managers.GetValueOrDefault(id));
    }

    private static Result Report(User user, User? manager, params User[] everyone) =>
        user.SetManager(manager, manager is null ? null : ChainAbove(manager, everyone));

    [Fact]
    public void ORG_04_AC1_a_new_user_belongs_to_the_active_primary_unit_they_are_created_in()
    {
        var ravi = NewUser("ravi");

        ravi.PrimaryUnitId.ShouldBe(_tNagar.Id);
        ravi.ManagerId.ShouldBeNull();
        ravi.BusinessRoleIds.ShouldBeEmpty();
    }

    [Fact]
    public void ORG_04_AC6_creating_a_user_in_an_inactive_unit_is_refused()
    {
        _adyar.Deactivate(activeChildUnits: 0, activeUsers: 0);

        var created = User.Create(Guid.NewGuid(), "ravi", "Ravi", "ravi@example.org", null, [], _adyar);

        created.Error.ShouldBe(OrgErrors.PrimaryUnitInactive);
        created.Error!.FieldErrors!.Keys.ShouldContain("primaryUnitId");
    }

    [Fact]
    public void Until_task_T010_the_feature_001_form_puts_a_new_user_in_the_root_unit()
    {
        User.Create(Guid.NewGuid(), "ravi", "Ravi", "ravi@example.org", null, []).Value.PrimaryUnitId.ShouldBe(OrgRoot.UnitId);
    }

    [Fact]
    public void ORG_04_AC1_a_user_can_be_moved_only_to_an_active_unit()
    {
        var ravi = NewUser("ravi");
        ravi.ClearDomainEvents();
        _adyar.Deactivate(activeChildUnits: 0, activeUsers: 0);

        ravi.PlaceInUnit(_adyar).Error.ShouldBe(OrgErrors.PrimaryUnitInactive);
        ravi.PrimaryUnitId.ShouldBe(_tNagar.Id);

        ravi.PlaceInUnit(_root).IsSuccess.ShouldBeTrue();
        ravi.PlaceInUnit(_root).IsSuccess.ShouldBeTrue(); // the same unit again changes nothing
        ravi.PrimaryUnitId.ShouldBe(_root.Id);
        ravi.DomainEvents.ShouldBe([new UserOrgPlacementChangedDomainEvent(ravi.Id, _root.Id, null)]);
    }

    [Fact]
    public void ORG_04_AC6_reactivating_a_user_whose_primary_unit_is_inactive_is_refused_until_an_active_unit_is_set()
    {
        var ravi = NewUser("ravi", _adyar);
        ravi.Deactivate(activePlatformAdminCount: 1);
        _adyar.Deactivate(activeChildUnits: 0, activeUsers: 0);

        ravi.Activate(_adyar).Error.ShouldBe(OrgErrors.PrimaryUnitInactive);
        ravi.IsActive.ShouldBeFalse();

        ravi.PlaceInUnit(_tNagar).IsSuccess.ShouldBeTrue(); // allowed while the user is inactive
        ravi.Activate(_tNagar).IsSuccess.ShouldBeTrue();
        ravi.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Activating_needs_the_users_own_primary_unit()
    {
        var ravi = NewUser("ravi");

        Should.Throw<ArgumentException>(() => ravi.Activate(_adyar));
    }

    [Fact]
    public void ORG_04_AC3_a_user_cannot_be_their_own_manager()
    {
        var ravi = NewUser("ravi");

        var result = Report(ravi, ravi);

        result.Error.ShouldBe(OrgErrors.ManagerIsSelf);
        result.Error!.FieldErrors!.Keys.ShouldContain("managerId");
        ravi.ManagerId.ShouldBeNull();
    }

    [Fact]
    public void ORG_04_AC2_a_direct_reporting_loop_is_refused()
    {
        var arun = NewUser("arun");
        var ravi = NewUser("ravi");
        Report(ravi, arun, arun).IsSuccess.ShouldBeTrue(); // Ravi reports to Arun

        Report(arun, ravi, arun).Error.ShouldBe(OrgErrors.ReportingLoop); // Arun would report to Ravi

        arun.ManagerId.ShouldBeNull();
    }

    [Fact]
    public void ORG_04_AC2_a_reporting_loop_through_a_chain_is_refused()
    {
        var lakshmi = NewUser("lakshmi");
        var priya = NewUser("priya");
        var suresh = NewUser("suresh");
        var arun = NewUser("arun");
        Report(priya, lakshmi, lakshmi).IsSuccess.ShouldBeTrue();
        Report(suresh, priya, lakshmi, priya).IsSuccess.ShouldBeTrue();
        Report(arun, suresh, lakshmi, priya, suresh).IsSuccess.ShouldBeTrue();

        // Lakshmi → Arun would close Arun → Suresh → Priya → Lakshmi → Arun.
        Report(lakshmi, arun, lakshmi, priya, suresh, arun).Error.ShouldBe(OrgErrors.ReportingLoop);
        lakshmi.ManagerId.ShouldBeNull();
    }

    [Fact]
    public void A_damaged_chain_counts_as_a_loop()
    {
        var a = NewUser("user.a");
        var b = NewUser("user.b");
        var ravi = NewUser("ravi");
        var broken = ManagerChain.Walk(a.Id, id => id == a.Id ? b.Id : a.Id); // a → b → a …

        broken.IsBroken.ShouldBeTrue();
        ravi.SetManager(a, broken).Error.ShouldBe(OrgErrors.ReportingLoop);
    }

    [Fact]
    public void ORG_04_AC7_an_inactive_user_cannot_become_a_reporting_manager()
    {
        var arun = NewUser("arun");
        var ravi = NewUser("ravi");
        arun.Deactivate(activePlatformAdminCount: 1);

        Report(ravi, arun, arun).Error.ShouldBe(OrgErrors.ManagerInactive);
        ravi.ManagerId.ShouldBeNull();
    }

    [Fact]
    public void ORG_04_AC5_the_reporting_manager_is_optional_and_can_be_set_changed_and_removed()
    {
        var arun = NewUser("arun");
        var suresh = NewUser("suresh");
        var ravi = NewUser("ravi");
        ravi.ClearDomainEvents();

        Report(ravi, arun, arun).IsSuccess.ShouldBeTrue();
        Report(ravi, arun, arun).IsSuccess.ShouldBeTrue(); // the same manager again changes nothing
        Report(ravi, suresh, suresh).IsSuccess.ShouldBeTrue();
        Report(ravi, null).IsSuccess.ShouldBeTrue();

        ravi.ManagerId.ShouldBeNull();
        ravi.DomainEvents.ShouldBe(
        [
            new UserOrgPlacementChangedDomainEvent(ravi.Id, _tNagar.Id, arun.Id),
            new UserOrgPlacementChangedDomainEvent(ravi.Id, _tNagar.Id, suresh.Id),
            new UserOrgPlacementChangedDomainEvent(ravi.Id, _tNagar.Id, null),
        ]);
    }

    [Fact]
    public void The_chain_passed_must_start_at_the_new_manager()
    {
        var arun = NewUser("arun");
        var ravi = NewUser("ravi");

        Should.Throw<ArgumentException>(() => ravi.SetManager(arun, ChainAbove(ravi)));
        Should.Throw<ArgumentNullException>(() => ravi.SetManager(arun, null));
    }

    [Fact]
    public void ORG_05_AC1_business_roles_can_be_given_and_taken_away()
    {
        var meena = NewUser("meena");
        meena.ClearDomainEvents();

        meena.AddBusinessRole(_creditOfficer).IsSuccess.ShouldBeTrue();
        meena.AddBusinessRole(_creditOfficer).IsSuccess.ShouldBeTrue(); // already held: nothing changes
        meena.HoldsBusinessRole(_creditOfficer.Id).ShouldBeTrue();

        meena.RemoveBusinessRole(_creditOfficer.Id).IsSuccess.ShouldBeTrue();
        meena.RemoveBusinessRole(_creditOfficer.Id).IsSuccess.ShouldBeTrue(); // not held: nothing changes

        meena.BusinessRoleIds.ShouldBeEmpty();
        meena.DomainEvents.ShouldBe(
        [
            new UserBusinessRolesChangedDomainEvent(meena.Id, [_creditOfficer.Id]),
            new UserBusinessRolesChangedDomainEvent(meena.Id, []),
        ],
        new BusinessRoleEventComparer());
    }

    [Fact]
    public void ORG_05_AC2_a_deactivated_role_cannot_be_given_to_users()
    {
        var meena = NewUser("meena");
        _creditOfficer.Deactivate();

        meena.AddBusinessRole(_creditOfficer).Error.ShouldBe(OrgErrors.BusinessRoleInactive);
        meena.SetBusinessRoles([_creditOfficer]).Error.ShouldBe(OrgErrors.BusinessRoleInactive);
        meena.BusinessRoleIds.ShouldBeEmpty();
    }

    [Fact]
    public void A_member_keeps_a_role_that_is_deactivated_later_and_it_can_still_be_taken_away()
    {
        var meena = NewUser("meena");
        meena.AddBusinessRole(_creditOfficer);
        _creditOfficer.Deactivate();

        meena.SetBusinessRoles([_creditOfficer, _reviewer]).IsSuccess.ShouldBeTrue(); // kept, plus a new active one
        meena.BusinessRoleIds.ShouldBe([_creditOfficer.Id, _reviewer.Id], ignoreOrder: true);

        meena.RemoveBusinessRole(_creditOfficer.Id).IsSuccess.ShouldBeTrue();
        meena.BusinessRoleIds.ShouldBe([_reviewer.Id]);
    }

    [Fact]
    public void ORG_05_AC5_an_inactive_user_cannot_be_given_a_role()
    {
        var meena = NewUser("meena");
        meena.Deactivate(activePlatformAdminCount: 1);

        meena.AddBusinessRole(_creditOfficer).Error.ShouldBe(OrgErrors.MemberInactive);
        meena.SetBusinessRoles([_creditOfficer]).Error.ShouldBe(OrgErrors.MemberInactive);
    }

    [Fact]
    public void ORG_04_AC5_setting_all_business_roles_at_once_is_all_or_nothing()
    {
        var meena = NewUser("meena");
        meena.AddBusinessRole(_reviewer);
        _creditOfficer.Deactivate();
        meena.ClearDomainEvents();

        meena.SetBusinessRoles([_creditOfficer]).IsFailure.ShouldBeTrue();
        meena.BusinessRoleIds.ShouldBe([_reviewer.Id]); // the failed call removed nothing

        meena.SetBusinessRoles([_reviewer]).IsSuccess.ShouldBeTrue(); // unchanged: no event
        meena.SetBusinessRoles([]).IsSuccess.ShouldBeTrue();
        meena.BusinessRoleIds.ShouldBeEmpty();
        meena.DomainEvents.ShouldHaveSingleItem();
    }

    /// <summary>The events carry lists, which records compare by reference; compare their contents instead.</summary>
    private sealed class BusinessRoleEventComparer : IEqualityComparer<IDomainEvent>
    {
        public bool Equals(IDomainEvent? x, IDomainEvent? y) =>
            x is UserBusinessRolesChangedDomainEvent a && y is UserBusinessRolesChangedDomainEvent b
                ? a.UserId == b.UserId && a.BusinessRoleIds.SequenceEqual(b.BusinessRoleIds)
                : object.Equals(x, y);

        public int GetHashCode(IDomainEvent obj) => 0;
    }
}
