using System.Text.RegularExpressions;

namespace Flowza.Testing;

/// <summary>Splits a SQL script at "GO" lines, which only tools like SSMS and sqlcmd understand.</summary>
public static partial class SqlBatches
{
    public static IReadOnlyList<string> Split(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return [.. GoLine().Split(script).Where(batch => !string.IsNullOrWhiteSpace(batch))];
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoLine();
}
