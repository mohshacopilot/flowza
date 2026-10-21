using Flowza.Common.Results;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users;

/// <summary>Turns role names from a request into system roles; an unknown name is a validation error (constitution 7.3).</summary>
internal static class RoleNames
{
    public static Error UnknownRole { get; } = Error.Validation(
        "users.role_unknown",
        "A role is not one of PlatformAdmin, AppDesigner or User.",
        new Dictionary<string, string[]> { ["roles"] = ["users.role_unknown"] });

    public static Result<IReadOnlyList<SystemRole>> Parse(IEnumerable<string>? names)
    {
        var roles = new List<SystemRole>();
        foreach (var name in names ?? [])
        {
            var role = SystemRole.FromName(name);
            if (role is null)
            {
                return UnknownRole;
            }

            roles.Add(role);
        }

        return roles;
    }
}
