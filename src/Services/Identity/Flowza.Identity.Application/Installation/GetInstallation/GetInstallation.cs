using Flowza.Identity.Application.Abstractions;

namespace Flowza.Identity.Application.Installation.GetInstallation;

/// <summary>The installation's name and initials for the web app's frame (UI-08; plan 007 §5.3 <c>GET /api/v1/installation</c>).</summary>
public sealed record GetInstallationQuery;

public sealed record InstallationView(string Name, string Initials);

public sealed class GetInstallationHandler(IInstallationBrandSource brands)
{
    public async Task<InstallationView> Handle(GetInstallationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var brand = await brands.GetAsync(cancellationToken);
        return new InstallationView(brand.Name, brand.Initials);
    }
}
