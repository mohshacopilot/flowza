using System.Security.Claims;
using Flowza.Common.Results;
using Flowza.Identity.Application.Me.ChangeOwnPassword;
using Flowza.Identity.Infrastructure.AspNetIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Wolverine;

namespace Flowza.Identity.Api.Pages.Account;

/// <summary>
/// The forced password change after the first login of the first admin (IAM-03) and after an admin reset (IAM-04-AC2):
/// the code flow does not continue until the new password is saved.
/// </summary>
[Authorize(AuthenticationSchemes = LoginSchemes.Cookie)]
public sealed class ChangePasswordModel(
    IMessageBus bus,
    LoginSessionRenewer loginSession,
    IStringLocalizer<AccountTexts> texts) : PageModel
{
    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IReadOnlyList<string> Errors { get; private set; } = [];

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            return Failed([texts[AccountTexts.ChangePasswordMismatch]]);
        }

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await bus.InvokeAsync<Result>(new ChangeOwnPasswordCommand(userId, CurrentPassword, NewPassword), cancellationToken);
        if (result.IsFailure)
        {
            return Failed(MessagesFor(result.Error));
        }

        // The new password gave the user a new security stamp; renew this login so it stays valid.
        await loginSession.RenewAsync(userId);
        return LocalRedirect(!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
    }

    private PageResult Failed(IReadOnlyList<string> errors)
    {
        CurrentPassword = NewPassword = ConfirmPassword = null;
        Errors = errors;
        return Page();
    }

    private List<string> MessagesFor(Error error)
    {
        var codes = error.FieldErrors?.SelectMany(f => f.Value).Distinct().ToList() ?? [error.Code];
        return [.. codes.Select(code => texts[code].ResourceNotFound ? error.Message : texts[code].Value)];
    }
}

/// <summary>Authentication schemes of Identity's own pages.</summary>
public static class LoginSchemes
{
    /// <summary>Same value as <c>IdentityConstants.ApplicationScheme</c>, as a constant for attributes.</summary>
    public const string Cookie = "Identity.Application";
}
