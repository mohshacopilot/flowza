using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flowza.Identity.Infrastructure.Persistence;

// The organization's tables (plan 002 §5.2, PA4). Codes are stored in upper case, so a plain unique index enforces
// "unique ignoring case" (P7) with any collation. Nothing here is ever deleted (NFR-08), apart from membership rows.

/// <summary>The installed root unit type, root unit and org-structure row (C1, PA5), seeded by the migration.</summary>
internal static class OrgSeed
{
    // A fixed creation time, so the seed never changes the model (and never causes a new migration).
    public static readonly DateTime CreatedAt = new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
}

internal sealed class UnitTypeConfiguration : IEntityTypeConfiguration<UnitType>
{
    public void Configure(EntityTypeBuilder<UnitType> builder)
    {
        builder.ToTable("UnitTypes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Code).HasMaxLength(OrgCode.MaxLength).IsRequired();
        builder.HasIndex(t => t.Code).IsUnique();
        builder.Property(t => t.Name).HasMaxLength(OrgName.MaxLength).IsRequired();
        builder.Property(t => t.IsActive);
        builder.Ignore(t => t.DomainEvents);

        builder.HasData(new
        {
            Id = OrgRoot.UnitTypeId,
            Code = OrgCode.FromStored(OrgRoot.Code),
            Name = OrgRoot.Name,
            IsActive = true,
            CreatedAt = OrgSeed.CreatedAt,
        });
    }
}

internal sealed class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    /// <summary>The filtered unique index that allows a single unit without a parent (ORG-02-AC5).</summary>
    public const string OneRootIndex = "IX_OrgUnits_OneRoot";

    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        builder.ToTable("OrgUnits");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Number).ValueGeneratedNever();
        builder.HasIndex(u => u.Number).IsUnique();
        builder.Property(u => u.Code).HasMaxLength(OrgCode.MaxLength).IsRequired();
        builder.HasIndex(u => u.Code).IsUnique();
        builder.Property(u => u.Name).HasMaxLength(OrgName.MaxLength).IsRequired();
        builder.Property(u => u.IsActive);
        builder.Ignore(u => u.IsRoot);
        builder.Ignore(u => u.DomainEvents);

        // Digits and separators only: varchar keeps the column indexable at 900 bytes (PA4). "Below a unit" is a range
        // seek on this index; the included columns answer the rules' questions without a lookup (plan §8).
        builder.Property(u => u.Path).HasMaxLength(UnitPath.MaxLength).IsUnicode(false).IsRequired();
        builder.HasIndex(u => u.Path).IncludeProperties(u => new { u.UnitTypeId, u.HeadUserId, u.IsActive });

        builder.HasIndex(u => u.ParentId);
        builder.HasIndex(u => u.ParentId, OneRootIndex).IsUnique().HasFilter("[ParentId] IS NULL");
        builder.HasIndex(u => u.HeadUserId);

        builder.HasOne<UnitType>().WithMany().HasForeignKey(u => u.UnitTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrgUnit>().WithMany().HasForeignKey(u => u.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(u => u.HeadUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new
        {
            Id = OrgRoot.UnitId,
            Number = OrgRoot.UnitNumber,
            Code = OrgCode.FromStored(OrgRoot.Code),
            Name = OrgRoot.Name,
            UnitTypeId = OrgRoot.UnitTypeId,
            Path = UnitPath.ForRoot(OrgRoot.UnitNumber),
            IsActive = true,
            CreatedAt = OrgSeed.CreatedAt,
        });
    }
}

internal sealed class BusinessRoleConfiguration : IEntityTypeConfiguration<BusinessRole>
{
    public void Configure(EntityTypeBuilder<BusinessRole> builder)
    {
        builder.ToTable("BusinessRoles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Code).HasMaxLength(OrgCode.MaxLength).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasMaxLength(OrgName.MaxLength).IsRequired();
        builder.Property(r => r.IsActive);
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class OrgStructureConfiguration : IEntityTypeConfiguration<OrgStructure>
{
    public void Configure(EntityTypeBuilder<OrgStructure> builder)
    {
        builder.ToTable("OrgStructure");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.ChangeCount);
        builder.Property(s => s.ChangedAt);

        builder.HasData(new { Id = OrgStructure.SingletonId, ChangeCount = 0L });
    }
}

/// <summary>The user's place in the organization (spec 001 amendment 1): primary unit, manager, business roles.</summary>
internal static class UserOrgConfiguration
{
    public static void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.PrimaryUnitId).IsRequired();
        builder.HasIndex(u => u.PrimaryUnitId).IncludeProperties(u => u.IsActive);
        builder.HasOne<OrgUnit>().WithMany().HasForeignKey(u => u.PrimaryUnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.ManagerId);
        builder.HasOne<User>().WithMany().HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);

        // Like system roles (001): one row per role held; "Remove member" removes the row (plan §5.2).
        builder.OwnsMany(u => u.BusinessRoleAssignments, roles =>
        {
            roles.ToTable("UserBusinessRoles");
            roles.WithOwner().HasForeignKey("UserId");
            roles.Property(r => r.BusinessRoleId);
            roles.HasKey("UserId", nameof(UserBusinessRoleAssignment.BusinessRoleId));
            roles.HasIndex(nameof(UserBusinessRoleAssignment.BusinessRoleId), "UserId"); // members of a role (rule f)
            roles.HasOne<BusinessRole>().WithMany().HasForeignKey(r => r.BusinessRoleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(u => u.BusinessRoleAssignments).HasField("_businessRoleAssignments").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(u => u.BusinessRoleIds);
    }
}

/// <summary>The sequence of unit numbers (plan §5.2): the root has 1, new units start at 2.</summary>
internal static class UnitNumberSequence
{
    public const string Name = "UnitNumbers";

    public static void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.HasSequence<int>(Name).StartsAt(OrgRoot.UnitNumber + 1);
}
