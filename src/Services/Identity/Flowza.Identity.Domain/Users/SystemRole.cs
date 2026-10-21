namespace Flowza.Identity.Domain.Users;

/// <summary>
/// What part of the platform a user may use (IAM-02). A fixed set with fixed IDs, so the roles table can be seeded
/// and the role names in tokens never change. Business roles (who approves) belong to ORG, not here.
/// </summary>
public sealed record SystemRole
{
    private SystemRole(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public static SystemRole PlatformAdmin { get; } = new(new Guid("8f1c2a10-0000-4000-8000-000000000001"), "PlatformAdmin");

    public static SystemRole AppDesigner { get; } = new(new Guid("8f1c2a10-0000-4000-8000-000000000002"), "AppDesigner");

    /// <summary>Every active user has this role (IAM-02).</summary>
    public static SystemRole User { get; } = new(new Guid("8f1c2a10-0000-4000-8000-000000000003"), "User");

    public static IReadOnlyList<SystemRole> All { get; } = [PlatformAdmin, AppDesigner, User];

    public Guid Id { get; }

    /// <summary>Stable name, used in tokens, policies and as the translation key in the web app.</summary>
    public string Name { get; }

    public static SystemRole? FromName(string? name) =>
        All.FirstOrDefault(r => string.Equals(r.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static SystemRole FromId(Guid id) =>
        All.FirstOrDefault(r => r.Id == id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown system role.");

    public override string ToString() => Name;
}
