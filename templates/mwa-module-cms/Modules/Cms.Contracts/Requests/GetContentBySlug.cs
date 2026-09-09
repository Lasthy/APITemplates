using Cms.Contracts.Dtos;
using Kernel.Requests;

namespace Cms.Contracts.Requests;

/// <summary>
/// Cross-module request to get content item details by content type and slug.
/// </summary>
public record GetContentBySlug(
    string ContentType,
    string Slug) : IRequest<ContentItemDetailDto>;
