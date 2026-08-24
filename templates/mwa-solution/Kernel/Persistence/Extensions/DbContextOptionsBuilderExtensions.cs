using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Kernel.Persistence.Extensions;

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
    /// Configures the DbContext to use the specified database provider using reflection.
    /// The consuming project must have the appropriate Entity Framework Core provider NuGet package installed.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="provider">The provider name (e.g., Sqlite, SqlServer, PostgreSQL, MySql).</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The options builder so that further configuration can be chained.</returns>
    public static DbContextOptionsBuilder UseConfiguredDatabase(
        this DbContextOptionsBuilder builder,
        string provider,
        string connectionString)
    {
        return provider.ToLowerInvariant() switch
        {
            "sqlite" => InvokeExtensionMethod(
                builder,
                "Microsoft.EntityFrameworkCore.Sqlite",
                "Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions",
                "UseSqlite",
                connectionString),
            "sqlserver" => InvokeExtensionMethod(
                builder,
                "Microsoft.EntityFrameworkCore.SqlServer",
                "Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions",
                "UseSqlServer",
                connectionString),
            "postgresql" or "postgres" => InvokeExtensionMethod(
                builder,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                "Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsExtensions",
                "UseNpgsql",
                connectionString),
            "mysql" or "mariadb" => InvokeExtensionMethod(
                builder,
                "Pomelo.EntityFrameworkCore.MySql",
                "Microsoft.EntityFrameworkCore.MySqlDbContextOptionsExtensions",
                "UseMySql",
                connectionString,
                // Pomelo MySql requires a ServerVersion parameter. We use AutoDetect.
                requiresServerVersion: true),
            "inmemory" => InvokeExtensionMethod(
                builder,
                "Microsoft.EntityFrameworkCore.InMemory",
                "Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions",
                "UseInMemoryDatabase",
                connectionString),
            _ => throw new InvalidOperationException($"Unsupported database provider: {provider}")
        };
    }

    private static DbContextOptionsBuilder InvokeExtensionMethod(
        DbContextOptionsBuilder builder,
        string assemblyName,
        string typeName,
        string methodName,
        string connectionString,
        bool requiresServerVersion = false)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.Load(assemblyName);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException($"Could not load assembly '{assemblyName}'. Ensure the corresponding Entity Framework Core provider NuGet package is installed.");
        }

        var type = assembly.GetType(typeName);
        if (type == null)
        {
            throw new InvalidOperationException($"Could not find type '{typeName}' in assembly '{assemblyName}'.");
        }

        if (requiresServerVersion && methodName == "UseMySql")
        {
            // Pomelo.EntityFrameworkCore.MySql: UseMySql(DbContextOptionsBuilder, string, ServerVersion, Action<MySqlDbContextOptionsBuilder>)
            var serverVersionType = assembly.GetType("Microsoft.EntityFrameworkCore.ServerVersion");
            if (serverVersionType == null) throw new InvalidOperationException("Could not find ServerVersion type for Pomelo MySql.");

            var autoDetectMethod = serverVersionType.GetMethod("AutoDetect", new[] { typeof(string) });
            if (autoDetectMethod == null) throw new InvalidOperationException("Could not find ServerVersion.AutoDetect method.");

            var serverVersion = autoDetectMethod.Invoke(null, new object[] { connectionString });

            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName &&
                                     m.GetParameters().Length >= 3 &&
                                     m.GetParameters()[0].ParameterType == typeof(DbContextOptionsBuilder) &&
                                     m.GetParameters()[1].ParameterType == typeof(string) &&
                                     m.GetParameters()[2].ParameterType == serverVersionType);

            if (method == null) throw new InvalidOperationException($"Could not find suitable {methodName} method for MySql.");

            var parameters = new object[method.GetParameters().Length];
            parameters[0] = builder;
            parameters[1] = connectionString;
            parameters[2] = serverVersion!;
            // Leave remaining parameters (like action options) as default/null
            
            return (DbContextOptionsBuilder)method.Invoke(null, parameters)!;
        }
        else
        {
            // Standard providers: Use*(DbContextOptionsBuilder, string, Action<*DbContextOptionsBuilder>)
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName &&
                                     m.GetParameters().Length >= 2 &&
                                     m.GetParameters()[0].ParameterType == typeof(DbContextOptionsBuilder) &&
                                     m.GetParameters()[1].ParameterType == typeof(string));

            if (method == null)
            {
                throw new InvalidOperationException($"Could not find suitable '{methodName}' method in '{typeName}'.");
            }

            var parameters = new object[method.GetParameters().Length];
            parameters[0] = builder;
            parameters[1] = connectionString;
            // Leave remaining parameters (like action options) as default/null

            return (DbContextOptionsBuilder)method.Invoke(null, parameters)!;
        }
    }
}
