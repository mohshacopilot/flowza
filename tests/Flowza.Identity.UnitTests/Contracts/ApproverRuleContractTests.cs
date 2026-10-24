using System.Text.Json;
using System.Text.Json.Nodes;
using Flowza.Contracts.Identity.ApproverRules.V1;

namespace Flowza.Identity.UnitTests.Contracts;

/// <summary>The approver rule contract of plan 002 §7 (PA2), as Design, Runtime and Identity read and write it.</summary>
public sealed class ApproverRuleContractTests
{
    // The options ASP.NET Core and Wolverine use for JSON (camelCase names, case-insensitive reading).
    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    public static TheoryData<string, ApproverRule> PlanExamples => new()
    {
        { """{ "type": "SpecificUser", "username": "priya" }""", new SpecificUserRule("priya") },
        { """{ "type": "Requester" }""", new RequesterRule() },
        { """{ "type": "ManagerLevelsUp", "levels": 2 }""", new ManagerLevelsUpRule(2) },
        { """{ "type": "RequesterUnitHead" }""", new RequesterUnitHeadRule() },
        { """{ "type": "NearestUnitOfTypeHead", "unitType": "ZONE" }""", new NearestUnitOfTypeHeadRule("ZONE") },
        {
            """{ "type": "RoleInScope", "role": "CREDIT_OFFICER", "scope": "NearestUnitOfTypeAndBelow", "unitType": "ZONE" }""",
            new RoleInScopeRule("CREDIT_OFFICER", RoleScope.NearestUnitOfTypeAndBelow, "ZONE")
        },
        { """{ "type": "UserPickerField", "field": "approver" }""", new UserPickerFieldRule("approver") },
    };

    [Theory]
    [MemberData(nameof(PlanExamples))]
    public void PA2_every_rule_shape_of_the_plan_is_read_as_its_rule(string json, ApproverRule expected)
    {
        JsonSerializer.Deserialize<ApproverRule>(json, s_web).ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(PlanExamples))]
    public void PA2_every_rule_is_written_in_the_shape_of_the_plan(string json, ApproverRule rule)
    {
        var written = JsonSerializer.Serialize(rule, s_web);

        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).ShouldBeTrue(written);
        written.ShouldStartWith("""{"type":""");
    }

    [Theory]
    [InlineData("""{ "type": "Anybody" }""")]
    [InlineData("""{ "type": "specificuser", "username": "priya" }""")] // the type name is exact
    public void PA2_an_unknown_rule_type_is_refused(string json)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ApproverRule>(json, s_web));
    }

    [Fact]
    public void PA2_a_rule_without_a_type_is_refused()
    {
        Should.Throw<NotSupportedException>(() => JsonSerializer.Deserialize<ApproverRule>("""{ "username": "priya" }""", s_web));
    }

    [Theory]
    [InlineData(RoleScope.Anywhere, "Anywhere")]
    [InlineData(RoleScope.RequesterUnit, "RequesterUnit")]
    [InlineData(RoleScope.RequesterUnitAndBelow, "RequesterUnitAndBelow")]
    [InlineData(RoleScope.NearestUnitOfTypeAndBelow, "NearestUnitOfTypeAndBelow")]
    public void PA2_the_four_rule_f_scopes_are_written_by_name(RoleScope scope, string name)
    {
        JsonSerializer.Serialize<ApproverRule>(new RoleInScopeRule("CREDIT_OFFICER", scope), s_web).ShouldContain($"\"scope\":\"{name}\"");
    }

    [Fact]
    public void PA2_a_resolve_request_and_result_round_trip()
    {
        var request = new ResolveRequest(Guid.NewGuid(), new UserPickerFieldRule("approver"), PickedUserId: Guid.NewGuid());
        var unit = new OrgRef(OrgRefKind.Unit, Guid.NewGuid(), "ZONE-S", "South Zone");
        var head = new OrgRef(OrgRefKind.User, Guid.NewGuid(), null, "Priya");
        var result = new ResolveResult(
            [head.Id],
            [new ExplanationStep("org.explain.nearestOfType", new Dictionary<string, OrgRef> { ["unit"] = unit }), new ExplanationStep("org.explain.head", new Dictionary<string, OrgRef> { ["user"] = head })],
            Reason: null);

        var readRequest = JsonSerializer.Deserialize<ResolveRequest>(JsonSerializer.Serialize(request, s_web), s_web)!;
        var resultJson = JsonSerializer.Serialize(result, s_web);
        var readResult = JsonSerializer.Deserialize<ResolveResult>(resultJson, s_web)!;

        readRequest.ShouldBe(request);
        readResult.UserIds.ShouldBe([head.Id]);
        readResult.Explanation.Select(s => s.Key).ShouldBe(["org.explain.nearestOfType", "org.explain.head"]);
        readResult.Explanation[0].Args["unit"].ShouldBe(unit);
        resultJson.ShouldContain("\"kind\":\"Unit\"");
    }

    [Fact]
    public void PA2_an_empty_result_carries_its_reason_with_a_number_where_the_text_needs_one()
    {
        var requester = new OrgRef(OrgRefKind.User, Guid.NewGuid(), null, "Ravi");
        var result = new ResolveResult([], [], new ExplanationStep("org.reason.managerChainTooShort", new Dictionary<string, OrgRef> { ["requester"] = requester }, Number: 2));

        var read = JsonSerializer.Deserialize<ResolveResult>(JsonSerializer.Serialize(result, s_web), s_web)!;

        read.UserIds.ShouldBeEmpty();
        read.Reason!.Key.ShouldBe("org.reason.managerChainTooShort");
        read.Reason.Number.ShouldBe(2);
        read.Reason.Args["requester"].ShouldBe(requester);
    }
}
