using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// Keeps the Data Protection keys (login cookie, anti-forgery, OpenIddict tokens) in <c>flowza_identity</c>, so they
/// survive restarts and are shared by several instances (plan 001 §5.2; IAM design note). Data Protection calls
/// this rarely (start-up and key rotation), so a short-lived scope per call is fine.
/// </summary>
internal sealed class DatabaseXmlRepository(IServiceScopeFactory scopeFactory) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        return [.. context.DataProtectionKeys.AsNoTracking().Select(k => k.Xml).AsEnumerable().Select(XElement.Parse)];
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        context.DataProtectionKeys.Add(new DataProtectionKeyRecord
        {
            FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting),
        });
        context.SaveChanges();
    }
}
