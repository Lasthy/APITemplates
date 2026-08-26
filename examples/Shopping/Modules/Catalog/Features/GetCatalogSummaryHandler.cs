using Kernel.Common;
using Kernel.Requests;
using Microsoft.Extensions.Logging;
using Catalog.Contracts.Dtos;
using Catalog.Contracts.Requests;

namespace Catalog.Features;

/// <summary>
/// Handles <see cref="GetCatalogSummary"/> requests dispatched across module boundaries.
/// </summary>
[Logging(Order = 1)]
public class GetCatalogSummaryHandler : IRequestHandler<GetCatalogSummary, CatalogSummaryDto>
{
    private readonly ILogger<GetCatalogSummaryHandler> _logger;

    public GetCatalogSummaryHandler(ILogger<GetCatalogSummaryHandler> logger)
    {
        _logger = logger;
    }

    public Task<Result<CatalogSummaryDto>> HandleAsync(GetCatalogSummary request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing GetCatalogSummary for ID {Id}", request.Id);

        var summary = new CatalogSummaryDto(
            Id: request.Id,
            Name: "Sample Resource",
            IsActive: true
        );

        return Task.FromResult(Result.Success(summary));
    }
}
