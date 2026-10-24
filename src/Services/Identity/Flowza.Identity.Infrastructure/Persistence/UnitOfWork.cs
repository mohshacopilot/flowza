using System.Diagnostics;
using Flowza.Common.Domain;
using Flowza.Common.Results;
using Flowza.Common.Security;
using Flowza.Contracts;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Application.Events;
using Flowza.Identity.Domain.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

/// <summary>
/// Saves a use case's changes and its integration events in one transaction through Wolverine's EF Core outbox
/// (constitution 3.3; IAM-L5), then sends the events. The events come from the domain events of every changed aggregate
/// (users, unit types, org units, business roles) plus any added with <see cref="AddEvent"/>. Database failures we
/// expect become errors (constitution 8.5; task T035): a newer row version is a conflict, a duplicate in a unique index
/// (a race the use case's own check could not see) is the same validation error as that check gives.
/// </summary>
internal sealed class UnitOfWork(
    IDbContextOutbox<IdentityServiceDbContext> outbox,
    TimeProvider timeProvider,
    ICurrentUser currentUser) : IUnitOfWork
{
    // SQL Server error numbers for a duplicate key in a unique index or constraint.
    private const int DuplicateKeyInUniqueIndex = 2601;
    private const int DuplicateKeyInUniqueConstraint = 2627;

    /// <summary>
    /// The unique indexes whose duplicates are expected, by a part of the index name that SQL Server quotes in its
    /// message, and the error each one gives. A new unique value adds a row here, nothing else changes.
    /// </summary>
    private static readonly (string IndexNamePart, Error Error)[] s_duplicateErrors =
    [
        ("NormalizedEmail", UserErrors.EmailTaken),
        ("NormalizedUserName", UserErrors.UsernameTaken),
    ];

    private readonly List<IntegrationEvent> _events = [];

    public static Error ConcurrencyConflict { get; } = Error.Conflict(
        "common.changed_by_someone_else",
        "Someone else changed this record in the meantime. Reload it and try again.");

    public void AddEvent(IntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _events.Add(integrationEvent);
    }

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        var context = outbox.DbContext;
        var aggregates = context.ChangeTracker.Entries<AggregateRoot>().Select(e => e.Entity).ToList();
        var events = aggregates.SelectMany(a => a.DomainEvents).Select(IdentityEventMapper.Map).Concat(_events).ToList();

        var now = timeProvider.GetUtcNow();
        var correlationId = Activity.Current?.TraceId.ToHexString();
        foreach (var integrationEvent in events)
        {
            // The event itself names the user; the actor is who did it (the user at login, an admin otherwise).
            await outbox.PublishAsync(integrationEvent with
            {
                OccurredAt = now,
                ActorId = currentUser.UserId ?? ActorOf(integrationEvent),
                CorrelationId = correlationId,
            });
        }

        try
        {
            await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failed(ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException
        {
            Number: DuplicateKeyInUniqueIndex or DuplicateKeyInUniqueConstraint,
        } sql)
        {
            // Only the index name is read; the message also quotes the duplicate value, which must not travel further.
            return Failed(DuplicateError(sql.Message) ?? throw new InvalidOperationException("A unique index was violated.", exception));
        }

        _events.Clear();
        aggregates.ForEach(a => a.ClearDomainEvents());
        return Result.Success();
    }

    private static Error? DuplicateError(string sqlMessage) =>
        Array.Find(s_duplicateErrors, d => sqlMessage.Contains(d.IndexNamePart, StringComparison.Ordinal)).Error;

    // Login events happen before anyone is logged in; the user themselves is the actor.
    private static Guid? ActorOf(IntegrationEvent integrationEvent) => integrationEvent switch
    {
        Contracts.Identity.Events.V1.UserLoggedIn e => e.UserId,
        Contracts.Identity.Events.V1.UserLoginFailed e => e.UserId,
        Contracts.Identity.Events.V1.UserLockedOut e => e.UserId,
        _ => null,
    };

    // The failed changes (and the events with them) are dropped, so no later save of this scope can try them again:
    // the use case has failed as a whole.
    private Result Failed(Error error)
    {
        outbox.DbContext.ChangeTracker.Clear();
        _events.Clear();
        return error;
    }
}
