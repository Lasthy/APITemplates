using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Orders.Entities;

namespace Orders.Persistence;

/// <summary>
/// EF Core DbContext for the Orders module.
/// </summary>
public class OrdersDbContext : DbContext, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "orders";

    public DbSet<CartItem> CartItems { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
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

        // Apply "orders" schema if supported by the active database provider (bypassed on SQLite and InMemory)
        builder.ApplySchema(Schema, Database);

        builder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.UserId).HasMaxLength(26);
            entity.Property(e => e.ProductId).HasMaxLength(26);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.ProductId }).IsUnique();
        });

        builder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.UserId).HasMaxLength(26);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);

            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasMany(e => e.Items)
                .WithOne(e => e.Order)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.OrderId).HasMaxLength(26);
            entity.Property(e => e.ProductId).HasMaxLength(26);

            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);

            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ProductId);
        });
    }
}
