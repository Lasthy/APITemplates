using Cms.Configuration;
using Cms.ContentTypes;
using Cms.Contracts.Events;
using Cms.Entities;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Events;
using Kernel.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cms.Services;

/// <summary>
/// Media management service wrapping Kernel's IFileStorage.
/// Handles media attachment validation, uploading, retrieval, and cleanup.
/// </summary>
public class ContentMediaService : IContentMediaService
{
    private readonly CmsModuleDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly IContentTypeRegistry _typeRegistry;
    private readonly IEventBus _eventBus;
    private readonly CmsSettings _settings;
    private readonly ILogger<ContentMediaService> _logger;

    public ContentMediaService(
        CmsModuleDbContext dbContext,
        IFileStorage fileStorage,
        IContentTypeRegistry typeRegistry,
        IEventBus eventBus,
        CmsSettings settings,
        ILogger<ContentMediaService> logger)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _typeRegistry = typeRegistry;
        _eventBus = eventBus;
        _settings = settings;
        _logger = logger;
    }

    public async Task<Result<ContentMedia>> UploadAsync(
        DefaultId? contentItemId,
        Stream fileStream,
        string fileName,
        string mimeType,
        string? altText = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        var maxSizeBytes = (long)_settings.MaxMediaFileSizeMb * 1024 * 1024;
        if (fileStream.Length > maxSizeBytes)
        {
            return Fail<ContentMedia>(new ArgumentException(
                $"File size ({fileStream.Length} bytes) exceeds maximum limit of {_settings.MaxMediaFileSizeMb} MB."));
        }

        if (!IsMimeTypeAllowed(mimeType))
        {
            return Fail<ContentMedia>(new ArgumentException($"MIME type '{mimeType}' is not allowed."));
        }

        if (contentItemId.HasValue && contentItemId.Value != default)
        {
            var contentItem = await _dbContext.ContentItems
                .FirstOrDefaultAsync(ci => ci.Id == contentItemId.Value, ct);

            if (contentItem == null)
            {
                return Fail<ContentMedia>(new KeyNotFoundException($"Content item '{contentItemId.Value}' not found."));
            }

            if (_typeRegistry.TryGet(contentItem.ContentType, out var descriptor) && descriptor != null && !descriptor.SupportsMedia)
            {
                return Fail<ContentMedia>(new InvalidOperationException(
                    $"Content type '{contentItem.ContentType}' does not support media attachments."));
            }

            var currentCount = await _dbContext.Media
                .CountAsync(m => m.ContentItemId == contentItemId.Value, ct);

            if (currentCount >= _settings.MaxMediaPerContent)
            {
                return Fail<ContentMedia>(new InvalidOperationException(
                    $"Maximum media attachments per content item ({_settings.MaxMediaPerContent}) exceeded."));
            }
        }

        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", "_");
        var storagePath = $"{_settings.MediaStoragePath.TrimEnd('/')}/{DefaultId.NewUlid()}_{baseName}{extension}";

        var uploadResult = await _fileStorage.UploadAsync(storagePath, fileStream, mimeType, ct);
        if (uploadResult.IsFailure)
        {
            return Fail<ContentMedia>(uploadResult.Error!);
        }

        var publicUrlResult = await _fileStorage.GetUrlAsync(storagePath, ct: ct);
        var publicUrl = publicUrlResult.IsSuccess ? publicUrlResult.Value : null;

        var media = new ContentMedia
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = contentItemId.HasValue && contentItemId.Value != default ? contentItemId.Value : null,
            FileName = Path.GetFileName(fileName),
            StoragePath = uploadResult.Value!,
            MimeType = mimeType,
            SizeBytes = fileStream.Length,
            AltText = altText,
            PublicUrl = publicUrl,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Media.Add(media);
        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new MediaUploadedEvent(
            media.Id,
            media.ContentItemId,
            media.FileName,
            media.MimeType,
            media.SizeBytes), ct);

        _logger.LogInformation("Uploaded media {MediaId} ({FileName}) to {StoragePath}",
            media.Id, media.FileName, media.StoragePath);

        return Result.Success(media);
    }

    public async Task<Result<IEnumerable<ContentMedia>>> GetByContentItemAsync(DefaultId contentItemId, CancellationToken ct = default)
    {
        var mediaList = await _dbContext.Media
            .AsNoTracking()
            .Where(m => m.ContentItemId == contentItemId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return Result.Success<IEnumerable<ContentMedia>>(mediaList);
    }

    public async Task<Result> DeleteAsync(DefaultId mediaId, CancellationToken ct = default)
    {
        var media = await _dbContext.Media.FirstOrDefaultAsync(m => m.Id == mediaId, ct);
        if (media == null)
        {
            return Fail(new KeyNotFoundException($"Media '{mediaId}' not found."));
        }

        await _fileStorage.DeleteAsync(media.StoragePath, ct);

        _dbContext.Media.Remove(media);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted media {MediaId} and removed storage path {StoragePath}",
            mediaId, media.StoragePath);

        return Result.Success();
    }

    public async Task<Result<Stream>> DownloadAsync(DefaultId mediaId, CancellationToken ct = default)
    {
        var media = await _dbContext.Media
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId, ct);

        if (media == null)
        {
            return Fail<Stream>(new KeyNotFoundException($"Media '{mediaId}' not found."));
        }

        return await _fileStorage.DownloadAsync(media.StoragePath, ct);
    }

    private static Result Fail(Exception ex) => Result.Failure(ex, ex.Message);
    private static Result<T> Fail<T>(Exception ex) => Result.Failure<T>(ex, ex.Message);

    private bool IsMimeTypeAllowed(string mimeType)
    {
        var allowedTypes = _settings.AllowedMediaMimeTypes;
        if (allowedTypes == null || allowedTypes.Count == 0)
        {
            return true;
        }

        foreach (var allowed in allowedTypes)
        {
            if (string.Equals(allowed, "*/*", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (allowed.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = allowed[..^2];
                if (mimeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(allowed, mimeType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
