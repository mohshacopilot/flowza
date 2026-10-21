using Flowza.Common.Results;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// Saves a use case's changes in one transaction and turns the database failures we expect into errors
/// (constitution 8.5; task T035): a newer row version is a conflict, a duplicate username or email (a race the
/// use case's own check could not see) is the same validation error as that check gives.
/// </summary>
internal sealed class UnitOfWork(IdentityServiceDbContext context) : IUnitOfWork
{
    // SQL Server error numbers for a duplicate key in a unique index or constraint.
    private const int DuplicateKeyInUniqueIndex = 2601;
    private const int DuplicateKeyInUniqueConstraint = 2627;

    public static Error ConcurrencyConflict { get; } = Error.Conflict(
        "common.changed_by_someone_else",
        "Someone else changed this record in the meantime. Reload it and try again.");

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException
        {
            Number: DuplicateKeyInUniqueIndex or DuplicateKeyInUniqueConstraint,
        } sql)
        {
            // Only the index name is read; the message also quotes the duplicate value, which must not travel further.
            return sql.Message.Contains("NormalizedEmail", StringComparison.Ordinal) ? UserErrors.EmailTaken
                : sql.Message.Contains("NormalizedUserName", StringComparison.Ordinal) ? UserErrors.UsernameTaken
                : throw new InvalidOperationException("A unique index was violated.", exception);
        }
    }
}
