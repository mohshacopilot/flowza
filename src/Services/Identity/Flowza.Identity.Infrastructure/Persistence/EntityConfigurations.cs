using Flowza.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flowza.Identity.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        // The application creates the ID (time-ordered, SQL Server order) so the domain events can carry it.
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Username).HasColumnName("UserName").HasMaxLength(Username.MaxLength).IsRequired();
        builder.Property(u => u.NormalizedUsername).HasColumnName("NormalizedUserName").HasMaxLength(Username.MaxLength).IsRequired();
        builder.HasIndex(u => u.NormalizedUsername).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();

        builder.Property(u => u.Email).HasMaxLength(Email.MaxLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(Email.MaxLength).IsRequired();
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();

        builder.Property(u => u.Mobile).HasMaxLength(User.MobileMaxLength);
        builder.Property(u => u.IsActive);
        builder.Property(u => u.MustChangePassword);
        builder.Property(u => u.PasswordChangedAt);

        builder.Property<string?>(UserCredentialColumns.PasswordHash).HasMaxLength(256);
        builder.Property<string?>(UserCredentialColumns.SecurityStamp).HasMaxLength(64);
        builder.Property<int>(UserCredentialColumns.AccessFailedCount);
        builder.Property<DateTimeOffset?>(UserCredentialColumns.LockoutEnd);
        builder.Property<bool>(UserCredentialColumns.LockoutEnabled).HasDefaultValue(true);

        builder.OwnsMany(u => u.RoleAssignments, roles =>
        {
            roles.ToTable("UserRoles");
            roles.WithOwner().HasForeignKey("UserId");
            roles.Property(r => r.RoleId);
            roles.HasKey("UserId", nameof(UserRoleAssignment.RoleId));
            roles.HasOne<RoleRecord>().WithMany().HasForeignKey(r => r.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(u => u.RoleAssignments).HasField("_roleAssignments").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(u => u.Roles);
        builder.Ignore(u => u.IsActivePlatformAdmin);
        builder.Ignore(u => u.DomainEvents);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<RoleRecord>
{
    public void Configure(EntityTypeBuilder<RoleRecord> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.Property(r => r.NormalizedName).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.NormalizedName).IsUnique();

        // The three system roles are fixed (IAM-02); seeding them in the migration keeps their IDs the same everywhere.
        builder.HasData(SystemRole.All.Select(r => new RoleRecord
        {
            Id = r.Id,
            Name = r.Name,
            NormalizedName = r.Name.ToUpperInvariant(),
        }));
    }
}

internal sealed class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistoryRecord>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryRecord> builder)
    {
        builder.ToTable("PasswordHistory");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PasswordHash).HasMaxLength(256).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.UserId, p.ChangedAt });
    }
}

internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKeyRecord>
{
    public void Configure(EntityTypeBuilder<DataProtectionKeyRecord> builder)
    {
        builder.ToTable("DataProtectionKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.FriendlyName).HasMaxLength(200);
        builder.Property(k => k.Xml).IsRequired();
    }
}
