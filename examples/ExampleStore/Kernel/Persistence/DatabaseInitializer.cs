using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Kernel.Persistence;

/// <summary>
/// Utility for safely creating databases and tables across multiple EF Core DbContexts and providers.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Initializes the database and tables for the given <see cref="DbContext"/>.
    /// </summary>
    public static void InitializeContext(DbContext dbContext, Type contextType, ILogger? logger)
    {
        try
        {
            var creator = dbContext.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;

            if (creator != null)
            {
                if (!creator.Exists())
                {
                    creator.Create();
                    logger?.LogInformation("Created database for {ContextType}", contextType.Name);
                }

                if (!ContextTablesExist(dbContext))
                {
                    try
                    {
                        creator.CreateTables();
                        logger?.LogInformation("Created tables for {ContextType}", contextType.Name);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Could not create tables for {ContextType}", contextType.Name);
                    }
                }
            }
            else
            {
                dbContext.Database.EnsureCreated();
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to initialize database tables for {ContextType}", contextType.Name);
        }
    }

    private static bool ContextTablesExist(DbContext context)
    {
        var entityTypes = context.Model.GetEntityTypes().ToList();
        if (!entityTypes.Any()) return true;

        var firstEntity = entityTypes.FirstOrDefault(e => !string.IsNullOrEmpty(e.GetTableName()));
        if (firstEntity == null) return true;

        var tableName = firstEntity.GetTableName()!;
        var schema = firstEntity.GetSchema();

        return TableExists(context, tableName, schema);
    }

    private static bool TableExists(DbContext context, string tableName, string? schema = null)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
            openedHere = true;
        }

        try
        {
            using var command = connection.CreateCommand();
            var provider = context.Database.ProviderName ?? string.Empty;

            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name;";
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = tableName;
                command.Parameters.Add(p);
                var result = Convert.ToInt64(command.ExecuteScalar());
                return result > 0;
            }
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) || provider.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE LOWER(table_name) = LOWER(@name)" +
                    (string.IsNullOrEmpty(schema) ? ";" : " AND LOWER(table_schema) = LOWER(@schema);");
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = tableName;
                command.Parameters.Add(p);
                if (!string.IsNullOrEmpty(schema))
                {
                    var ps = command.CreateParameter();
                    ps.ParameterName = "@schema";
                    ps.Value = schema;
                    command.Parameters.Add(ps);
                }
                var result = Convert.ToInt64(command.ExecuteScalar());
                return result > 0;
            }
            else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @name" +
                    (string.IsNullOrEmpty(schema) ? ";" : " AND TABLE_SCHEMA = @schema;");
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = tableName;
                command.Parameters.Add(p);
                if (!string.IsNullOrEmpty(schema))
                {
                    var ps = command.CreateParameter();
                    ps.ParameterName = "@schema";
                    ps.Value = schema;
                    command.Parameters.Add(ps);
                }
                var result = Convert.ToInt64(command.ExecuteScalar());
                return result > 0;
            }
            else if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase) || provider.Contains("MariaDb", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @name;";
                var p = command.CreateParameter();
                p.ParameterName = "@name";
                p.Value = tableName;
                command.Parameters.Add(p);
                var result = Convert.ToInt64(command.ExecuteScalar());
                return result > 0;
            }
            else
            {
                var creator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                return creator?.HasTables() ?? true;
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            if (openedHere)
            {
                connection.Close();
            }
        }
    }
}
