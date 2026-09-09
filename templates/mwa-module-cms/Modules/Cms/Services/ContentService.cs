using System.Text.Json;
using System.Text.RegularExpressions;
using Cms.Configuration;
using Cms.ContentTypes;
using Cms.Contracts.Constants;
using Cms.Contracts.Events;
using Cms.Entities;
using Cms.Persistence;
using Kernel.Common;
using Kernel.Context;
using Kernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cms.Services;

/// <summary>
/// Core implementation of IContentService handling content lifecycles, revisions, tags, and field validation.
/// </summary>
public class ContentService : IContentService
{
    private readonly CmsModuleDbContext _dbContext;
    private readonly IContentTypeRegistry _typeRegistry;
    private readonly ISlugService _slugService;
    private readonly IEventBus _eventBus;
    private readonly IScopeContext _scopeContext;
    private readonly CmsSettings _settings;
    private readonly ILogger<ContentService> _logger;

    public ContentService(
        CmsModuleDbContext dbContext,
        IContentTypeRegistry typeRegistry,
        ISlugService slugService,
        IEventBus eventBus,
        IScopeContext scopeContext,
        CmsSettings settings,
        ILogger<ContentService> logger)
    {
        _dbContext = dbContext;
        _typeRegistry = typeRegistry;
        _slugService = slugService;
        _eventBus = eventBus;
        _scopeContext = scopeContext;
        _settings = settings;
        _logger = logger;
    }

    public Result ValidateFields(string contentType, string? fieldsJson)
    {
        if (!_typeRegistry.TryGet(contentType, out var descriptor) || descriptor == null)
        {
            return Fail(new ArgumentException($"Content type '{contentType}' is not registered."));
        }

        if (string.IsNullOrWhiteSpace(fieldsJson))
        {
            var requiredField = descriptor.Fields.FirstOrDefault(f => f.IsRequired);
            if (requiredField != null)
            {
                return Fail(new ArgumentException($"Field '{requiredField.Name}' is required for content type '{contentType}'."));
            }
            return Result.Success();
        }

        if (fieldsJson.Length > _settings.MaxFieldsJsonLength)
        {
            return Fail(new ArgumentException($"Fields JSON exceeds maximum allowed length of {_settings.MaxFieldsJsonLength} characters."));
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(fieldsJson);
        }
        catch (JsonException ex)
        {
            return Fail(new ArgumentException($"Invalid JSON in fields: {ex.Message}", ex));
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Fail(new ArgumentException("Fields JSON must be a valid JSON object."));
            }

            var root = doc.RootElement;

            foreach (var field in descriptor.Fields)
            {
                var hasProperty = root.TryGetProperty(field.Name, out var prop);

                if (!hasProperty || prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
                {
                    if (field.IsRequired)
                    {
                        return Fail(new ArgumentException($"Field '{field.Name}' is required."));
                    }
                    continue;
                }

                var validationResult = ValidateFieldValue(field, prop);
                if (validationResult.IsFailure)
                {
                    return validationResult;
                }
            }
        }

        return Result.Success();
    }

    private static Result ValidateFieldValue(ContentField field, JsonElement prop)
    {
        switch (field.FieldType)
        {
            case ContentFieldType.String:
            case ContentFieldType.Text:
            case ContentFieldType.Html:
            case ContentFieldType.Markdown:
            case ContentFieldType.Url:
            case ContentFieldType.Image:
                if (prop.ValueKind != JsonValueKind.String)
                {
                    return Fail(new ArgumentException($"Field '{field.Name}' must be a string."));
                }
                var strVal = prop.GetString() ?? string.Empty;
                if (field.MaxLength.HasValue && strVal.Length > field.MaxLength.Value)
                {
                    return Fail(new ArgumentException($"Field '{field.Name}' exceeds maximum length of {field.MaxLength.Value}."));
                }
                if (!string.IsNullOrWhiteSpace(field.ValidationRegex))
                {
                    if (!Regex.IsMatch(strVal, field.ValidationRegex))
                    {
                        return Fail(new ArgumentException($"Field '{field.Name}' does not match required format."));
                    }
                }
                break;

            case ContentFieldType.Integer:
                if (prop.ValueKind != JsonValueKind.Number || !prop.TryGetInt64(out _))
                {
                    return Fail(new ArgumentException($"Field '{field.Name}' must be a valid integer."));
                }
                break;

            case ContentFieldType.Decimal:
                if (prop.ValueKind != JsonValueKind.Number || !prop.TryGetDecimal(out _))
                {
                    return Fail(new ArgumentException($"Field '{field.Name}' must be a valid decimal number."));
                }
                break;

            case ContentFieldType.Boolean:
                if (prop.ValueKind != JsonValueKind.True && prop.ValueKind != JsonValueKind.False)
                {
                    return Fail(new ArgumentException($"Field '{field.Name}' must be a boolean."));
                }
                break;

            case ContentFieldType.DateTime:
                if (prop.ValueKind != JsonValueKind.String || !DateTime.TryParse(prop.GetString(), out _))
                {
                    return Fail(new ArgumentException($"Field '{field.Name}' must be a valid ISO-8601 date string."));
                }
                break;

            case ContentFieldType.Json:
                // Any valid JSON element is acceptable
                break;
        }

        return Result.Success();
    }

