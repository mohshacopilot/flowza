namespace Flowza.Common.Data;

// Marker interfaces that let the persistence conventions add their columns as EF Core shadow properties, so
// domain classes stay free of persistence details (constitution 4.2) and cannot set audit values by mistake.

/// <summary>The entity gets <c>CreatedAt/By</c> and <c>UpdatedAt/By</c>, filled automatically on save (tech discussion T3, D1).</summary>
public interface IAuditable;

/// <summary>
/// The entity is never hard-deleted: deleting it sets <c>IsDeleted</c>, <c>DeletedAt</c>, <c>DeletedBy</c>, and it
/// disappears from normal queries (constitution 8.6).
/// </summary>
public interface ISoftDeletable;

/// <summary>The entity gets a <c>RowVersion</c> column for optimistic concurrency (constitution 8.5).</summary>
public interface IVersioned;
