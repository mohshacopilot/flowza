using Flowza.Common.Data;
using Flowza.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Flowza.Persistence;

/// <summary>
/// Before every save: fills the audit columns, and turns deletes of soft-deletable entities into updates
/// (tech discussion T3, D1; constitution 8.6). Times come from <see cref="TimeProvider"/> (D2).
/// </summary>
public sealed class AuditSaveChangesInterceptor(TimeProvider timeProvider, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable)
            {
                SoftDelete(entry, now, userId);
            }

            if (entry.Entity is not IAuditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(ShadowColumns.CreatedAt).CurrentValue = now;
                    entry.Property(ShadowColumns.CreatedBy).CurrentValue = userId;
                    entry.Property(ShadowColumns.UpdatedAt).CurrentValue = null;
                    entry.Property(ShadowColumns.UpdatedBy).CurrentValue = null;
                    break;
                case EntityState.Modified:
                    // The creation stamp can never be changed by an update.
                    entry.Property(ShadowColumns.CreatedAt).IsModified = false;
                    entry.Property(ShadowColumns.CreatedBy).IsModified = false;
                    entry.Property(ShadowColumns.UpdatedAt).CurrentValue = now;
                    entry.Property(ShadowColumns.UpdatedBy).CurrentValue = userId;
                    break;
            }
        }
    }

    private static void SoftDelete(EntityEntry entry, DateTime now, Guid? userId)
    {
        entry.State = EntityState.Modified;
        foreach (var property in entry.Properties)
        {
            property.IsModified = false;
        }

        entry.Property(ShadowColumns.IsDeleted).CurrentValue = true;
        entry.Property(ShadowColumns.IsDeleted).IsModified = true;
        entry.Property(ShadowColumns.DeletedAt).CurrentValue = now;
        entry.Property(ShadowColumns.DeletedAt).IsModified = true;
        entry.Property(ShadowColumns.DeletedBy).CurrentValue = userId;
        entry.Property(ShadowColumns.DeletedBy).IsModified = true;
    }
}