    public async Task<Result<ContentItem>> CreateAsync(
        string contentType,
        string title,
        string? body,
        string? excerpt,
        string? fieldsJson,
        DefaultId authorId,
        IEnumerable<DefaultId>? tagIds = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<ContentItem>(new ArgumentException("Title is required."));
        }

        if (title.Length > _settings.MaxTitleLength)
        {
            return Result.Failure<ContentItem>(new ArgumentException($"Title exceeds maximum length of {_settings.MaxTitleLength}."));
        }

        if (!_typeRegistry.TryGet(contentType, out var descriptor) || descriptor == null)
        {
            return Result.Failure<ContentItem>(new ArgumentException($"Content type '{contentType}' is not registered."));
        }

        if (descriptor.HasBody && body != null && body.Length > _settings.MaxBodyLength)
        {
            return Result.Failure<ContentItem>(new ArgumentException($"Body exceeds maximum length of {_settings.MaxBodyLength}."));
        }

        if (excerpt != null && excerpt.Length > _settings.MaxExcerptLength)
        {
            return Result.Failure<ContentItem>(new ArgumentException($"Excerpt exceeds maximum length of {_settings.MaxExcerptLength}."));
        }

        var validationResult = ValidateFields(contentType, fieldsJson);
        if (validationResult.IsFailure)
        {
            return Result.Failure<ContentItem>(validationResult.Error!);
        }

        var tagIdList = tagIds?.Distinct().ToList() ?? [];
        if (tagIdList.Count > 0)
        {
            if (!descriptor.SupportsTags)
            {
                return Result.Failure<ContentItem>(new InvalidOperationException($"Content type '{contentType}' does not support tags."));
            }
            if (tagIdList.Count > _settings.MaxTagsPerContent)
            {
                return Result.Failure<ContentItem>(new ArgumentException($"Maximum allowed tags per content is {_settings.MaxTagsPerContent}."));
            }
        }

        var slug = _slugService.Generate(title);
        var uniqueSlug = await _slugService.EnsureUniqueAsync(slug, contentType, ct: ct);

        var resolvedAuthorId = authorId != default
            ? authorId
            : (_scopeContext.UserId ?? DefaultId.NewUlid());

        var contentItem = new ContentItem
        {
            Id = DefaultId.NewUlid(),
            ContentType = contentType,
            Title = title.Trim(),
            Slug = uniqueSlug,
            Body = descriptor.HasBody ? body : null,
            Excerpt = excerpt?.Trim(),
            FieldsJson = fieldsJson,
            Status = ContentStatus.IsValid(_settings.DefaultStatus) ? _settings.DefaultStatus : ContentStatus.Draft,
            AuthorId = resolvedAuthorId,
            RevisionNumber = 1,
            CreatedAt = DateTime.UtcNow
        };

        if (tagIdList.Count > 0)
        {
            var validTagIds = await _dbContext.Tags
                .Where(t => tagIdList.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync(ct);

            foreach (var tid in validTagIds)
            {
                contentItem.Tags.Add(new ContentItemTag
                {
                    ContentItemId = contentItem.Id,
                    ContentTagId = tid
                });
            }
        }

        var initialRevision = new ContentRevision
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = contentItem.Id,
            RevisionNumber = 1,
            Title = contentItem.Title,
            Body = contentItem.Body,
            Excerpt = contentItem.Excerpt,
            FieldsJson = contentItem.FieldsJson,
            MetadataJson = contentItem.MetadataJson,
            ChangeNote = "Initial creation",
            AuthorId = resolvedAuthorId,
            CreatedAt = contentItem.CreatedAt
        };

