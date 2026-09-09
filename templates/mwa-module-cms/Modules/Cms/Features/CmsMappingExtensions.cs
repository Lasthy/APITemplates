using Cms.ContentTypes;
using Cms.Contracts.Dtos;
using Cms.Entities;

namespace Cms.Features;

/// <summary>
/// Mapping extensions from entities and descriptors to DTOs.
/// </summary>
public static class CmsMappingExtensions
{
    public static ContentItemDto ToDto(this ContentItem item)
    {
        return new ContentItemDto(
            item.Id,
            item.ContentType,
            item.Title,
            item.Slug,
            item.Excerpt,
            item.Status,
            item.AuthorId,
            item.PublishedAt,
            item.RevisionNumber,
            item.CreatedAt);
    }

    public static ContentItemDetailDto ToDetailDto(this ContentItem item)
    {
        var tags = item.Tags?
            .Where(t => t.ContentTag != null)
            .Select(t => t.ContentTag.ToDto())
            .ToList() ?? [];

        var media = item.Media?
            .Select(m => m.ToDto())
            .ToList() ?? [];

        return new ContentItemDetailDto(
            item.Id,
            item.ContentType,
            item.Title,
            item.Slug,
            item.Body,
            item.Excerpt,
            item.FieldsJson,
            item.MetadataJson,
            item.Status,
            item.AuthorId,
            item.PublishAt,
            item.PublishedAt,
            item.RevisionNumber,
            item.SortOrder,
            tags,
            media,
            item.CreatedAt);
    }

    public static ContentRevisionDto ToDto(this ContentRevision revision)
    {
        return new ContentRevisionDto(
            revision.Id,
            revision.RevisionNumber,
            revision.AuthorId,
            revision.ChangeNote,
            revision.CreatedAt);
    }

    public static ContentRevisionDetailDto ToDetailDto(this ContentRevision revision)
    {
        return new ContentRevisionDetailDto(
            revision.Id,
            revision.RevisionNumber,
            revision.Title,
            revision.Body,
            revision.Excerpt,
            revision.FieldsJson,
            revision.MetadataJson,
            revision.AuthorId,
            revision.ChangeNote,
            revision.CreatedAt);
    }

    public static ContentTagDto ToDto(this ContentTag tag)
    {
        return new ContentTagDto(
            tag.Id,
            tag.Name,
            tag.Slug,
            tag.Description);
    }

    public static ContentMediaDto ToDto(this ContentMedia media)
    {
        return new ContentMediaDto(
            media.Id,
            media.FileName,
            media.MimeType,
            media.SizeBytes,
            media.AltText,
            media.Width,
            media.Height,
            media.PublicUrl,
            media.CreatedAt);
    }

    public static ContentFieldDto ToDto(this ContentField field)
    {
        return new ContentFieldDto(
            field.Name,
            field.DisplayName,
            field.FieldType.ToString(),
            field.IsRequired,
            field.DefaultValue,
            field.Description,
            field.MaxLength,
            field.ValidationRegex);
    }

    public static ContentTypeDto ToDto(this ContentTypeDescriptor descriptor)
    {
        return new ContentTypeDto(
            descriptor.Name,
            descriptor.Description,
            descriptor.Icon,
            descriptor.HasBody);
    }

    public static ContentTypeDetailDto ToDetailDto(this ContentTypeDescriptor descriptor)
    {
        var fields = descriptor.Fields.Select(f => f.ToDto()).ToList();

        return new ContentTypeDetailDto(
            descriptor.Name,
            descriptor.Description,
            descriptor.Icon,
            descriptor.HasBody,
            descriptor.BodyFormat,
            descriptor.SupportsTags,
            descriptor.SupportsMedia,
            descriptor.MaxRevisions,
            fields);
    }
}
