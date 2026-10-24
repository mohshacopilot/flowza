using Flowza.Identity.Domain.Organization;

namespace Flowza.Identity.UnitTests.Organization;

public sealed class OrgCodeTests
{
    [Theory]
    [InlineData("Z")]
    [InlineData("ZONE")]
    [InlineData("BR-TNAGAR")]
    [InlineData("CREDIT_OFFICER")]
    [InlineData("L2")]
    [InlineData("12345678901234567890123456789012345678901234567890")] // 50 characters
    public void ORG_01_AC5_codes_in_the_format_are_accepted(string value)
    {
        OrgCode.Create(value).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123456789012345678901")] // 51 characters
    [InlineData("SOUTH ZONE")] // space
    [InlineData("ZONE.1")] // '.' is allowed in usernames, not in codes
    [InlineData("ZONE/1")]
    [InlineData("ZÖNE")] // letters outside ASCII could imitate another code
    [InlineData("ΖONE")] // Greek capital zeta looks like Z
    public void ORG_01_AC5_codes_outside_the_format_are_rejected(string? value)
    {
        var result = OrgCode.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(OrgErrors.InvalidCode);
        result.Error.FieldErrors!.Keys.ShouldContain("code");
    }

    [Fact]
    public void ORG_01_AC5_a_code_entered_in_lower_case_is_stored_in_upper_case_and_equals_the_same_code_in_another_case()
    {
        var lower = OrgCode.Create("br-tnagar").Value;
        var mixed = OrgCode.Create("Br-TNagar").Value;

        lower.Value.ShouldBe("BR-TNAGAR");
        lower.ShouldBe(mixed);
    }

    [Fact]
    public void Codes_are_trimmed()
    {
        OrgCode.Create("  zone  ").Value.Value.ShouldBe("ZONE");
    }

    [Fact]
    public void Error_messages_never_quote_the_entered_value()
    {
        const string entered = "SECRET VALUE";

        var error = OrgCode.Create(entered).Error!;

        error.Message.ShouldNotContain(entered);
        error.Code.ShouldNotContain(entered);
    }
}
