using Flowza.Identity.Application.Users;
using Flowza.Identity.Domain.Users.Passwords;

namespace Flowza.Identity.UnitTests.Domain;

public sealed class PasswordPolicyTests
{
    private sealed class FakeCommonPasswords(params string[] passwords) : ICommonPasswordList
    {
        public bool Contains(string password) => passwords.Contains(password, StringComparer.OrdinalIgnoreCase);
    }

    private static readonly PasswordPolicy s_policy = new(new FakeCommonPasswords("qwertyuiop", "1234567890"));

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("nine-char")]
    public void IAM_06_AC1_passwords_shorter_than_10_are_rejected(string password)
    {
        var result = s_policy.Check(password, "ravi.k");

        result.Error!.Code.ShouldBe("passwords.too_short");
        result.Error.FieldErrors![PasswordPolicy.Field].ShouldContain("passwords.too_short");
    }

    [Theory]
    [InlineData("qwertyuiop")]
    [InlineData("QwertyUiop")]
    public void IAM_06_AC1_common_passwords_are_rejected_ignoring_case(string password)
    {
        s_policy.Check(password, "ravi.k").Error!.Code.ShouldBe("passwords.common");
    }

    [Theory]
    [InlineData("ravi.kumar.01", "ravi.kumar.01")]
    [InlineData("RAVI.KUMAR.01", "ravi.kumar.01")]
    public void IAM_06_AC1_a_password_equal_to_the_username_is_rejected(string password, string username)
    {
        s_policy.Check(password, username).Error!.Code.ShouldBe("passwords.same_as_username");
    }

    [Fact]
    public void Every_broken_rule_is_listed()
    {
        var result = s_policy.Check("abc", "abc");

        result.Error!.FieldErrors![PasswordPolicy.Field].ShouldBe(["passwords.too_short", "passwords.same_as_username"]);
    }

    [Theory]
    [InlineData("correct horse battery")]
    [InlineData("exactly10!")]
    public void A_long_uncommon_password_that_is_not_the_username_is_accepted(string password)
    {
        s_policy.Check(password, "ravi.k").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Error_messages_never_contain_the_password()
    {
        const string password = "ravi.k";

        var error = s_policy.Check(password, "ravi.k").Error!;

        error.Message.ShouldNotContain(password);
    }

    [Fact]
    public void Temporary_passwords_are_long_random_and_pass_the_policy()
    {
        var generator = new TemporaryPasswordGenerator();
        var passwords = Enumerable.Range(0, 200).Select(_ => generator.Generate()).ToList();

        passwords.ShouldAllBe(p => p.Length == TemporaryPasswordGenerator.Length);
        passwords.ShouldAllBe(p => p.All(c => TemporaryPasswordGenerator.Alphabet.Contains(c)));
        passwords.Distinct().Count().ShouldBe(passwords.Count);
        passwords.ShouldAllBe(p => s_policy.Check(p, "ravi.k").IsSuccess);
    }
}
