using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Flowza.ServiceDefaults.Logging;

/// <summary>
/// Third line of the privacy guard for exceptions (task T035; constitution 11.2): exception messages can quote data,
/// e.g. SQL Server's "The duplicate key value is (ravi@bank.in)". Before any sink writes a warning, an error or an
/// event with an exception, the exception is replaced by a copy with such values masked, and so are text properties
/// (EF Core, for one, also puts the exception text into its message as a property). Stack traces and exception types
/// stay, so support can still find the cause. Normal information events pass untouched (no cost).
/// </summary>
public sealed class ExceptionMaskingSink(ILogEventSink inner) : ILogEventSink, IDisposable
{
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        if (logEvent.Exception is null && logEvent.Level < LogEventLevel.Warning)
        {
            inner.Emit(logEvent);
            return;
        }

        inner.Emit(new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Exception is null ? null : new MaskedException(logEvent.Exception),
            logEvent.MessageTemplate,
            logEvent.Properties.Select(p => new LogEventProperty(p.Key, MaskText(p.Value))),
            logEvent.TraceId ?? default,
            logEvent.SpanId ?? default));
    }

    public void Dispose() => (inner as IDisposable)?.Dispose();

    private static LogEventPropertyValue MaskText(LogEventPropertyValue value) =>
        value is ScalarValue { Value: string text } ? new ScalarValue(SensitiveTextMasker.Apply(text)) : value;
}

/// <summary>Stands in for a logged exception; its text is the original's with values masked.</summary>
public sealed class MaskedException : Exception
{
    private readonly string _text;

    public MaskedException(Exception original)
        : base(SensitiveTextMasker.Apply(original?.Message ?? string.Empty))
    {
        ArgumentNullException.ThrowIfNull(original);
        OriginalType = original.GetType().FullName ?? original.GetType().Name;
        _text = SensitiveTextMasker.Apply(original.ToString());
        HResult = original.HResult;
    }

    public MaskedException()
        : this(new InvalidOperationException())
    {
    }

    public MaskedException(string message)
        : this(new InvalidOperationException(message))
    {
    }

    public MaskedException(string message, Exception innerException)
        : this(new InvalidOperationException(message, innerException))
    {
    }

    public string OriginalType { get; }

    public override string ToString() => _text;
}

/// <summary>Masks values that database and framework error texts quote.</summary>
public static partial class SensitiveTextMasker
{
    private const string Mask = SensitiveDataMaskingEnricher.Mask;

    public static string Apply(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = DuplicateKeyValue().Replace(text, $"The duplicate key value is ({Mask}).");
        text = QuotedValue().Replace(text, $"$1 '{Mask}'");
        text = TruncatedValue().Replace(text, $"Truncated value: '{Mask}'");
        text = EmailAddress().Replace(text, Mask);
        return text;
    }

    // SQL Server 2601/2627: "... The duplicate key value is (RAVI@BANK.IN)."
    [GeneratedRegex(@"The duplicate key value is \(.*?\)\.", RegexOptions.Singleline)]
    private static partial Regex DuplicateKeyValue();

    // SQL Server 245 and similar: "Conversion failed when converting the nvarchar value 'abc' to data type int."
    [GeneratedRegex(@"(\b(?:n?varchar|n?char) value) '[^']*'", RegexOptions.IgnoreCase)]
    private static partial Regex QuotedValue();

    // SQL Server 2628: "String or binary data would be truncated ... Truncated value: 'abc'."
    [GeneratedRegex(@"Truncated value: '[^']*'")]
    private static partial Regex TruncatedValue();

    // Anything else that looks like an email address.
    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailAddress();
}
