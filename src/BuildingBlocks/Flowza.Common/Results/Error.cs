namespace Flowza.Common.Results;

/// <summary>The kind of an expected failure; the API edge maps it to an HTTP status (tech discussion T2, S4).</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    Failure,
}

/// <summary>
/// An expected business failure. <see cref="Code"/> is stable and used as the translation key in the web app;
/// <see cref="Message"/> is an English fallback. Never put personal data or form values in either
/// (constitution 11.2).
/// </summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorKind Kind,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(code, message, ErrorKind.Validation, fieldErrors);

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorKind.Unauthorized);

    public static Error Failure(string code, string message) => new(code, message, ErrorKind.Failure);
}
