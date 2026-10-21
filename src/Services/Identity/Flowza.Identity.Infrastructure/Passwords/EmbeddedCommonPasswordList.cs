using Flowza.Identity.Domain.Users.Passwords;

namespace Flowza.Identity.Infrastructure.Passwords;

/// <summary>
/// The embedded top-10,000 list (PA7), loaded once into a hash set: O(1) per check, about 10,000 short strings in
/// memory. Letter case is ignored, so "Qwertyuiop" counts as common too.
/// </summary>
internal sealed class EmbeddedCommonPasswordList : ICommonPasswordList
{
    public const string ResourceName = "Flowza.Identity.Infrastructure.Passwords.common-passwords.txt";

    private static readonly Lazy<HashSet<string>> s_passwords = new(Load);

    public static int Count => s_passwords.Value.Count;

    public bool Contains(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return s_passwords.Value.Contains(password);
    }

    private static HashSet<string> Load()
    {
        using var stream = typeof(EmbeddedCommonPasswordList).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream);

        var passwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                passwords.Add(line);
            }
        }

        return passwords;
    }
}
