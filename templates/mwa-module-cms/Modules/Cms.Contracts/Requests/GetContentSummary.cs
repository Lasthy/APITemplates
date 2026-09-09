using Cms.Contracts.Dtos;
using Kernel.Requests;

namespace Cms.Contracts.Requests;

/// <summary>
/// Cross-module request to get content item summary by ID.
/// </summary>
public record GetContentSummary(
    DefaultId ContentId) : IRequest<ContentItemDto>;
