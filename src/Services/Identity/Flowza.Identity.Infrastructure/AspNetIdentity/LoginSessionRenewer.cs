using Flowza.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Identity.Infrastructure.AspNetIdentity;

/// <summary>
/// Issues a new login cookie after the user's own password change. The change gives the user a new security stamp,
/// which ends the old cookie; the user is read in a fresh scope so the new cookie carries the new stamp (the request's
/// own DbContext may still hold the user as it was when the request began).
/// </summary>
public sealed class LoginSessionRenewer(IServiceScopeFactory scopeFactory)
{
    public async Task RenewAsync(Guid userId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("The user who just changed their password was not found.");
        await scope.ServiceProvider.GetRequiredService<SignInManager<User>>().SignInAsync(user, isPersistent: false);
    }
}
