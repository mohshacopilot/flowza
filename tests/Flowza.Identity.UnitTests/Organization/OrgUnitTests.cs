using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.UnitTests.Organization;

/// <summary>
/// Tree rules on part of the spec's example organization: Head Office → South Zone → Chennai Region → T Nagar and Adyar
/// Branch, and North Zone.
/// </summary>
public sealed class OrgUnitTests
{
    private readonly UnitType _ho = UnitType.Create(Guid.NewGuid(), "HO", "Head Office").Value;
    private readonly UnitType _zone = UnitType.Create(Guid.NewGuid(), "ZONE", "Zone").Value;
    private readonly UnitType _region = UnitType.Create(Guid.NewGuid(), "REGION", "Region").Value;
    private readonly UnitType _branch = UnitType.Create(Guid.NewGuid(), "BRANCH", "Branch").Value;
    private readonly OrgUnit _headOffice;
    private readonly OrgUnit _south;
    private readonly OrgUnit _chennai;
    private readonly OrgUnit _tNagar;
    private readonly OrgUnit _adyar;
    private readonly OrgUnit _north;
    private int _nextNumber = 2;

    public OrgUnitTests()
    {
        _headOffice = OrgUnit.CreateRoot(Guid.NewGuid(), 1, OrgCode.Create("ROOT").Value, "Head Office", _ho.Id);
        _south = Child("ZONE-S", "South Zone", _zone, _headOffice);
        _chennai = Child("REG-CHN", "Chennai Region", _region, _south);
        _tNagar = Child("BR-TNAGAR", "T Nagar Branch", _branch, _chennai);
        _adyar = Child("BR-ADYAR", "Adyar Branch", _branch, _chennai);
        _north = Child("ZONE-N", "North Zone", _zone, _headOffice);
    }

    private OrgUnit Child(string code, string name, UnitType type, OrgUnit parent) =>
        OrgUnit.Create(Guid.NewGuid(), _nextNumber++, code, name, type, parent).Value;

    private IReadOnlyCollection<OrgUnit> Below(OrgUnit unit) =>
        [.. new[] { _headOffice, _south, _chennai, _tNagar, _adyar, _north }.Where(u => u.Id != unit.Id && u.Path.IsSameOrBelow(unit.Path))];

    private static User ActiveUser(string username) =>
        User.Create(Guid.NewGuid(), username, username, $"{username}@example.org", null, []).Value;

    [Fact]
    public void A_new_unit_is_active_under_its_parent_with_a_path_below_the_parent()
    {
        _tNagar.IsActive.ShouldBeTrue();
        _tNagar.ParentId.ShouldBe(_chennai.Id);
        _tNagar.Code.Value.ShouldBe("BR-TNAGAR");
        _tNagar.Path.Value.ShouldBe($"{_chennai.Path.Value}{_tNagar.Number}/");
        _tNagar.DomainEvents.ShouldBe([new OrgUnitCreatedDomainEvent(_tNagar.Id, _chennai.Id, _branch.Id)]);
    }

    [Fact]
    public void ORG_02_AC1_units_can_be_created_at_any_depth_with_any_type_under_any_other_type()
    {
        // A branch under a branch under a zone under a branch …, down to the deepest allowed level.
        var parent = _tNagar;
        var types = new[] { _zone, _branch, _region, _ho };
        for (var level = parent.Path.Depth + 1; level <= UnitPath.MaxDepth; level++)
        {
            var created = OrgUnit.Create(Guid.NewGuid(), _nextNumber++, $"L{level}", $"Level {level}", types[level % types.Length], parent);
            created.IsSuccess.ShouldBeTrue();
            parent = created.Value;
        }

        parent.Path.Depth.ShouldBe(UnitPath.MaxDepth);
        OrgUnit.Create(Guid.NewGuid(), _nextNumber++, "TOO-DEEP", "Too deep", _branch, parent).Error.ShouldBe(OrgErrors.TreeTooDeep);
    }

