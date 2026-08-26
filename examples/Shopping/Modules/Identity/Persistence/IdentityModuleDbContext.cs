using Identity.Entities;
using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Persistence;

/// <summary>
/// EF Core DbContext for the Identity module.
/// Extends <see cref="IdentityDbContext{TUser,TRole,TKey}"/> to provide ASP.NET Identity
/// tables with <see cref="DefaultId"/> (Ulid) as the key type, plus custom entities
/// like <see cref="RefreshToken"/>.
/// </summary>
public class IdentityModuleDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, DefaultId>, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "identity";

    /// <summary>
    /// Refresh tokens for JWT rotation.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    /// <summary>
    /// Clients associated with client profile users.
    /// </summary>
    public DbSet<Client> Clients { get; set; } = null!;

    public IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options)
        : base(options) { }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DefaultId>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToStringConverter>();
        configurationBuilder.Properties<DefaultId?>().HaveConversion<Cysharp.EntityFrameworkCore.UlidToNullableStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply "identity" schema if supported by the active database provider (bypassed on SQLite and InMemory)
        builder.ApplySchema(Schema, Database);

        // Apply Ulid value converters to all Identity key columns
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.ProfileName).HasMaxLength(64);
            entity.Property(e => e.BanReason).HasMaxLength(500);
            entity.Property(e => e.BannedBy).HasMaxLength(26);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.HasIndex(e => e.ProfileName);
            entity.HasIndex(e => e.IsBanned);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(e => e.Id).HasMaxLength(26);
        });

        // Identity join/claim tables with Ulid key converters
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<DefaultId>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(26);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<DefaultId>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(26);
            entity.Property(e => e.RoleId).HasMaxLength(26);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<DefaultId>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(26);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<DefaultId>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(26);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<DefaultId>>(entity =>
        {
            entity.Property(e => e.RoleId).HasMaxLength(26);
        });

        // RefreshToken configuration
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.UserId).HasMaxLength(26);
            entity.Property(e => e.Token).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ReplacedByToken).HasMaxLength(256);

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Client configuration
        builder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(26);
            entity.Property(e => e.UserId).HasMaxLength(26);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(26);
            entity.Property(e => e.UpdatedBy).HasMaxLength(26);

            entity.HasIndex(e => e.UserId).IsUnique();

            entity.HasOne(e => e.User)
                .WithOne(u => u.Client)
                .HasForeignKey<Client>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
