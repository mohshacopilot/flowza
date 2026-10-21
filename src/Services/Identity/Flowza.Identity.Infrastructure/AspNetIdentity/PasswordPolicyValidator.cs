using Flowza.Identity.Domain.Users;
using Flowza.Identity.Domain.Users.Passwords;
using Microsoft.AspNetCore.Identity;

namespace Flowza.Identity.Infrastructure.AspNetIdentity;

/// <summary>Runs the domain <see cref="PasswordPolicy"/> wherever ASP.NET Core Identity sets a password (adapter, IAM-06).</summary>
internal sealed class PasswordPolicyValidator(PasswordPolicy policy) : IPasswordValidator<User>
{
    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        ArgumentNullException.ThrowIfNull(user);
        var result = policy.Check(password, user.Username.Value);
        if (result.IsSuccess)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        var errors = result.Error.FieldErrors![PasswordPolicy.Field]
            .Select(code => new IdentityError { Code = code, Description = code });
        return Task.FromResult(IdentityResult.Failed([.. errors]));
    }
}
