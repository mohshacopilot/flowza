using Serilog.Core;
using Serilog.Events;

namespace Flowza.ServiceDefaults.Logging;

/// <summary>
/// Second line of the privacy guard (tech discussion T7, OB2; constitution 11.2): replaces the value of any log
/// property whose name looks personal or secret (password, email, mobile, form data, token, …) with "***",
/// also inside structured objects, dictionaries and lists. The first line is to log IDs only.
/// </summary>
public sealed class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public const string Mask = "***";

    private static readonly LogEventPropertyValue s_mask = new ScalarValue(Mask);

    private static readonly string[] s_sensitiveNameParts =
    [
        "password", "secret", "token", "apikey", "connectionstring", "credential",
        "email", "mobile", "phone", "formdata", "formvalue", "fieldvalue",
        "pan", "aadhaar", "address", "dateofbirth", "dob",
        "username", "displayname", "fullname", "firstname", "lastname",
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            var masked = IsSensitive(name) ? s_mask : MaskInside(value);
            if (!ReferenceEquals(masked, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, masked));
            }
        }
    }

    public static bool IsSensitive(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        var normalized = propertyName.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        // "pan" only as a whole word (e.g. "Pan", "PanNumber"), not inside "company" or "span".
        return s_sensitiveNameParts.Any(part => part == "pan"
            ? normalized is "pan" or "pannumber" or "panno"
            : normalized.Contains(part, StringComparison.Ordinal));
    }

    private static LogEventPropertyValue MaskInside(LogEventPropertyValue value) => value switch
    {
        StructureValue structure => MaskStructure(structure),
        DictionaryValue dictionary => MaskDictionary(dictionary),
        SequenceValue sequence => MaskSequence(sequence),
        _ => value,
    };

    private static StructureValue MaskStructure(StructureValue structure)
    {
        var changed = false;
        var properties = structure.Properties.Select(p =>
        {
            var masked = IsSensitive(p.Name) ? s_mask : MaskInside(p.Value);
            changed |= !ReferenceEquals(masked, p.Value);
            return new LogEventProperty(p.Name, masked);
        }).ToList();
        return changed ? new StructureValue(properties, structure.TypeTag) : structure;
    }

    private static DictionaryValue MaskDictionary(DictionaryValue dictionary)
    {
        var changed = false;
        var elements = dictionary.Elements.Select(e =>
        {
            var key = e.Key.Value?.ToString() ?? string.Empty;
            var masked = IsSensitive(key) ? s_mask : MaskInside(e.Value);
            changed |= !ReferenceEquals(masked, e.Value);
            return new KeyValuePair<ScalarValue, LogEventPropertyValue>(e.Key, masked);
        }).ToList();
        return changed ? new DictionaryValue(elements) : dictionary;
    }

    private static SequenceValue MaskSequence(SequenceValue sequence)
    {
        var changed = false;
        var elements = sequence.Elements.Select(e =>
        {
            var masked = MaskInside(e);
            changed |= !ReferenceEquals(masked, e);
            return masked;
        }).ToList();
        return changed ? new SequenceValue(elements) : sequence;
    }
}
