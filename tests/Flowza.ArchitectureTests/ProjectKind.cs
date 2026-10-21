namespace Flowza.ArchitectureTests;

/// <summary>What a project is, derived from its name (e.g. <c>Flowza.Identity.Domain</c>).</summary>
internal sealed record ProjectKind(string Name, string Category, string? Service, string? Layer)
{
    public static readonly string[] Services = ["Identity", "Design", "Runtime", "Worker"];
    public static readonly string[] BuildingBlocks = ["Flowza.Common", "Flowza.Contracts", "Flowza.Persistence", "Flowza.Messaging"];

    public static ProjectKind Of(string name)
    {
        if (BuildingBlocks.Contains(name))
        {
            return new(name, "BuildingBlock", null, null);
        }

        switch (name)
        {
            case "Flowza.ServiceDefaults":
                return new(name, "ServiceDefaults", null, null);
            case "Flowza.Gateway":
                return new(name, "Gateway", null, null);
            case "Flowza.AppHost":
                return new(name, "AppHost", null, null);
            case "Flowza.Worker":
                return new(name, "Service", "Worker", "Single");
        }

        var parts = name.Split('.');
        if (parts.Length == 3 && parts[0] == "Flowza" && Services.Contains(parts[1]))
        {
            return new(name, "Service", parts[1], parts[2]);
        }

        return new(name, "Unknown", null, null);
    }
}
