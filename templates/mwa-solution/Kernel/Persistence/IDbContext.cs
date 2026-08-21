using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kernel.Persistence;

/// <summary>
/// Common interface for database contexts.
/// Exposes standard Entity Framework Core operations.
/// </summary>
public interface IDbContext : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Provides access to database-related information and operations.
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Provides access to change tracking information and operations for entities.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// Creates a <see cref="DbSet{TEntity}"/> that can be used to query and save instances of <typeparamref name="TEntity"/>.
    /// </summary>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    /// <summary>
    /// Gets an <see cref="EntityEntry{TEntity}"/> for the given entity.
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    int SaveChanges();

    /// <summary>
    /// Saves all changes made in this context to the database asynchronously.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
