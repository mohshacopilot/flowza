using Flowza.Identity.Domain.Users;
using Flowza.Messaging;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// The Identity service's own database, <c>flowza_identity</c> (plan 001 §5.2, PA3): users with their credential
/// columns, system roles, user roles, password history, OpenIddict's tables, Data Protection keys and Wolverine's
/// outbox/inbox.
/// </summary>
public sealed class IdentityServiceDbContext(DbContextOptions<IdentityServiceDbContext> options) : FlowzaDbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RoleRecord> Roles => Set<RoleRecord>();

    public DbSet<PasswordHistoryRecord> PasswordHistory => Set<PasswordHistoryRecord>();

    public DbSet<DataProtectionKeyRecord> DataProtectionKeys => Set<DataProtectionKeyRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Value objects are stored as their text (otherwise EF would take the records for entities).
        configurationBuilder.Properties<Username>().HaveConversion<UsernameConverter>();
        configurationBuilder.Properties<Email>().HaveConversion<EmailConverter>();
    }

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Roles are stored as role IDs (user-roles table); the SystemRole values themselves are code, not rows.
        modelBuilder.Ignore<SystemRole>();
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new DataProtectionKeyConfiguration());

        // OpenIddict's applications, authorizations, scopes and tokens, with Guid keys (T013).
        modelBuilder.UseOpenIddict<Guid>();

        // Wolverine's outbox/inbox tables, so business changes and outgoing events share one transaction.
        modelBuilder.MapWolverineEnvelopeStorage(FlowzaMessagingExtensions.EnvelopeSchema);
    }
}
