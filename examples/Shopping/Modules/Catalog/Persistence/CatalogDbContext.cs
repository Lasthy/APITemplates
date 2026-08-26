using Catalog.Entities;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Persistence;

/// <summary>
/// EF Core DbContext for the Catalog module.
/// </summary>
public class CatalogDbContext : DbContext, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "catalog";

    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<RestockRequest> RestockRequests { get; set; } = null!;

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
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

        // Apply "catalog" schema if supported by the active database provider (bypassed on SQLite and InMemory)
        builder.ApplySchema(Schema, Database);

        builder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);
            entity.Property(e => e.DeletedBy).HasMaxLength(26);

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Sku).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Price).HasPrecision(18, 2);

            entity.HasIndex(e => e.Sku).IsUnique();

            // Global soft-delete query filter
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        builder.Entity<RestockRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.ProductId).HasMaxLength(26);
            entity.Property(e => e.RequestedBy).HasMaxLength(26);
            entity.Property(e => e.FulfilledBy).HasMaxLength(26);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.Status);
        });
    }
}
