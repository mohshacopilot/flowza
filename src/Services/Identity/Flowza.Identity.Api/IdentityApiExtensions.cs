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

        // Login and change-password pages (SE2), texts from Resources/*.resx (constitution 12.1).
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.AddRazorPages();
        return builder;
    }

    /// <summary>English only in v0.1 (constitution 12.1); more cultures are added with their .resx files.</summary>
    public static readonly string[] SupportedCultures = ["en"];

    public static WebApplication UseIdentityLoginPages(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseRequestLocalization(options => options
            .SetDefaultCulture(SupportedCultures[0])
            .AddSupportedCultures(SupportedCultures)
            .AddSupportedUICultures(SupportedCultures));
        app.UseStaticFiles();
        return app;
    }
}
