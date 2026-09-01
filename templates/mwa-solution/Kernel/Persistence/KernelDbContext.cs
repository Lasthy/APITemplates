using Kernel.AuditLog;
using Kernel.Boxing;
using Kernel.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kernel.Persistence;

/// <summary>
/// Entity Framework Core database context for Kernel infrastructure entities (e.g. outbox/inbox messages, audit logs).
/// </summary>
public class KernelDbContext : DbContext, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "kernel";

    /// <summary>
    /// Outbox and inbox persistent messages.
    /// </summary>
    public DbSet<Message> Messages { get; set; } = null!;

    /// <summary>
    /// Structured audit log entries.
    /// </summary>
    public DbSet<AuditEntry> AuditEntries { get; set; } = null!;

    public KernelDbContext(DbContextOptions<KernelDbContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DefaultId>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToStringConverter>();
        configurationBuilder.Properties<DefaultId?>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToNullableStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply "kernel" schema if supported by the active database provider (bypassed on SQLite and InMemory)
        modelBuilder.ApplySchema(Schema, Database);

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.Type).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Channel).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);

            entity.HasIndex(e => e.Partition);
            entity.HasIndex(e => e.ProcessedAt);
            entity.HasIndex(e => e.ProcessingAt);
            entity.HasIndex(e => e.NextRetryAt);
            entity.HasIndex(e => e.CorrelationId);
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.UserId).HasMaxLength(26);
            entity.Property(e => e.EntityType).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Action).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);

            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.EntityId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OccurredAt);
            entity.HasIndex(e => e.CorrelationId);
        });
    }
}
