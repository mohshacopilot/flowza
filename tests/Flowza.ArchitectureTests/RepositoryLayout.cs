using System.Xml.Linq;

namespace Flowza.ArchitectureTests;

/// <summary>Finds the repository root and reads the project references of every project under <c>src/</c>.</summary>
internal static class RepositoryLayout
{
    private static readonly Lazy<string> s_root = new(FindRoot);

    public static string Root => s_root.Value;

    /// <summary>Project name (file name without extension) → names of the projects it references.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> SourceProjectReferences()
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            var references = XDocument.Load(file)
                .Descendants("ProjectReference")
                .Select(e => (string?)e.Attribute("Include"))
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)))
                .ToList();
            result[Path.GetFileNameWithoutExtension(file)] = references;
        }

        return result;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Flowza.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Flowza.slnx was not found above the test output folder.");
    }
}
