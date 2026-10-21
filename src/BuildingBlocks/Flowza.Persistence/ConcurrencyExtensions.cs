using Flowza.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Persistence;

public static class ConcurrencyExtensions
{
    /// <summary>Reads the current row version of a versioned entity (sent to the client with the data).</summary>
    public static byte[]? GetRowVersion<TEntity>(this DbContext context, TEntity entity)
        where TEntity : class, IVersioned
    {
        ArgumentNullException.ThrowIfNull(context);
        return (byte[]?)context.Entry(entity).Property(ShadowColumns.RowVersion).CurrentValue;
    }

    /// <summary>
    /// Tells EF Core which row version the client last saw. If someone saved in between, <c>SaveChanges</c> throws
    /// <see cref="DbUpdateConcurrencyException"/>, which the API turns into a clear "changed by someone else" message
    /// (constitution 8.5).
    /// </summary>
    public static void SetExpectedRowVersion<TEntity>(this DbContext context, TEntity entity, byte[] rowVersion)
        where TEntity : class, IVersioned
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rowVersion);
        context.Entry(entity).Property(ShadowColumns.RowVersion).OriginalValue = rowVersion;
    }
}
