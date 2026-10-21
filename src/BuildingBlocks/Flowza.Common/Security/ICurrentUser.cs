namespace Flowza.Common.Security;

/// <summary>Who is acting right now; <c>null</c> means the system (background jobs, message handlers without a user).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}

/// <summary>Used where no user is known: actions are recorded as done by the system.</summary>
public sealed class SystemUser : ICurrentUser
{
    public static SystemUser Instance { get; } = new();

    public Guid? UserId => null;
}
