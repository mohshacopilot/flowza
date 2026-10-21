using System.Reflection;
using NetArchTest.Rules;

namespace Flowza.ArchitectureTests;

/// <summary>
/// Constitution articles 3 and 4 checked on the compiled types (NetArchTest), so a forbidden dependency is caught
/// even if it arrives through a package rather than a project reference.
/// </summary>
public sealed class DependencyRulesTests
{
    private static readonly string[] s_frameworks = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Wolverine"];

    [Theory]
    [InlineData("Identity")]
    [InlineData("Design")]
    [InlineData("Runtime")]
    public void Constitution_4_2_domain_has_no_framework_or_outer_layer_dependencies(string service)
    {
        string[] forbidden =
        [
            .. s_frameworks,
            $"Flowza.{service}.Application",
            $"Flowza.{service}.Infrastructure",
            $"Flowza.{service}.Api",
        ];

        AssertNoDependencies(Load($"Flowza.{service}.Domain"), forbidden);
    }

    [Theory]
    [InlineData("Identity")]
    [InlineData("Design")]
    [InlineData("Runtime")]
    public void Constitution_4_1_application_does_not_depend_on_web_or_infrastructure(string service)
    {
        AssertNoDependencies(
            Load($"Flowza.{service}.Application"),
            ["Microsoft.AspNetCore", $"Flowza.{service}.Infrastructure", $"Flowza.{service}.Api"]);
    }

    [Theory]
    [InlineData("Flowza.Identity.Domain", "Identity")]
    [InlineData("Flowza.Identity.Application", "Identity")]
    [InlineData("Flowza.Identity.Infrastructure", "Identity")]
    [InlineData("Flowza.Identity.Api", "Identity")]
    [InlineData("Flowza.Design.Domain", "Design")]
    [InlineData("Flowza.Design.Application", "Design")]
    [InlineData("Flowza.Design.Infrastructure", "Design")]
    [InlineData("Flowza.Design.Api", "Design")]
    [InlineData("Flowza.Runtime.Domain", "Runtime")]
    [InlineData("Flowza.Runtime.Application", "Runtime")]
    [InlineData("Flowza.Runtime.Infrastructure", "Runtime")]
    [InlineData("Flowza.Runtime.Api", "Runtime")]
    [InlineData("Flowza.Worker", "Worker")]
    public void Constitution_3_1_services_do_not_depend_on_other_services(string assembly, string service)
    {
        var otherServices = ProjectKind.Services
            .Where(s => s != service)
            .Select(s => $"Flowza.{s}")
            .ToArray();

        AssertNoDependencies(Load(assembly), otherServices);
    }

    [Fact]
    public void Constitution_3_5_contracts_depend_on_no_framework_and_no_other_Flowza_code()
    {
        string[] forbidden =
        [
            .. s_frameworks,
            "Flowza.Common",
            "Flowza.Persistence",
            "Flowza.Messaging",
            "Flowza.ServiceDefaults",
            .. ProjectKind.Services.Select(s => $"Flowza.{s}"),
        ];

        AssertNoDependencies(Load("Flowza.Contracts"), forbidden);
    }

    [Fact]
    public void Common_building_block_depends_on_no_framework()
    {
        AssertNoDependencies(Load("Flowza.Common"), s_frameworks);
    }

    private static Assembly Load(string name) => Assembly.Load(new AssemblyName(name));

    private static void AssertNoDependencies(Assembly assembly, string[] forbiddenNamespaces)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        var failing = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            $"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbiddenNamespaces)}]; failing types: {string.Join(", ", failing)}");
    }
}
