using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Application.Installation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Flowza.Identity.Infrastructure.Installation;

/// <summary>
/// <c>Installation:Name</c>, set at installation (C7, PA2): <c>appsettings.json</c> holds the default "Flowza"; an
/// installation sets its own in <c>appsettings.Production.json</c> or the environment (<c>Installation__Name</c>).
/// </summary>
public sealed class InstallationOptions
{
    public const string Section = "Installation";

    /// <summary>Not set at all → "Flowza"; set but blank → the service refuses to start.</summary>
    public string? Name { get; set; } = InstallationBrand.DefaultName;
}

/// <summary>Refuses to start with a blank or too long installation name, instead of showing it wrong later.</summary>
public sealed class InstallationOptionsValidator : IValidateOptions<InstallationOptions>
{
    public ValidateOptionsResult Validate(string? name, InstallationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var brand = InstallationBrand.Create(options.Name);
        return brand.IsSuccess
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{InstallationOptions.Section}:Name: {brand.Error!.Message}");
    }
}

/// <summary>Adapter from configuration to the application's brand port; the brand is built once.</summary>
internal sealed class ConfiguredInstallationBrandSource(IOptions<InstallationOptions> options) : IInstallationBrandSource
{
    private readonly InstallationBrand _brand = InstallationBrand.Create(options.Value.Name).Value;

    public Task<InstallationBrand> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_brand);
}

public static class InstallationExtensions
{
    /// <summary>The installation name and initials mark (UI-08), checked when the service starts.</summary>
    public static TBuilder AddInstallationBrand<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddOptions<InstallationOptions>()
            .BindConfiguration(InstallationOptions.Section)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<InstallationOptions>, InstallationOptionsValidator>();
        builder.Services.AddSingleton<IInstallationBrandSource, ConfiguredInstallationBrandSource>();
        return builder;
    }
}
