using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Entities;

namespace MultiTenancy.Persistence;

/// <summary>
/// EF Core DbContext for the MultiTenancy module.
/// Uses the "tenancy" schema for tenant, member, and invitation tables.
/// </summary>
public class TenantModuleDbContext : DbContext, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "tenancy";

    /// <summary>
    /// Tenants managed by the application.
    /// </summary>
    public DbSet<Tenant> Tenants { get; set; } = null!;

    /// <summary>
    /// Memberships of users within tenants.
    /// </summary>
    public DbSet<TenantMember> Members { get; set; } = null!;

    /// <summary>
    /// Pending, accepted, or revoked invitations.
    /// </summary>
    public DbSet<TenantInvitation> Invitations { get; set; } = null!;

    public TenantModuleDbContext(DbContextOptions<TenantModuleDbContext> options)
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

        // Apply "tenancy" schema if supported by the active database provider (bypassed on SQLite and InMemory)
        builder.ApplySchema(Schema, Database);

        builder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(e => e.Plan).HasMaxLength(64);
            entity.Property(e => e.OwnerUserId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.SuspensionReason).HasMaxLength(500);
            entity.Property(e => e.SuspendedBy).HasMaxLength(26);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);
            entity.Property(e => e.DeletedBy).HasMaxLength(26);

            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => e.Status);
        });

        builder.Entity<TenantMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.TenantId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TenantInvitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.TenantId).HasMaxLength(26).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Token).HasMaxLength(256).IsRequired();
            entity.Property(e => e.AcceptedByUserId).HasMaxLength(26);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.HasIndex(e => new { e.TenantId, e.Email });
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
