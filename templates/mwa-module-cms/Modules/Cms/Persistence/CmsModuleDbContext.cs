using Cms.Entities;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Cms.Persistence;

/// <summary>
/// EF Core DbContext for the CMS module.
/// Uses the "cms" schema for content items, revisions, tags, and media.
/// </summary>
public class CmsModuleDbContext : DbContext, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "cms";

    /// <summary>
    /// Content items (pages, posts, FAQs, etc.).
    /// </summary>
    public DbSet<ContentItem> ContentItems { get; set; } = null!;

    /// <summary>
    /// Point-in-time revisions of content items.
    /// </summary>
    public DbSet<ContentRevision> Revisions { get; set; } = null!;

    /// <summary>
    /// Tags for categorizing content items.
    /// </summary>
    public DbSet<ContentTag> Tags { get; set; } = null!;

    /// <summary>
    /// Join entity between content items and tags.
    /// </summary>
    public DbSet<ContentItemTag> ContentItemTags { get; set; } = null!;

    /// <summary>
    /// Media attachments and library files.
    /// </summary>
    public DbSet<ContentMedia> Media { get; set; } = null!;

    public CmsModuleDbContext(DbContextOptions<CmsModuleDbContext> options)
        : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DefaultId>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToStringConverter>();
        configurationBuilder.Properties<DefaultId?>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToNullableStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply "cms" schema if supported by the active database provider
        builder.ApplySchema(Schema, Database);

        builder.Entity<ContentItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AuthorId).HasMaxLength(26);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);
            entity.Property(e => e.DeletedBy).HasMaxLength(26);

            entity.HasIndex(e => new { e.ContentType, e.Slug });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AuthorId);
            entity.HasIndex(e => e.PublishAt);
            entity.HasIndex(e => e.PublishedAt);
            entity.HasIndex(e => new { e.ContentType, e.SortOrder });

            entity.HasQueryFilter(e => e.DeletedAt == null);

            entity.HasMany(e => e.Revisions)
                .WithOne(r => r.ContentItem)
                .HasForeignKey(r => r.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Tags)
                .WithOne(t => t.ContentItem)
                .HasForeignKey(t => t.ContentItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Media)
                .WithOne(m => m.ContentItem)
                .HasForeignKey(m => m.ContentItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ContentRevision>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.ContentItemId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.AuthorId).HasMaxLength(26);
            entity.Property(e => e.ChangeNote).HasMaxLength(1000);

            entity.HasIndex(e => new { e.ContentItemId, e.RevisionNumber }).IsUnique();
        });

        builder.Entity<ContentTag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.HasIndex(e => e.Slug).IsUnique();

            entity.HasMany(e => e.ContentItems)
                .WithOne(cit => cit.ContentTag)
                .HasForeignKey(cit => cit.ContentTagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ContentItemTag>(entity =>
        {
            entity.HasKey(e => new { e.ContentItemId, e.ContentTagId });
            entity.Property(e => e.ContentItemId).HasMaxLength(26);
            entity.Property(e => e.ContentTagId).HasMaxLength(26);
        });

        builder.Entity<ContentMedia>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.ContentItemId).HasMaxLength(26);
            entity.Property(e => e.FileName).HasMaxLength(500).IsRequired();
            entity.Property(e => e.StoragePath).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.MimeType).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AltText).HasMaxLength(1000);
            entity.Property(e => e.PublicUrl).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.HasIndex(e => e.ContentItemId);
            entity.HasIndex(e => e.StoragePath).IsUnique();
        });
    }
}
