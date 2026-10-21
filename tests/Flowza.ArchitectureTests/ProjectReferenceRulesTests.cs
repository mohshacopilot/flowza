namespace Flowza.ArchitectureTests;

/// <summary>
/// Constitution articles 3 and 4 checked on the project files themselves, so a forbidden reference fails the
/// build even before any code uses it.
/// </summary>
public sealed class ProjectReferenceRulesTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> s_references =
        RepositoryLayout.SourceProjectReferences();

    [Fact]
    public void Every_source_project_has_a_known_kind()
    {
        var unknown = s_references.Keys.Where(name => ProjectKind.Of(name).Category == "Unknown").ToList();

        unknown.ShouldBeEmpty("every project under src/ must be a service layer, building block, ServiceDefaults, Gateway or AppHost");
    }

    [Fact]
    public void Constitution_3_1_no_service_references_another_service()
    {
        var violations =
            from pair in s_references
            let source = ProjectKind.Of(pair.Key)
            where source.Category == "Service"
            from target in pair.Value.Select(ProjectKind.Of)
            where target.Category == "Service" && target.Service != source.Service
            select $"{source.Name} -> {target.Name}";

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Constitution_4_1_layers_reference_only_inward()
    {
        var allowedInnerLayers = new Dictionary<string, string[]>
        {
            ["Domain"] = [],
            ["Application"] = ["Domain"],
            ["Infrastructure"] = ["Application", "Domain"],
            ["Api"] = ["Infrastructure", "Application", "Domain"],
            ["Single"] = [],
        };

        var violations =
            from pair in s_references
            let source = ProjectKind.Of(pair.Key)
            where source.Category == "Service"
            from target in pair.Value.Select(ProjectKind.Of)
            where target.Category == "Service" && target.Service == source.Service
                && !allowedInnerLayers[source.Layer!].Contains(target.Layer)
            select $"{source.Name} -> {target.Name}";

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Constitution_4_2_domain_references_only_Flowza_Common()
    {
        var violations =
            from pair in s_references
            where ProjectKind.Of(pair.Key).Layer == "Domain"
            from target in pair.Value
            where target != "Flowza.Common"
            select $"{pair.Key} -> {target}";

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_and_Application_do_not_reference_infrastructure_building_blocks()
    {
        string[] forbidden = ["Flowza.Persistence", "Flowza.Messaging", "Flowza.ServiceDefaults"];

        var violations =
            from pair in s_references
            let layer = ProjectKind.Of(pair.Key).Layer
            where layer is "Domain" or "Application"
            from target in pair.Value
            where forbidden.Contains(target)
            select $"{pair.Key} -> {target}";

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Building_blocks_and_ServiceDefaults_never_reference_services_or_the_gateway()
    {
        var violations =
            from pair in s_references
            let source = ProjectKind.Of(pair.Key)
            where source.Category is "BuildingBlock" or "ServiceDefaults"
            from target in pair.Value.Select(ProjectKind.Of)
            where target.Category is "Service" or "Gateway" or "AppHost"
            select $"{source.Name} -> {target.Name}";

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Constitution_3_5_contracts_reference_no_other_project()
    {
        s_references["Flowza.Contracts"].ShouldBeEmpty();
    }

    [Fact]
    public void Gateway_references_no_service()
    {
        var targets = s_references["Flowza.Gateway"].Select(ProjectKind.Of);

        targets.Where(t => t.Category == "Service").Select(t => t.Name).ShouldBeEmpty();
    }
}