    [Fact]
    public void ORG_01_AC4_a_deactivated_unit_type_cannot_be_chosen_for_a_new_unit_and_existing_units_keep_it()
    {
        var dept = UnitType.Create(Guid.NewGuid(), "DEPT", "Department").Value;
        var credit = Child("DEPT-CREDIT", "South Credit Dept", dept, _south);
        credit.Deactivate(activeChildUnits: 0, activeUsers: 0);
        dept.Deactivate(activeUnitsOfType: 0).IsSuccess.ShouldBeTrue();

        OrgUnit.Create(Guid.NewGuid(), _nextNumber++, "DEPT-HR", "HR Dept", dept, _south).Error.ShouldBe(OrgErrors.UnitTypeInactive);
        credit.UnitTypeId.ShouldBe(dept.Id);
    }

    [Fact]
    public void ORG_02_AC8_an_active_unit_cannot_be_added_under_an_inactive_unit()
    {
        _north.Deactivate(activeChildUnits: 0, activeUsers: 0).IsSuccess.ShouldBeTrue();

        OrgUnit.Create(Guid.NewGuid(), _nextNumber++, "REG-DEL", "Delhi Region", _region, _north).Error.ShouldBe(OrgErrors.ParentInactive);
    }

    [Fact]
    public void ORG_02_AC8_an_active_unit_cannot_be_moved_under_an_inactive_unit()
    {
        _north.Deactivate(activeChildUnits: 0, activeUsers: 0);
        var pathBefore = _chennai.Path;

        _chennai.MoveTo(_north, Below(_chennai)).Error.ShouldBe(OrgErrors.ParentInactive);

        _chennai.ParentId.ShouldBe(_south.Id);
        _chennai.Path.ShouldBe(pathBefore);
    }

