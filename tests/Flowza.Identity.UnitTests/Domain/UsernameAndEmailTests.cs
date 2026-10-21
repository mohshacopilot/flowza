using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.UnitTests.Domain;

public sealed class UsernameAndEmailTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("ravi.k")]
    [InlineData("EMP_0042")]
    [InlineData("a-b.c_d")]
    [InlineData("12345678901234567890123456789012345678901234567890")] // 50 characters
    public void IAM_04_AC4_usernames_in_the_format_are_accepted(string value)
    {
        Username.Create(value).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")] // too short
    [InlineData("123456789012345678901234567890123456789012345678901")] // 51 characters
    [InlineData("ravi kumar")] // space
    [InlineData("ravi@bank")] // '@' is not allowed
    [InlineData("rávi")] // letters outside ASCII could imitate another name
    [InlineData("ра.vi")] // Cyrillic look-alikes
    public void IAM_04_AC4_usernames_outside_the_format_are_rejected(string? value)
    {
        var result = Username.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidUsername);
        result.Error.FieldErrors!.Keys.ShouldContain("username");
    }

    [Fact]
    public void IAM_04_AC4_usernames_are_compared_ignoring_letter_case()
    {
        Username.Create("Ravi.K").Value.Normalized.ShouldBe(Username.Create("ravi.k").Value.Normalized);
        Username.Normalize(" ravi.K ").ShouldBe("RAVI.K");
    }

    [Fact]
    public void Usernames_are_trimmed()
    {
        Username.Create("  ravi.k  ").Value.Value.ShouldBe("ravi.k");
    }

    [Theory]
    [InlineData("ravi@bank.in")]
    [InlineData("first.last+tag@sub.example.org")]
    public void IAM_04_AC4_valid_emails_are_accepted(string value)
    {
        Email.Create(value).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ravi")]
    [InlineData("@bank.in")]
    [InlineData("ravi@")]
    [InlineData("ravi@bank")]
    [InlineData("ravi@@bank.in")]
    [InlineData("ravi@bank..in")]
    [InlineData("ravi@bank.in.")]
    [InlineData("ravi kumar@bank.in")]
    public void IAM_04_AC4_invalid_emails_are_rejected(string? value)
    {
        var result = Email.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidEmail);
    }

    [Fact]
    public void IAM_04_AC4_emails_are_compared_ignoring_letter_case()
    {
        Email.Create("Ravi@Bank.IN").Value.Normalized.ShouldBe(Email.Create("ravi@bank.in").Value.Normalized);
    }

    [Fact]
    public void Error_messages_never_quote_the_entered_value()
    {
        const string entered = "secret.person@@example.org";

        var error = Email.Create(entered).Error!;

        error.Message.ShouldNotContain(entered);
        error.Code.ShouldNotContain(entered);
    }
}
