using Kernel.Common;
using Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kernel.Storage;

/// <summary>
/// Entity Framework Core implementation of <see cref="IFileRegistry"/> backed by <see cref="KernelDbContext"/>.
/// Uses <see cref="IServiceScopeFactory"/> to avoid captive dependencies when registered as a singleton.
/// </summary>
public class DbFileRegistry : IFileRegistry
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbFileRegistry>? _logger;

    public DbFileRegistry(
        IServiceScopeFactory scopeFactory,
        ILogger<DbFileRegistry>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<DefaultId>> RegisterAsync(
        string path,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Failure<DefaultId>(new ArgumentException("Storage path cannot be empty.", nameof(path)));
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KernelDbContext>();

            var now = DateTime.UtcNow;

            // If no specific expiry was requested, check for an existing active non-expired mapping
            if (!expiry.HasValue)
            {
                var existing = await db.FileRegistry
                    .Where(e => e.Path == path && (e.ExpiresAt == null || e.ExpiresAt > now))
                    .OrderByDescending(e => e.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (existing != null)
                {
                    return Result.Success(existing.Id);
                }
            }

            var entry = new FileRegistryEntry
            {
                Id = DefaultId.NewUlid(),
                Path = path,
                CreatedAt = now,
                ExpiresAt = expiry.HasValue ? now.Add(expiry.Value) : null
            };

            db.FileRegistry.Add(entry);
            await db.SaveChangesAsync(ct);

            _logger?.LogDebug("Registered file registry entry {Id} for path {Path} with expiry {ExpiresAt}",
                entry.Id, path, entry.ExpiresAt);

            return Result.Success(entry.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to register file mapping for path {Path}", path);
            return Result.Failure<DefaultId>(ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<FileRegistryEntry>> GetAsync(
        DefaultId id,
        CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KernelDbContext>();

            var entry = await db.FileRegistry
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, ct);

            if (entry == null)
            {
                return Result.Failure<FileRegistryEntry>(new KeyNotFoundException($"File registry entry '{id}' not found."));
            }

            if (entry.IsExpired)
            {
                return Result.Failure<FileRegistryEntry>(new InvalidOperationException($"File registry entry '{id}' has expired."));
            }

            return Result.Success(entry);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve file registry entry {Id}", id);
            return Result.Failure<FileRegistryEntry>(ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> RevokeAsync(
        DefaultId id,
        CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KernelDbContext>();

            var entry = await db.FileRegistry.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entry != null)
            {
                db.FileRegistry.Remove(entry);
                await db.SaveChangesAsync(ct);
                _logger?.LogInformation("Revoked file registry entry {Id}", id);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to revoke file registry entry {Id}", id);
            return Result.Failure(ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> RevokeByPathAsync(
        string path,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Success();
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KernelDbContext>();

            var entries = await db.FileRegistry
                .Where(e => e.Path == path)
                .ToListAsync(ct);

            if (entries.Count > 0)
            {
                db.FileRegistry.RemoveRange(entries);
                await db.SaveChangesAsync(ct);
                _logger?.LogInformation("Revoked {Count} file registry entries for path {Path}", entries.Count, path);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to revoke file registry entries for path {Path}", path);
            return Result.Failure(ex);
        }
    }
}
