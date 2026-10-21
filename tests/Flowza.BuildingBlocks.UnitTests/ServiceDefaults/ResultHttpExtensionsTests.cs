using Flowza.Common.Results;
using Flowza.ServiceDefaults.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flowza.BuildingBlocks.UnitTests.ServiceDefaults;

public sealed class ResultHttpExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorKind.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorKind.Failure, StatusCodes.Status422UnprocessableEntity)]
    public void T2_S4_each_error_kind_maps_to_its_status_code(ErrorKind kind, int status)
    {
        var problem = new Error("some.code", "Some message.", kind).ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(status);
        problem.ProblemDetails.Extensions["code"].ShouldBe("some.code");
    }

    [Fact]
    public void T2_S4_validation_errors_list_the_fields()
    {
        var error = Error.Validation("validation", "Fields are not valid.", new Dictionary<string, string[]> { ["Email"] = ["identity.email.taken"] });

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var details = problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        details.Errors["Email"].ShouldBe(["identity.email.taken"]);
        details.Extensions["code"].ShouldBe("validation");
    }

    [Fact]
    public void T2_S4_success_with_a_value_is_200_and_without_a_value_is_204()
    {
        Result.Success(5).ToHttpResult().ShouldBeOfType<Ok<int>>().Value.ShouldBe(5);
        Result.Success().ToHttpResult().ShouldBeOfType<NoContent>();
    }

    [Fact]
    public void T2_S4_failures_become_problems()
    {
        Result.Failure(Error.Conflict("c", "m")).ToHttpResult().ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(409);
        Result.Failure<int>(Error.NotFound("n", "m")).ToHttpResult().ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(404);
    }

    [Theory]
    [InlineData("4bf92f3577b34da6a3ce929d0e0e4736", "4bf92f35")]
    [InlineData("abc", "abc")]
    public void T7_OB4_reference_code_is_the_first_8_characters_of_the_trace_id(string traceId, string expected)
    {
        ReferenceCode.From(traceId).ShouldBe(expected);
    }
}
