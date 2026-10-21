using Flowza.Common.Security;

namespace Flowza.Testing;

/// <summary>A current user the test sets.</summary>
public sealed class FixedCurrentUser(Guid? userId) : ICurrentUser
{
    public Guid? UserId { get; set; } = userId;
}
