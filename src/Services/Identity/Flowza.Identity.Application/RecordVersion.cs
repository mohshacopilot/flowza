using Flowza.Common.Results;

namespace Flowza.Identity.Application;

/// <summary>
/// The row version of a record as the screens see it (base64 text). A change sends back the version it last read; a
/// newer save by someone else makes it a conflict (constitution 8.5). Shared by users and the organization's records.
/// </summary>
public static class RecordVersion
{
    public static Error Required { get; } = Error.Validation(
        "common.version_required",
        "The version of the record you edited is missing.",
        new Dictionary<string, string[]> { ["version"] = ["common.version_required"] });

    public static string Encode(byte[]? rowVersion) => rowVersion is null ? string.Empty : Convert.ToBase64String(rowVersion);

    public static byte[]? Decode(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var buffer = new byte[version.Length];
        return Convert.TryFromBase64String(version, buffer, out var written) && written == 8 ? buffer[..written] : null;
    }
}
