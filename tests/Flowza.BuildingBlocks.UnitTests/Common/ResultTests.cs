using Flowza.Common.Results;

namespace Flowza.BuildingBlocks.UnitTests.Common;

public sealed class ResultTests
{
    private static readonly Error s_notFound = Error.NotFound("user.not_found", "The user was not found.");

    [Fact]
    public void T2_S4_success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void T2_S4_failure_carries_its_error()
    {
        Result result = s_notFound;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(s_notFound);
    }

    [Fact]
    public void T2_S4_success_with_value_returns_the_value()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        Result.Success("text").Value.ShouldBe("text");
    }

    [Fact]
    public void T2_S4_reading_the_value_of_a_failure_is_a_programming_error()
    {
        Result<int> result = s_notFound;

        var exception = Should.Throw<InvalidOperationException>(() => result.Value);
        exception.Message.ShouldContain("user.not_found");
    }

    [Fact]
    public void T2_S4_failure_factories_reject_a_missing_error()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
        Should.Throw<ArgumentNullException>(() => Result.Failure<int>(null!));
    }

    [Theory]
    [InlineData(ErrorKind.Validation)]
    [InlineData(ErrorKind.NotFound)]
    [InlineData(ErrorKind.Conflict)]
    [InlineData(ErrorKind.Forbidden)]
    [InlineData(ErrorKind.Unauthorized)]
    [InlineData(ErrorKind.Failure)]
    public void T2_S4_error_factories_set_the_kind(ErrorKind kind)
    {
        var error = kind switch
        {
            ErrorKind.Validation => Error.Validation("c", "m"),
            ErrorKind.NotFound => Error.NotFound("c", "m"),
            ErrorKind.Conflict => Error.Conflict("c", "m"),
            ErrorKind.Forbidden => Error.Forbidden("c", "m"),
            ErrorKind.Unauthorized => Error.Unauthorized("c", "m"),
            _ => Error.Failure("c", "m"),
        };

        error.Kind.ShouldBe(kind);
        Result.Failure<string>(error).Error.ShouldBe(error);
    }

    [Fact]
    public void T2_S4_validation_error_keeps_field_errors()
    {
        var fields = new Dictionary<string, string[]> { ["Email"] = ["identity.email.taken"] };

        var error = Error.Validation("validation", "Some fields are not valid.", fields);

        error.FieldErrors.ShouldNotBeNull();
        error.FieldErrors["Email"].ShouldBe(["identity.email.taken"]);
    }
}
