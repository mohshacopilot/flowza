using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;
using Flowza.Messaging;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// The Identity service's own database, <c>flowza_identity</c> (plan 001 §5.2, PA3): users with their credential
/// columns, system roles, user roles, password history, OpenIddict's tables, Data Protection keys and Wolverine's
/// outbox/inbox; from feature 002 the organization (unit types, org units, business roles, the org-structure row).
/// </summary>
public sealed class IdentityServiceDbContext(DbContextOptions<IdentityServiceDbContext> options) : FlowzaDbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<UnitType> UnitTypes => Set<UnitType>();

    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();

    public DbSet<BusinessRole> BusinessRoles => Set<BusinessRole>();

    public DbSet<OrgStructure> OrgStructure => Set<OrgStructure>();

    public DbSet<RoleRecord> Roles => Set<RoleRecord>();

    public DbSet<PasswordHistoryRecord> PasswordHistory => Set<PasswordHistoryRecord>();

    public DbSet<DataProtectionKeyRecord> DataProtectionKeys => Set<DataProtectionKeyRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Value objects are stored as their text (otherwise EF would take the records for entities).
        configurationBuilder.Properties<Username>().HaveConversion<UsernameConverter>();
        configurationBuilder.Properties<Email>().HaveConversion<EmailConverter>();
        configurationBuilder.Properties<OrgCode>().HaveConversion<OrgCodeConverter>();
        configurationBuilder.Properties<UnitPath>().HaveConversion<UnitPathConverter>();
    }

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Roles are stored as role IDs (user-roles table); the SystemRole values themselves are code, not rows.
        modelBuilder.Ignore<SystemRole>();

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new DataProtectionKeyConfiguration());

        // Feature 002: the organization (plan 002 §5.2).
        modelBuilder.ApplyConfiguration(new UnitTypeConfiguration());
        modelBuilder.ApplyConfiguration(new OrgUnitConfiguration());
        modelBuilder.ApplyConfiguration(new BusinessRoleConfiguration());
        modelBuilder.ApplyConfiguration(new OrgStructureConfiguration());
        UnitNumberSequence.Configure(modelBuilder);

        // OpenIddict's applications, authorizations, scopes and tokens, with Guid keys (T013).
        modelBuilder.UseOpenIddict<Guid>();

        // Wolverine's outbox/inbox tables, so business changes and outgoing events share one transaction.
        modelBuilder.MapWolverineEnvelopeStorage(FlowzaMessagingExtensions.EnvelopeSchema);
    }
}
