using Flowza.Common.Results;
using Microsoft.AspNetCore.Http;

namespace Flowza.ServiceDefaults.Http;

/// <summary>
/// Turns a <see cref="Result"/> into an HTTP response at the API edge (tech discussion T2, S4). Failures become
/// ProblemDetails whose <c>code</c> the web app translates; validation errors list the fields.
/// </summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error.Kind == ErrorKind.Validation && error.FieldErrors is { Count: > 0 } fields)
        {
            return Results.ValidationProblem(
                fields.ToDictionary(f => f.Key, f => f.Value),
                detail: error.Message,
                extensions: extensions);
        }

        return Results.Problem(detail: error.Message, statusCode: StatusCodeFor(error.Kind), extensions: extensions);
    }

    public static int StatusCodeFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}
