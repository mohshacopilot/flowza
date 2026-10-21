using Flowza.Identity.Domain.Users;
using Flowza.Identity.Infrastructure.AspNetIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Flowza.Identity.Api.Pages.Account;

/// <summary>
/// Identity's login page (tech discussion SE2; IAM-01, IAM-07): passwords never pass through the web app. Wrong
/// username, wrong password and inactive user all get one general message (IAM-01-AC2); a locked account says so
/// (IAM-07-AC1).
/// </summary>
[AllowAnonymous]
public sealed class LoginModel(PasswordLogin passwordLogin, SignInManager<User> signInManager, IStringLocalizer<AccountTexts> texts) : PageModel
{
    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var check = await passwordLogin.CheckAsync(Username, Password, cancellationToken);
        Password = null;

        switch (check.Outcome)
        {
            case LoginOutcome.Succeeded:
                await signInManager.SignInAsync(check.User!, isPersistent: false);
                var target = SafeReturnUrl();
                return check.MustChangePassword
                    ? Redirect($"/account/change-password?returnUrl={Uri.EscapeDataString(target)}")
                    : LocalRedirect(target);

            case LoginOutcome.LockedOut:
                ErrorMessage = texts[AccountTexts.LoginLockedOut];
                return Page();

            default:
                ErrorMessage = texts[AccountTexts.LoginInvalid];
                return Page();
        }
    }

    // Only addresses on this site, so the login page cannot be used to send people elsewhere (open redirect).
    private string SafeReturnUrl() => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
}
