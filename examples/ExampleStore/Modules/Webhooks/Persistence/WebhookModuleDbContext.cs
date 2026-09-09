using Kernel.Encryption.Extensions;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Webhooks.Entities;

namespace Webhooks.Persistence;

/// <summary>
/// EF Core DbContext for the Webhooks module.
/// Uses the "webhooks" schema for subscriptions, delivery logs, and queued events.
/// </summary>
public class WebhookModuleDbContext : DbContext, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "webhooks";

    public DbSet<WebhookSubscription> Subscriptions { get; set; } = null!;
    public DbSet<WebhookDelivery> Deliveries { get; set; } = null!;
    public DbSet<WebhookEvent> Events { get; set; } = null!;

    public WebhookModuleDbContext(DbContextOptions<WebhookModuleDbContext> options)
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

        // Apply "webhooks" schema if supported by the active database provider
        builder.ApplySchema(Schema, Database);

        // Configure transparent property encryption for [Encrypted] properties
        builder.UseEncryption(this);

        builder.Entity<WebhookSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Url).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Secret).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EventTypes).HasMaxLength(2000);
            entity.Property(e => e.OwnerUserId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);
            entity.Property(e => e.DeletedBy).HasMaxLength(26);

            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            // Soft-delete query filter
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        builder.Entity<WebhookDelivery>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.SubscriptionId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.WebhookEventId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ResponseBody).HasMaxLength(8192);

            entity.HasIndex(e => e.SubscriptionId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRetryAt);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Deliveries)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WebhookEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.EventType).HasMaxLength(200).IsRequired();

            entity.HasIndex(e => e.IsProcessed);
            entity.HasIndex(e => e.OccurredAt);
        });
    }
}
