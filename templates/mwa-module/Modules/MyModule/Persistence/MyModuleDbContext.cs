using Kernel.Persistence;
using Kernel.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MyModule.Persistence;

/// <summary>
/// Entity Framework Core database context for the <c>MyModule</c> module.
/// </summary>
public class MyModuleDbContext : DbContext, IDbContextSchema
{
    /// <inheritdoc/>
    public string Schema => "mymodule";

    public MyModuleDbContext(DbContextOptions<MyModuleDbContext> options) : base(options)
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

        // Apply module schema if supported by the active database provider (bypassed on SQLite and InMemory)
        builder.ApplySchema(Schema, Database);

        // TODO: Configure entity mappings, indices, relationships, and soft-delete filters here.
        // Example:
        // builder.Entity<MyEntity>(entity =>
        // {
        //     entity.HasKey(e => e.Id);
        //     entity.Property(e => e.Id).HasMaxLength(26);
        // });
    }
}
