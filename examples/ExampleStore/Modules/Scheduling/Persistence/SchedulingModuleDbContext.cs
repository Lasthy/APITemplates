using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Scheduling.Entities;

namespace Scheduling.Persistence;

/// <summary>
/// EF Core DbContext for the Scheduling module.
/// Uses the "scheduling" schema for scheduled jobs, execution logs, and distributed locks.
/// </summary>
public class SchedulingModuleDbContext : DbContext, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "scheduling";

    /// <summary>
    /// Scheduled jobs.
    /// </summary>
    public DbSet<ScheduledJob> Jobs { get; set; } = null!;

    /// <summary>
    /// Historical execution logs.
    /// </summary>
    public DbSet<JobExecution> Executions { get; set; } = null!;

    /// <summary>
    /// Distributed lock table for multi-instance scheduler coordination.
    /// </summary>
    public DbSet<JobLock> Locks { get; set; } = null!;

    public SchedulingModuleDbContext(DbContextOptions<SchedulingModuleDbContext> options)
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

        // Apply "scheduling" schema if supported by the active database provider
        builder.ApplySchema(Schema, Database);

        builder.Entity<ScheduledJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.HandlerType).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CronExpression).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LastRunStatus).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);
            entity.Property(e => e.DeletedBy).HasMaxLength(26);

            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRunAt);
            entity.HasIndex(e => e.HandlerType);
            entity.HasIndex(e => e.Priority);
        });

        builder.Entity<JobExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.JobId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HandlerType).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ResultJson).HasMaxLength(8192);
            entity.Property(e => e.ExecutedByInstance).HasMaxLength(200);

            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);

            entity.HasOne(e => e.Job)
                .WithMany(j => j.Executions)
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobLock>(entity =>
        {
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId).HasMaxLength(26);
            entity.Property(e => e.LockedByInstance).HasMaxLength(200).IsRequired();

            entity.HasIndex(e => e.ExpiresAt);
        });
    }
}
