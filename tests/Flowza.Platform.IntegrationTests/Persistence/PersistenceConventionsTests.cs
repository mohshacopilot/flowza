using Flowza.Persistence;
using Flowza.Testing;
using Flowza.Testing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Platform.IntegrationTests.Persistence;

/// <summary>The persistence conventions against a real SQL Server (constitution 5.2).</summary>
public sealed class PersistenceConventionsTests(PersistenceDatabaseFixture database)
    : IClassFixture<PersistenceDatabaseFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset s_start = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private readonly ManualTimeProvider _clock = new(s_start);
    private readonly FixedCurrentUser _user = new(Guid.Parse("0199f0a0-0000-7000-8000-000000000001"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await database.ClearAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task T3_D1_new_entity_gets_its_created_stamp_from_the_clock_and_the_user()
    {
        var id = await AddDocumentAsync("first");

        await using var context = database.CreateContext(_clock, _user);
        var entry = context.Entry(await context.Documents.SingleAsync(d => d.Id == id, Ct));

        entry.Property<DateTime>(ShadowColumns.CreatedAt).CurrentValue.ShouldBe(s_start.UtcDateTime);
        entry.Property<Guid?>(ShadowColumns.CreatedBy).CurrentValue.ShouldBe(_user.UserId);
        entry.Property<DateTime?>(ShadowColumns.UpdatedAt).CurrentValue.ShouldBeNull();
    }

    [Fact]
    public async Task T3_D1_update_sets_the_updated_stamp_and_keeps_the_created_stamp()
    {
        var id = await AddDocumentAsync("before");
        _clock.Now = s_start.AddHours(2);
        var editor = new FixedCurrentUser(Guid.Parse("0199f0a0-0000-7000-8000-000000000002"));

        await using (var context = database.CreateContext(_clock, editor))
        {
            var document = await context.Documents.SingleAsync(d => d.Id == id, Ct);
            document.Title = "after";
            context.Entry(document).Property<DateTime>(ShadowColumns.CreatedAt).CurrentValue = DateTime.UtcNow; // must be ignored
            await context.SaveChangesAsync(Ct);
        }

        await using var check = database.CreateContext(_clock, editor);
        var entry = check.Entry(await check.Documents.SingleAsync(d => d.Id == id, Ct));
        entry.Entity.Title.ShouldBe("after");
        entry.Property<DateTime>(ShadowColumns.CreatedAt).CurrentValue.ShouldBe(s_start.UtcDateTime);
        entry.Property<Guid?>(ShadowColumns.CreatedBy).CurrentValue.ShouldBe(_user.UserId);
        entry.Property<DateTime?>(ShadowColumns.UpdatedAt).CurrentValue.ShouldBe(s_start.AddHours(2).UtcDateTime);
        entry.Property<Guid?>(ShadowColumns.UpdatedBy).CurrentValue.ShouldBe(editor.UserId);
    }

    [Fact]
    public async Task Constitution_8_6_deleting_a_soft_deletable_entity_keeps_the_row_and_hides_it()
    {
        var id = await AddDocumentAsync("to delete");
        _clock.Now = s_start.AddDays(1);

        await using (var context = database.CreateContext(_clock, _user))
        {
            context.Documents.Remove(await context.Documents.SingleAsync(d => d.Id == id, Ct));
            await context.SaveChangesAsync(Ct);
        }

        await using var check = database.CreateContext(_clock, _user);
        (await check.Documents.AnyAsync(d => d.Id == id, Ct)).ShouldBeFalse();

        var deleted = await check.Documents
            .IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter])
            .SingleAsync(d => d.Id == id, Ct);
        var entry = check.Entry(deleted);
        entry.Property<bool>(ShadowColumns.IsDeleted).CurrentValue.ShouldBeTrue();
        entry.Property<DateTime?>(ShadowColumns.DeletedAt).CurrentValue.ShouldBe(s_start.AddDays(1).UtcDateTime);
        entry.Property<Guid?>(ShadowColumns.DeletedBy).CurrentValue.ShouldBe(_user.UserId);
        deleted.Title.ShouldBe("to delete");
    }

    [Fact]
    public async Task Constitution_8_6_soft_delete_keeps_loaded_children_and_owned_values()
    {
        var document = new TestDocument { Title = "with children", IssuedAt = s_start.UtcDateTime, Signer = new TestSigner { Name = "Signer A" } };
        document.Attachments.Add(new TestAttachment { FileName = "one.pdf" });
        document.Attachments.Add(new TestAttachment { FileName = "two.pdf" });
        await using (var context = database.CreateContext(_clock, _user))
        {
            context.Documents.Add(document);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = database.CreateContext(_clock, _user))
        {
            var loaded = await context.Documents.Include(d => d.Attachments).SingleAsync(d => d.Id == document.Id, Ct);
            context.Documents.Remove(loaded);
            await context.SaveChangesAsync(Ct);
        }

        await using var check = database.CreateContext(_clock, _user);
        (await check.Attachments.CountAsync(a => a.DocumentId == document.Id, Ct)).ShouldBe(2);
        var stored = await check.Documents.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter]).SingleAsync(d => d.Id == document.Id, Ct);
        stored.Signer.ShouldNotBeNull();
        stored.Signer.Name.ShouldBe("Signer A");
        check.Entry(stored).Property<bool>(ShadowColumns.IsDeleted).CurrentValue.ShouldBeTrue();
    }

    [Fact]
    public async Task Entity_without_markers_is_really_deleted()
    {
        var note = new TestNote { Text = "temporary" };
        await using (var context = database.CreateContext(_clock, _user))
        {
            context.Notes.Add(note);
            await context.SaveChangesAsync(Ct);
            context.Notes.Remove(note);
            await context.SaveChangesAsync(Ct);
        }

        await using var check = database.CreateContext(_clock, _user);
        (await check.Notes.IgnoreQueryFilters().AnyAsync(n => n.Id == note.Id, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Constitution_8_5_the_second_writer_gets_a_concurrency_error()
    {
        var id = await AddDocumentAsync("shared");

        await using var first = database.CreateContext(_clock, _user);
        await using var second = database.CreateContext(_clock, _user);
        var copyA = await first.Documents.SingleAsync(d => d.Id == id, Ct);
        var copyB = await second.Documents.SingleAsync(d => d.Id == id, Ct);

        copyA.Title = "A";
        await first.SaveChangesAsync(Ct);

        copyB.Title = "B";
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Constitution_8_5_an_old_row_version_from_the_client_is_rejected()
    {
        var id = await AddDocumentAsync("v1");
        byte[] versionSeenByClient;
        await using (var read = database.CreateContext(_clock, _user))
        {
            versionSeenByClient = read.GetRowVersion(await read.Documents.SingleAsync(d => d.Id == id, Ct))!;
        }

        await using (var other = database.CreateContext(_clock, _user))
        {
            (await other.Documents.SingleAsync(d => d.Id == id, Ct)).Title = "changed meanwhile";
            await other.SaveChangesAsync(Ct);
        }

        await using var context = database.CreateContext(_clock, _user);
        var document = await context.Documents.SingleAsync(d => d.Id == id, Ct);
        context.SetExpectedRowVersion(document, versionSeenByClient);
        document.Title = "client edit";
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task T3_D2_times_are_stored_in_utc_and_read_back_as_utc()
    {
        var india = new DateTimeOffset(2026, 10, 21, 15, 30, 0, TimeSpan.FromHours(5.5));
        var document = new TestDocument
        {
            Title = "times",
            IssuedAt = new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc),
            ReceivedAt = india,
        };
        await using (var context = database.CreateContext(_clock, _user))
        {
            context.Documents.Add(document);
            await context.SaveChangesAsync(Ct);
        }

        await using var check = database.CreateContext(_clock, _user);
        var stored = await check.Documents.SingleAsync(d => d.Id == document.Id, Ct);
        stored.IssuedAt.Kind.ShouldBe(DateTimeKind.Utc);
        stored.IssuedAt.ShouldBe(document.IssuedAt);
        stored.ReceivedAt.ShouldBe(india);
        stored.ReceivedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task T3_D3_generated_keys_come_back_in_insert_order_when_SQL_Server_sorts_them()
    {
        await using (var context = database.CreateContext(_clock, _user))
        {
            for (var i = 0; i < 200; i++)
            {
                context.Documents.Add(new TestDocument { Title = $"doc {i}", Sequence = i, IssuedAt = DateTime.UtcNow });
                await context.SaveChangesAsync(Ct);
            }
        }

        await using var check = database.CreateContext(_clock, _user);
        var sequencesOrderedById = await check.Documents.OrderBy(d => d.Id).Select(d => d.Sequence).ToListAsync(Ct);

        sequencesOrderedById.ShouldBe(Enumerable.Range(0, 200).ToList());
    }

    private async Task<Guid> AddDocumentAsync(string title)
    {
        await using var context = database.CreateContext(_clock, _user);
        var document = new TestDocument { Title = title, IssuedAt = s_start.UtcDateTime };
        context.Documents.Add(document);
        await context.SaveChangesAsync(Ct);
        return document.Id;
    }
}
