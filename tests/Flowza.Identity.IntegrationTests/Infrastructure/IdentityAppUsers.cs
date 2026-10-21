using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Identity.IntegrationTests.Infrastructure;

/// <summary>Test helpers that put users into <c>flowza_test_identity</c> through the service's own code.</summary>
public static class IdentityAppUsers
{
    public const string Password = "Correct-Horse-42";

    public static async Task<User> CreateUserAsync(
        this IdentityApp app,
        string username,
        string? password = Password,
        bool mustChangePassword = false,
        params SystemRole[] roles)
    {
        ArgumentNullException.ThrowIfNull(app);
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var user = User.Create(Guid.NewGuid(), username, $"Test {username}", $"{username}@example.org", null, roles).Value;
        services.GetRequiredService<IUserRepository>().Add(user);
        if (password is not null)
        {
            (await services.GetRequiredService<IUserCredentials>().SetPasswordAsync(user, password, default)).IsSuccess.ShouldBeTrue();
            if (!mustChangePassword)
            {
                user.PasswordChanged(DateTimeOffset.UtcNow);
            }
        }

        (await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default)).IsSuccess.ShouldBeTrue();
        return user;
    }

    /// <summary>Runs <paramref name="action"/> in its own scope, like one request.</summary>
    public static async Task<T> InScopeAsync<T>(this IdentityApp app, Func<IServiceProvider, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }
}
