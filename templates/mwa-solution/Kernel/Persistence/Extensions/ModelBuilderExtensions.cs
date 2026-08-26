using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kernel.Persistence.Extensions;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> supporting schema resolution.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the specified default database schema to all tables in the model,
    /// but only if the active database provider supports schemas.
    /// Providers that do not support schemas (such as SQLite and InMemory) are automatically bypassed.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="schema">The schema name (e.g., "catalog", "identity", "kernel").</param>
    /// <param name="database">The database facade from the DbContext (used to detect the provider).</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplySchema(
        this ModelBuilder modelBuilder,
        string? schema,
        DatabaseFacade? database = null)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return modelBuilder;
        }

        if (database != null && !SupportsSchemas(database.ProviderName))
        {
            return modelBuilder;
        }

        modelBuilder.HasDefaultSchema(schema.ToLowerInvariant());
        return modelBuilder;
    }

    /// <summary>
    /// Applies the schema defined on the <see cref="IDbContextSchema"/> context to the model builder,
    /// if the database provider supports schemas.
    /// </summary>
    public static ModelBuilder ApplySchema(
        this ModelBuilder modelBuilder,
        DbContext context)
    {
        if (context is IDbContextSchema schemaProvider && !string.IsNullOrWhiteSpace(schemaProvider.Schema))
        {
            return modelBuilder.ApplySchema(schemaProvider.Schema, context.Database);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Checks whether the specified database provider name supports relational schemas.
    /// </summary>
    public static bool SupportsSchemas(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return true;
        }

        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
