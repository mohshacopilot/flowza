using System.Linq.Expressions;
using Flowza.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Flowza.Persistence.Conventions;

/// <summary>
/// Adds Flowza's standard columns and rules to every entity that carries a marker interface:
/// audit columns (<see cref="IAuditable"/>), soft delete with a query filter (<see cref="ISoftDeletable"/>),
/// a row version (<see cref="IVersioned"/>), and sequential Guid keys for all entities.
/// </summary>
public static class FlowzaModelConventions
{
    private static readonly System.Reflection.MethodInfo s_efProperty =
        typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(bool));

    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => !t.IsOwned() && t.BaseType is null).ToList())
        {
            var clrType = entityType.ClrType;
            var entity = modelBuilder.Entity(clrType);

            if (typeof(IAuditable).IsAssignableFrom(clrType))
            {
                entity.Property<DateTime>(ShadowColumns.CreatedAt).IsRequired();
                entity.Property<Guid?>(ShadowColumns.CreatedBy);
                entity.Property<DateTime?>(ShadowColumns.UpdatedAt);
                entity.Property<Guid?>(ShadowColumns.UpdatedBy);
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                entity.Property<bool>(ShadowColumns.IsDeleted).IsRequired().HasDefaultValue(false);
                entity.Property<DateTime?>(ShadowColumns.DeletedAt);
                entity.Property<Guid?>(ShadowColumns.DeletedBy);
                entity.HasQueryFilter(ShadowColumns.SoftDeleteFilter, NotDeletedFilter(clrType));
            }

            if (typeof(IVersioned).IsAssignableFrom(clrType))
            {
                entity.Property<byte[]>(ShadowColumns.RowVersion).IsRowVersion();
            }

            UseSequentialGuidKey(entityType);
        }
    }

    private static LambdaExpression NotDeletedFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var isDeleted = Expression.Call(s_efProperty, parameter, Expression.Constant(ShadowColumns.IsDeleted));
        return Expression.Lambda(Expression.Not(isDeleted), parameter);
    }

    private static void UseSequentialGuidKey(IMutableEntityType entityType)
    {
        var key = entityType.FindPrimaryKey();
        if (key is { Properties: [var property] } && property.ClrType == typeof(Guid) && property.ValueGenerated == ValueGenerated.OnAdd)
        {
            property.SetValueGeneratorFactory((_, _) => new FlowzaGuidValueGenerator());
        }
    }
}
