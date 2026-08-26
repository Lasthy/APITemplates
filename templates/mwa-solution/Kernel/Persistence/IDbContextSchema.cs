namespace Kernel.Persistence;

/// <summary>
/// Defines a custom database schema for an Entity Framework Core <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
/// </summary>
public interface IDbContextSchema
{
    /// <summary>
    /// The database schema name (e.g. "kernel", "identity", "catalog").
    /// Automatically bypassed on providers that do not support schemas (such as SQLite and InMemory).
    /// </summary>
    string Schema { get; }
}
