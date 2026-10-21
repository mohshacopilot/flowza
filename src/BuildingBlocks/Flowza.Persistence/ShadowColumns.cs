namespace Flowza.Persistence;

/// <summary>Names of the columns that the conventions add as EF Core shadow properties.</summary>
public static class ShadowColumns
{
    public const string CreatedAt = nameof(CreatedAt);
    public const string CreatedBy = nameof(CreatedBy);
    public const string UpdatedAt = nameof(UpdatedAt);
    public const string UpdatedBy = nameof(UpdatedBy);
    public const string IsDeleted = nameof(IsDeleted);
    public const string DeletedAt = nameof(DeletedAt);
    public const string DeletedBy = nameof(DeletedBy);
    public const string RowVersion = nameof(RowVersion);

    /// <summary>Name of the soft-delete query filter; pass it to <c>IgnoreQueryFilters</c> to include deleted rows.</summary>
    public const string SoftDeleteFilter = "SoftDelete";
}