    [Fact]
    public void An_inactive_unit_may_be_moved_under_an_inactive_unit()
    {
        _adyar.Deactivate(activeChildUnits: 0, activeUsers: 0);
        _north.Deactivate(activeChildUnits: 0, activeUsers: 0);

        _adyar.MoveTo(_north, Below(_adyar)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ORG_02_AC2_a_unit_cannot_be_moved_under_itself()
    {
        _chennai.MoveTo(_chennai, Below(_chennai)).Error.ShouldBe(OrgErrors.MoveIntoOwnSubtree);
    }

    [Fact]
    public void ORG_02_AC2_a_unit_cannot_be_moved_under_one_of_its_descendants()
    {
        _chennai.ClearDomainEvents();

        _south.MoveTo(_tNagar, Below(_south)).Error.ShouldBe(OrgErrors.MoveIntoOwnSubtree); // a grandchild
        _chennai.MoveTo(_tNagar, Below(_chennai)).Error.ShouldBe(OrgErrors.MoveIntoOwnSubtree); // a child

        _chennai.ParentId.ShouldBe(_south.Id);
        _south.ParentId.ShouldBe(_headOffice.Id);
        _chennai.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ORG_02_AC3_moving_a_unit_moves_its_whole_subtree()
    {
        _chennai.ClearDomainEvents();

        _chennai.MoveTo(_north, Below(_chennai)).IsSuccess.ShouldBeTrue();

        _chennai.ParentId.ShouldBe(_north.Id);
        _chennai.Path.Value.ShouldBe($"{_north.Path.Value}{_chennai.Number}/");
        _tNagar.Path.Value.ShouldBe($"{_chennai.Path.Value}{_tNagar.Number}/");
        _adyar.Path.Value.ShouldBe($"{_chennai.Path.Value}{_adyar.Number}/");
        _tNagar.ParentId.ShouldBe(_chennai.Id); // children keep their parent; only their place in the tree changes
        _tNagar.Path.IsSameOrBelow(_north.Path).ShouldBeTrue();
        _tNagar.Path.IsSameOrBelow(_south.Path).ShouldBeFalse();
        _chennai.DomainEvents.ShouldBe([new OrgUnitMovedDomainEvent(_chennai.Id, _south.Id, _north.Id)]);
    }

    [Fact]
    public void Moving_a_unit_to_its_current_parent_changes_nothing()
    {
        _chennai.ClearDomainEvents();
        var pathBefore = _tNagar.Path;

        _chennai.MoveTo(_south, Below(_chennai)).IsSuccess.ShouldBeTrue();

        _tNagar.Path.ShouldBe(pathBefore);
        _chennai.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void The_root_cannot_be_moved()
    {
        // Every other unit is below the root, so the root rule answers first with its own message.
        _headOffice.MoveTo(_north, Below(_headOffice)).Error.ShouldBe(OrgErrors.RootCannotMove);
        _headOffice.ParentId.ShouldBeNull();
    }

    [Fact]
    public void A_move_that_would_make_the_tree_too_deep_changes_nothing()
    {
        // A chain under North Zone down to the deepest level; moving Chennai Region (3 levels with its branches)
        // under its bottom would push the branches past level 50.
        var bottom = _north;
        while (bottom.Path.Depth < UnitPath.MaxDepth - 1)
        {
            bottom = Child($"N{bottom.Path.Depth}", "Level", _zone, bottom);
        }

        var chennaiPath = _chennai.Path;
        var tNagarPath = _tNagar.Path;

        _chennai.MoveTo(bottom, Below(_chennai)).Error.ShouldBe(OrgErrors.TreeTooDeep);

        _chennai.Path.ShouldBe(chennaiPath);
        _tNagar.Path.ShouldBe(tNagarPath);
        _chennai.ParentId.ShouldBe(_south.Id);
    }

    [Fact]
    public void Passing_a_unit_that_is_not_below_the_moved_unit_is_a_programming_error()
    {
        Should.Throw<ArgumentException>(() => _chennai.MoveTo(_north, [_tNagar, _north]));
        Should.Throw<ArgumentException>(() => _chennai.MoveTo(_north, [_chennai]));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public void ORG_02_AC4_a_unit_with_active_child_units_or_active_users_cannot_be_deactivated(int activeChildUnits, int activeUsers)
    {
        _south.ClearDomainEvents();

        _south.Deactivate(activeChildUnits, activeUsers).Error.ShouldBe(OrgErrors.UnitHasActiveMembers);

        _south.IsActive.ShouldBeTrue();
        _south.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ORG_02_AC4_a_unit_without_active_child_units_or_users_can_be_deactivated()
    {
        _adyar.ClearDomainEvents();

        _adyar.Deactivate(activeChildUnits: 0, activeUsers: 0).IsSuccess.ShouldBeTrue();

        _adyar.IsActive.ShouldBeFalse();
        _adyar.DomainEvents.ShouldBe([new OrgUnitDeactivatedDomainEvent(_adyar.Id)]);
    }

    [Fact]
    public void The_root_is_blocked_like_any_unit_while_it_has_active_units_or_users()
    {
        _headOffice.Deactivate(activeChildUnits: 2, activeUsers: 1).Error.ShouldBe(OrgErrors.UnitHasActiveMembers);
    }

    [Fact]
    public void ORG_02_AC9_a_deactivated_unit_can_be_reactivated_while_its_parent_is_active()
    {
        _adyar.Deactivate(activeChildUnits: 0, activeUsers: 0);
        _adyar.ClearDomainEvents();

        _adyar.Activate(_chennai).IsSuccess.ShouldBeTrue();

        _adyar.IsActive.ShouldBeTrue();
        _adyar.DomainEvents.ShouldBe([new OrgUnitActivatedDomainEvent(_adyar.Id)]);
    }

    [Fact]
    public void ORG_02_AC9_reactivating_a_unit_under_an_inactive_parent_is_refused()
    {
        _adyar.Deactivate(activeChildUnits: 0, activeUsers: 0);
        _tNagar.Deactivate(activeChildUnits: 0, activeUsers: 0);
        _chennai.Deactivate(activeChildUnits: 0, activeUsers: 0);

        _adyar.Activate(_chennai).Error.ShouldBe(OrgErrors.ParentInactive);
        _adyar.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activating_needs_the_units_own_parent()
    {
        _adyar.Deactivate(activeChildUnits: 0, activeUsers: 0);

        Should.Throw<ArgumentException>(() => _adyar.Activate(_north));
        Should.Throw<ArgumentException>(() => _adyar.Activate(null));
        _headOffice.Activate(null).IsSuccess.ShouldBeTrue(); // the root has no parent
    }

    [Fact]
    public void ORG_03_AC2_a_head_must_be_an_active_user()
    {
        var arun = ActiveUser("arun");
        arun.Deactivate(activePlatformAdminCount: 1);

        _tNagar.SetHead(arun).Error.ShouldBe(OrgErrors.HeadInactive);
        _tNagar.HeadUserId.ShouldBeNull();
    }

    [Fact]
    public void ORG_03_AC2_one_active_user_may_head_several_units_without_working_in_them()
    {
        var priya = ActiveUser("priya");

        _south.SetHead(priya).IsSuccess.ShouldBeTrue();
        _north.SetHead(priya).IsSuccess.ShouldBeTrue();

        _south.HeadUserId.ShouldBe(priya.Id);
        _north.HeadUserId.ShouldBe(priya.Id);
    }

    [Fact]
    public void ORG_03_AC4_the_head_can_be_changed_and_cleared()
    {
        var arun = ActiveUser("arun");
        var suresh = ActiveUser("suresh");
        _tNagar.ClearDomainEvents();

        _tNagar.SetHead(arun);
        _tNagar.SetHead(arun); // the same head again changes nothing
        _tNagar.SetHead(suresh);
        _tNagar.SetHead(null); // vacant

        _tNagar.HeadUserId.ShouldBeNull();
        _tNagar.DomainEvents.ShouldBe(
        [
            new OrgUnitHeadChangedDomainEvent(_tNagar.Id, arun.Id),
            new OrgUnitHeadChangedDomainEvent(_tNagar.Id, suresh.Id),
            new OrgUnitHeadChangedDomainEvent(_tNagar.Id, null),
        ]);
    }

    [Fact]
    public void ORG_03_AC4_renaming_a_unit_keeps_its_code()
    {
        _tNagar.ClearDomainEvents();

        _tNagar.Rename("T. Nagar Branch").IsSuccess.ShouldBeTrue();
        _tNagar.Rename(" ").Error.ShouldBe(OrgErrors.InvalidName);

        _tNagar.Name.ShouldBe("T. Nagar Branch");
        _tNagar.Code.Value.ShouldBe("BR-TNAGAR");
        _tNagar.DomainEvents.ShouldBe([new OrgUnitRenamedDomainEvent(_tNagar.Id)]);
        typeof(OrgUnit).GetProperty(nameof(OrgUnit.Code))!.SetMethod!.IsPublic.ShouldBeFalse();
    }

    [Theory]
    [InlineData("BR TNAGAR")]
    [InlineData("")]
    [InlineData("BR.TNAGAR")]
    public void ORG_03_AC3_a_unit_code_outside_the_format_is_rejected(string code)
    {
        OrgUnit.Create(Guid.NewGuid(), _nextNumber++, code, "Branch", _branch, _chennai).Error.ShouldBe(OrgErrors.InvalidCode);
    }

    [Fact]
    public void ORG_03_AC3_a_unit_code_entered_in_lower_case_is_stored_in_upper_case()
    {
        Child("br-velachery", "Velachery Branch", _branch, _chennai).Code.Value.ShouldBe("BR-VELACHERY");
    }

    [Fact]
    public void Touching_the_org_structure_always_changes_the_row()
    {
        var structure = new OrgStructure(OrgStructure.SingletonId);
        var now = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.FromHours(5.5));

        structure.Touch(now);
        structure.Touch(now); // the same moment still counts as a change

        structure.ChangeCount.ShouldBe(2);
        structure.ChangedAt.ShouldBe(now);
        structure.ChangedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }
}
