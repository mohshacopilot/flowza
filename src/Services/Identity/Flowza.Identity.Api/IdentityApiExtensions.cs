using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Flowza.Identity.Api;

public static class IdentityApiExtensions
{
    /// <summary>Identity's own login session (cookie) while the user passes through login and the code flow.</summary>
    public const string LoginCookieName = "flowza.identity";

    public static readonly TimeSpan LoginCookieLifetime = TimeSpan.FromMinutes(30);

    public static WebApplicationBuilder AddIdentityLoginSession(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The cookies of ASP.NET Core Identity, with the security-stamp check (a deactivated user's cookie stops working).
        builder.Services.AddAuthentication().AddIdentityCookies();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.AccessDeniedPath = "/account/login";
            options.Cookie.Name = LoginCookieName;
            options.Cookie.HttpOnly = true;

            // Lax, not Strict: the browser arrives here through redirects from the Gateway and must bring this cookie.
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = LoginCookieLifetime;
            options.SlidingExpiration = true;
        });

        // Check the security stamp on every request, so deactivation and password resets end the login at once (K7).
        builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        return builder;
    }
}
