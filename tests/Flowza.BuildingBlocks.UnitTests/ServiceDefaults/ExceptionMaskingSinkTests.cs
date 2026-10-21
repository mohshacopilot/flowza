using Flowza.ServiceDefaults.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Flowza.BuildingBlocks.UnitTests.ServiceDefaults;

/// <summary>T035 / PLT-05-AC3: exception texts reach the log sinks with the values they quote masked.</summary>
public sealed class ExceptionMaskingSinkTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    [Theory]
    [InlineData(
        "Cannot insert duplicate key row in object 'dbo.Users' with unique index 'IX_Users_NormalizedEmail'. The duplicate key value is (RAVI@BANK.IN).",
        "RAVI@BANK.IN")]
    [InlineData("Conversion failed when converting the nvarchar value '9876501234' to data type int.", "9876501234")]
    [InlineData("String or binary data would be truncated in table 'dbo.Users', column 'Mobile'. Truncated value: '98765 01234 99'.", "98765 01234 99")]
    [InlineData("Mail to priya.secret@example.org failed.", "priya.secret@example.org")]
    public void PLT_05_AC3_values_quoted_in_exception_texts_are_masked(string message, string personalValue)
    {
        var masked = SensitiveTextMasker.Apply(message);

        masked.ShouldNotContain(personalValue);
        masked.ShouldContain("***");
    }

    [Fact]
    public void Index_names_and_other_useful_details_stay()
    {
        var masked = SensitiveTextMasker.Apply("Cannot insert duplicate key row in object 'dbo.Users' with unique index 'IX_Users_NormalizedEmail'. The duplicate key value is (X).");

        masked.ShouldContain("IX_Users_NormalizedEmail");
        masked.ShouldContain("dbo.Users");
    }

    [Fact]
    public void T035_the_sink_hands_on_a_masked_exception_with_message_type_and_stack_trace()
    {
        var capture = new CapturingSink();
        var sink = new ExceptionMaskingSink(capture);
        Exception original;
        try
        {
            throw new InvalidOperationException("outer", new InvalidOperationException("The duplicate key value is (ravi@bank.in)."));
        }
        catch (InvalidOperationException exception)
        {
            original = exception;
        }

        sink.Emit(new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, original, new MessageTemplateParser().Parse("Save failed"), []));

        var written = capture.Events.ShouldHaveSingleItem().Exception.ShouldBeOfType<MaskedException>();
        written.ToString().ShouldNotContain("ravi@bank.in");
        written.ToString().ShouldContain(nameof(T035_the_sink_hands_on_a_masked_exception_with_message_type_and_stack_trace));
        written.OriginalType.ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void T035_text_properties_of_error_events_are_masked_too()
    {
        // EF Core logs "...{newline}{error}" with the exception text as a property.
        var capture = new CapturingSink();
        var template = new MessageTemplateParser().Parse("Saving failed.{newline}{error}");
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null, template,
            [new LogEventProperty("error", new ScalarValue("The duplicate key value is (RAVI@BANK.IN)."))]);

        new ExceptionMaskingSink(capture).Emit(logEvent);

        capture.Events.ShouldHaveSingleItem().Properties["error"].ToString().ShouldNotContain("RAVI@BANK.IN");
    }

    [Fact]
    public void Information_events_without_an_exception_pass_unchanged()
    {
        var capture = new CapturingSink();
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, new MessageTemplateParser().Parse("Hello"), []);

        new ExceptionMaskingSink(capture).Emit(logEvent);

        capture.Events.ShouldHaveSingleItem().ShouldBeSameAs(logEvent);
    }
}
