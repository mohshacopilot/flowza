using Flowza.Common.Data;
using Flowza.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Testing.Persistence;

/// <summary>An entity with all three markers, used to test the persistence conventions.</summary>
public sealed class TestDocument : IAuditable, ISoftDeletable, IVersioned
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }

    public DateTimeOffset? ReceivedAt { get; set; }

    public int Sequence { get; set; }

    /// <summary>An owned value stored in the document's own row.</summary>
    public TestSigner? Signer { get; set; }

    /// <summary>Child rows without soft delete, deleted by cascade in a plain delete.</summary>
    public List<TestAttachment> Attachments { get; } = [];
}

public sealed class TestSigner
{
    public string Name { get; set; } = string.Empty;
}

public sealed class TestAttachment
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public string FileName { get; set; } = string.Empty;
}

/// <summary>An entity without markers: no audit columns, hard delete, no row version.</summary>
public sealed class TestNote
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;
}

/// <summary>Test-only DbContext in its own schema, so it never touches a service's tables.</summary>
public sealed class PersistenceTestDbContext(DbContextOptions<PersistenceTestDbContext> options) : FlowzaDbContext(options)
{
    public const string Schema = "persistence_test";

    public DbSet<TestDocument> Documents => Set<TestDocument>();

    public DbSet<TestAttachment> Attachments => Set<TestAttachment>();

    public DbSet<TestNote> Notes => Set<TestNote>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<TestDocument>(document =>
        {
            document.Property(d => d.Title).HasMaxLength(200);
            document.OwnsOne(d => d.Signer, signer => signer.Property(s => s.Name).HasMaxLength(100));
            document.HasMany(d => d.Attachments).WithOne().HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TestAttachment>(attachment => attachment.Property(a => a.FileName).HasMaxLength(200));
        modelBuilder.Entity<TestNote>(note => note.Property(n => n.Text).HasMaxLength(200));
    }
}
