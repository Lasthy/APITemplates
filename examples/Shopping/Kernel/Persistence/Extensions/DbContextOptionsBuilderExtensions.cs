using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Persistence.Extensions;

/// <summary>
/// Extension methods for configuring <see cref="DbContextOptionsBuilder"/> with database providers and interceptors.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds all registered <see cref="ISaveChangesInterceptor"/> instances from the service provider
    /// to the DbContext options.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="serviceProvider">The service provider used to resolve registered interceptors.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder AddKernelInterceptors(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        var interceptors = serviceProvider.GetServices<ISaveChangesInterceptor>();
        return builder.AddInterceptors(interceptors);
    }

    /// <summary>
    /// Configures the DbContext to use the specified database provider with optional schema-aware migrations history isolation.
    /// Supported providers: Sqlite, SqlServer, PostgreSQL (or Postgres / Npgsql), MySql (or MariaDb), InMemory.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="provider">The provider name (e.g., Sqlite, SqlServer, PostgreSQL, MySql, InMemory).</param>
    /// <param name="connectionString">The connection string or database name.</param>
    /// <param name="schema">Optional schema name to isolate migrations history across modules.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder UseConfiguredDatabase(
        this DbContextOptionsBuilder builder,
        string provider,
        string connectionString,
        string? schema = null)
    {
        var normalizedProvider = provider?.Trim().ToLowerInvariant() ?? "sqlite";

        return normalizedProvider switch
        {
            "sqlite" => builder.UseSqlite(connectionString, x =>
            {
                if (!string.IsNullOrWhiteSpace(schema))
                {
                    x.MigrationsHistoryTable($"__EFMigrationsHistory_{schema.ToLowerInvariant()}");
                }
            }),

            "sqlserver" => builder.UseSqlServer(connectionString, x =>
            {
                if (!string.IsNullOrWhiteSpace(schema))
                {
                    x.MigrationsHistoryTable("__EFMigrationsHistory", schema.ToLowerInvariant());
                }
            }),

            "postgresql" or "postgres" or "npgsql" => builder.UseNpgsql(connectionString, x =>
            {
                if (!string.IsNullOrWhiteSpace(schema))
                {
                    x.MigrationsHistoryTable("__EFMigrationsHistory", schema.ToLowerInvariant());
                }
            }),

            "mysql" or "mariadb" => builder.UseMySql(
                connectionString,
                GetMySqlServerVersion(connectionString),
                x =>
                {
                    if (!string.IsNullOrWhiteSpace(schema))
                    {
                        x.MigrationsHistoryTable($"__EFMigrationsHistory_{schema.ToLowerInvariant()}");
                    }
                }),

            "inmemory" => builder.UseInMemoryDatabase(connectionString),

            _ => throw new InvalidOperationException($"Unsupported database provider: '{provider}'. Supported providers are: SqlServer, PostgreSQL, MySql, Sqlite, InMemory.")
        };
    }

    private static ServerVersion GetMySqlServerVersion(string connectionString)
    {
        try
        {
            return ServerVersion.AutoDetect(connectionString);
        }
        catch
        {
            // Fallback for offline or design-time scenarios where connection cannot be opened
            return new MySqlServerVersion(new Version(8, 0, 36));
        }
    }
}
