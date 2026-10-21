using Flowza.Persistence;
using Flowza.Persistence.Conventions;
using Flowza.Testing.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Flowza.BuildingBlocks.UnitTests.Persistence;

/// <summary>Checks the model the conventions build; no database connection is opened.</summary>
public sealed class ModelConventionsTests
{
    private static readonly IModel s_model = CreateModel();

    [Fact]
    public void T3_D1_auditable_entity_gets_the_four_audit_columns()
    {
        var document = s_model.FindEntityType(typeof(TestDocument))!;

        document.FindProperty(ShadowColumns.CreatedAt)!.IsNullable.ShouldBeFalse();
        document.FindProperty(ShadowColumns.CreatedBy).ShouldNotBeNull();
        document.FindProperty(ShadowColumns.UpdatedAt).ShouldNotBeNull();
        document.FindProperty(ShadowColumns.UpdatedBy).ShouldNotBeNull();
        document.FindProperty(ShadowColumns.CreatedAt)!.IsShadowProperty().ShouldBeTrue();
    }

    [Fact]
    public void Constitution_8_6_soft_deletable_entity_gets_delete_columns_and_a_named_filter()
    {
        var document = s_model.FindEntityType(typeof(TestDocument))!;

        document.FindProperty(ShadowColumns.IsDeleted).ShouldNotBeNull();
        document.FindProperty(ShadowColumns.DeletedAt).ShouldNotBeNull();
        document.FindProperty(ShadowColumns.DeletedBy).ShouldNotBeNull();
        document.GetDeclaredQueryFilters().Select(f => f.Key).ShouldContain(ShadowColumns.SoftDeleteFilter);
    }

    [Fact]
    public void Constitution_8_5_versioned_entity_gets_a_row_version()
    {
        var rowVersion = s_model.FindEntityType(typeof(TestDocument))!.FindProperty(ShadowColumns.RowVersion)!;

        rowVersion.IsConcurrencyToken.ShouldBeTrue();
        rowVersion.ValueGenerated.ShouldBe(ValueGenerated.OnAddOrUpdate);
    }

    [Fact]
    public void Entity_without_markers_gets_no_extra_columns_or_filter()
    {
        var note = s_model.FindEntityType(typeof(TestNote))!;

        note.GetProperties().Select(p => p.Name).ShouldBe(["Id", "Text"], ignoreOrder: true);
        note.GetDeclaredQueryFilters().ShouldBeEmpty();
    }

    [Fact]
    public void T3_D3_guid_keys_use_the_sequential_generator()
    {
        var key = s_model.FindEntityType(typeof(TestNote))!.FindPrimaryKey()!.Properties.Single();

        key.GetValueGeneratorFactory().ShouldNotBeNull();
        key.GetValueGeneratorFactory()!(key, s_model.FindEntityType(typeof(TestNote))!).ShouldBeOfType<FlowzaGuidValueGenerator>();
    }

    [Fact]
    public void T3_D2_dates_are_stored_as_utc_datetime2()
    {
        var document = s_model.FindEntityType(typeof(TestDocument))!;

        document.FindProperty(nameof(TestDocument.IssuedAt))!.GetValueConverter().ShouldBeOfType<UtcDateTimeConverter>();
        var received = document.FindProperty(nameof(TestDocument.ReceivedAt))!;
        received.GetValueConverter().ShouldBeOfType<UtcDateTimeOffsetConverter>();
        received.GetColumnType().ShouldBe("datetime2");
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<PersistenceTestDbContext>()
            .UseSqlServer("Server=localhost;Database=model_only", sql => sql.UseCompatibilityLevel(150))
            .Options;
        using var context = new PersistenceTestDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