        _dbContext.ContentItems.Add(contentItem);
        _dbContext.Revisions.Add(initialRevision);

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ContentCreatedEvent(
            contentItem.Id,
            contentItem.ContentType,
            contentItem.Title,
            contentItem.Slug,
            contentItem.AuthorId), ct);

        _logger.LogInformation("Created content item {ContentId} of type {ContentType} with slug {Slug}",
            contentItem.Id, contentItem.ContentType, contentItem.Slug);

        return Result.Success(contentItem);
    }

    public async Task<Result<ContentItem>> GetByIdAsync(DefaultId id, CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems
            .Include(ci => ci.Tags)
                .ThenInclude(cit => cit.ContentTag)
            .Include(ci => ci.Media)
            .FirstOrDefaultAsync(ci => ci.Id == id, ct);

        if (item == null)
        {
            return Result.Failure<ContentItem>(new KeyNotFoundException($"Content item '{id}' not found."));
        }

        return Result.Success(item);
    }

    public async Task<Result<ContentItem>> GetBySlugAsync(string contentType, string slug, CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems
            .Include(ci => ci.Tags)
                .ThenInclude(cit => cit.ContentTag)
            .Include(ci => ci.Media)
            .FirstOrDefaultAsync(ci => ci.ContentType == contentType && ci.Slug == slug, ct);

        if (item == null)
        {
            return Result.Failure<ContentItem>(new KeyNotFoundException($"Content item '{contentType}:{slug}' not found."));
        }

        return Result.Success(item);
    }

    public async Task<Result> UpdateAsync(
        DefaultId id,
        string? title,
        string? body,
        string? excerpt,
        string? fieldsJson,
        string? metadataJson,
        string? changeNote,
        CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems.FirstOrDefaultAsync(ci => ci.Id == id, ct);
        if (item == null)
        {
            return Result.Failure(new KeyNotFoundException($"Content item '{id}' not found."));
        }

        if (!_typeRegistry.TryGet(item.ContentType, out var descriptor) || descriptor == null)
        {
            return Result.Failure(new ArgumentException($"Content type '{item.ContentType}' is not registered."));
        }

        if (!string.IsNullOrWhiteSpace(title) && title.Length > _settings.MaxTitleLength)
        {
            return Result.Failure(new ArgumentException($"Title exceeds maximum length of {_settings.MaxTitleLength}."));
        }

        if (body != null && descriptor.HasBody && body.Length > _settings.MaxBodyLength)
        {
            return Result.Failure(new ArgumentException($"Body exceeds maximum length of {_settings.MaxBodyLength}."));
        }

        if (excerpt != null && excerpt.Length > _settings.MaxExcerptLength)
        {
            return Result.Failure(new ArgumentException($"Excerpt exceeds maximum length of {_settings.MaxExcerptLength}."));
        }

        if (metadataJson != null && metadataJson.Length > _settings.MaxMetadataJsonLength)
        {
            return Result.Failure(new ArgumentException($"Metadata JSON exceeds maximum length of {_settings.MaxMetadataJsonLength}."));
        }

        if (fieldsJson != null)
        {
            var fieldValidation = ValidateFields(item.ContentType, fieldsJson);
            if (fieldValidation.IsFailure)
            {
                return fieldValidation;
            }
        }

        // Handle title and slug change
        if (!string.IsNullOrWhiteSpace(title) && !string.Equals(title.Trim(), item.Title, StringComparison.Ordinal))
        {
            var generatedSlug = _slugService.Generate(title);
            var uniqueSlug = await _slugService.EnsureUniqueAsync(generatedSlug, item.ContentType, excludeContentId: item.Id, ct: ct);
            item.Title = title.Trim();
            item.Slug = uniqueSlug;
        }

        if (descriptor.HasBody && body != null)
        {
            item.Body = body;
        }

        if (excerpt != null)
        {
            item.Excerpt = excerpt.Trim();
        }

        if (fieldsJson != null)
        {
            item.FieldsJson = fieldsJson;
        }

        if (metadataJson != null)
        {
            item.MetadataJson = metadataJson;
        }

        item.RevisionNumber++;
        item.UpdatedAt = DateTime.UtcNow;

        var authorId = _scopeContext.UserId ?? item.AuthorId;

        var newRevision = new ContentRevision
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = item.Id,
            RevisionNumber = item.RevisionNumber,
            Title = item.Title,
            Body = item.Body,
            Excerpt = item.Excerpt,
            FieldsJson = item.FieldsJson,
            MetadataJson = item.MetadataJson,
            ChangeNote = changeNote,
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Revisions.Add(newRevision);

        // Enforce MaxRevisions pruning if configured
        if (descriptor.MaxRevisions.HasValue && descriptor.MaxRevisions.Value > 0)
        {
            var currentRevisionCount = await _dbContext.Revisions
                .CountAsync(r => r.ContentItemId == item.Id, ct);

            // current count plus the one being added
            var totalCount = currentRevisionCount + 1;
            var excess = totalCount - descriptor.MaxRevisions.Value;
            if (excess > 0)
            {
                var oldestRevisions = await _dbContext.Revisions
                    .Where(r => r.ContentItemId == item.Id)
                    .OrderBy(r => r.RevisionNumber)
                    .Take(excess)
                    .ToListAsync(ct);

                _dbContext.Revisions.RemoveRange(oldestRevisions);
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ContentUpdatedEvent(
            item.Id,
            item.ContentType,
            item.RevisionNumber), ct);

        _logger.LogInformation("Updated content item {ContentId} to revision {RevisionNumber}",
            item.Id, item.RevisionNumber);

        return Result.Success();
    }

    public async Task<Result> PublishAsync(DefaultId id, CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems.FirstOrDefaultAsync(ci => ci.Id == id, ct);
        if (item == null)
        {
            return Result.Failure(new KeyNotFoundException($"Content item '{id}' not found."));
        }

        item.Status = ContentStatus.Published;
        item.PublishedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var publisherId = _scopeContext.UserId ?? item.AuthorId;
        await _eventBus.PublishAsync(new ContentPublishedEvent(
            item.Id,
            item.ContentType,
            item.Slug,
            publisherId), ct);

        _logger.LogInformation("Published content item {ContentId} ({Slug})", item.Id, item.Slug);

        return Result.Success();
    }

    public async Task<Result> UnpublishAsync(DefaultId id, CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems.FirstOrDefaultAsync(ci => ci.Id == id, ct);
        if (item == null)
        {
            return Result.Failure(new KeyNotFoundException($"Content item '{id}' not found."));
        }

        item.Status = ContentStatus.Draft;
        item.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var unpublisherId = _scopeContext.UserId ?? item.AuthorId;
        await _eventBus.PublishAsync(new ContentUnpublishedEvent(
            item.Id,
            item.ContentType,
            item.Slug,
            unpublisherId), ct);

        _logger.LogInformation("Unpublished content item {ContentId} ({Slug})", item.Id, item.Slug);

        return Result.Success();
    }

    public async Task<Result> ArchiveAsync(DefaultId id, CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems.FirstOrDefaultAsync(ci => ci.Id == id, ct);
        if (item == null)
        {
            return Result.Failure(new KeyNotFoundException($"Content item '{id}' not found."));
        }

        item.Status = ContentStatus.Archived;
        item.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var archiverId = _scopeContext.UserId ?? item.AuthorId;
        await _eventBus.PublishAsync(new ContentArchivedEvent(
            item.Id,
            item.ContentType,
            item.Slug,
            archiverId), ct);

        _logger.LogInformation("Archived content item {ContentId} ({Slug})", item.Id, item.Slug);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(DefaultId id, CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems.FirstOrDefaultAsync(ci => ci.Id == id, ct);
        if (item == null)
        {
            return Result.Failure(new KeyNotFoundException($"Content item '{id}' not found."));
        }

        var deleterId = _scopeContext.UserId ?? item.AuthorId;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = deleterId;

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ContentDeletedEvent(
            item.Id,
            item.ContentType,
            item.Slug,
            deleterId), ct);

        _logger.LogInformation("Soft-deleted content item {ContentId} ({Slug})", item.Id, item.Slug);

        return Result.Success();
    }

    public async Task<Result> RestoreRevisionAsync(DefaultId contentId, int revisionNumber, CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems.FirstOrDefaultAsync(ci => ci.Id == contentId, ct);
        if (item == null)
        {
            return Result.Failure(new KeyNotFoundException($"Content item '{contentId}' not found."));
        }

        var revision = await _dbContext.Revisions
            .FirstOrDefaultAsync(r => r.ContentItemId == contentId && r.RevisionNumber == revisionNumber, ct);

        if (revision == null)
        {
            return Result.Failure(new KeyNotFoundException($"Revision {revisionNumber} not found for content item '{contentId}'."));
        }

        item.Title = revision.Title;
        item.Body = revision.Body;
        item.Excerpt = revision.Excerpt;
        item.FieldsJson = revision.FieldsJson;
        item.MetadataJson = revision.MetadataJson;

        // Ensure slug uniqueness if restored title requires update
        var generatedSlug = _slugService.Generate(revision.Title);
        item.Slug = await _slugService.EnsureUniqueAsync(generatedSlug, item.ContentType, excludeContentId: item.Id, ct: ct);

        item.RevisionNumber++;
        item.UpdatedAt = DateTime.UtcNow;

        var authorId = _scopeContext.UserId ?? item.AuthorId;

        var restoreSnapshot = new ContentRevision
        {
            Id = DefaultId.NewUlid(),
            ContentItemId = item.Id,
            RevisionNumber = item.RevisionNumber,
            Title = item.Title,
            Body = item.Body,
            Excerpt = item.Excerpt,
            FieldsJson = item.FieldsJson,
            MetadataJson = item.MetadataJson,
            ChangeNote = $"Restored from revision {revisionNumber}",
            AuthorId = authorId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Revisions.Add(restoreSnapshot);

        await _dbContext.SaveChangesAsync(ct);

        await _eventBus.PublishAsync(new ContentUpdatedEvent(
            item.Id,
            item.ContentType,
            item.RevisionNumber), ct);

        _logger.LogInformation("Restored content item {ContentId} from revision {FromRevision} as revision {NewRevision}",
            item.Id, revisionNumber, item.RevisionNumber);

        return Result.Success();
    }

    public async Task<Result> SetTagsAsync(DefaultId contentId, IEnumerable<DefaultId> tagIds, CancellationToken ct = default)
    {
        var item = await _dbContext.ContentItems.FirstOrDefaultAsync(ci => ci.Id == contentId, ct);
        if (item == null)
        {
            return Result.Failure(new KeyNotFoundException($"Content item '{contentId}' not found."));
        }

        if (!_typeRegistry.TryGet(item.ContentType, out var descriptor) || descriptor == null)
        {
            return Result.Failure(new ArgumentException($"Content type '{item.ContentType}' is not registered."));
        }

        if (!descriptor.SupportsTags)
        {
            return Result.Failure(new InvalidOperationException($"Content type '{item.ContentType}' does not support tags."));
        }

        var distinctTagIds = tagIds.Distinct().ToList();
        if (distinctTagIds.Count > _settings.MaxTagsPerContent)
        {
            return Result.Failure(new ArgumentException($"Maximum allowed tags per content item is {_settings.MaxTagsPerContent}."));
        }

        var existingTags = await _dbContext.ContentItemTags
            .Where(cit => cit.ContentItemId == contentId)
            .ToListAsync(ct);

        _dbContext.ContentItemTags.RemoveRange(existingTags);

        if (distinctTagIds.Count > 0)
        {
            var validTagIds = await _dbContext.Tags
                .Where(t => distinctTagIds.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync(ct);

            foreach (var tid in validTagIds)
            {
                _dbContext.ContentItemTags.Add(new ContentItemTag
                {
                    ContentItemId = contentId,
                    ContentTagId = tid
                });
            }
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated tags for content item {ContentId} ({TagCount} tags)", contentId, distinctTagIds.Count);

        return Result.Success();
    }

    private static Result Fail(Exception ex) => Result.Failure(ex, ex.Message);
    private static Result<T> Fail<T>(Exception ex) => Result.Failure<T>(ex, ex.Message);
}
