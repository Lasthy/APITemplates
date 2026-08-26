using Kernel.Common;
using Kernel.Requests;
using Microsoft.Extensions.Logging;
using Orders.Contracts.Dtos;
using Orders.Contracts.Requests;

namespace Orders.Features;

/// <summary>
/// Handles <see cref="GetOrdersSummary"/> requests dispatched across module boundaries.
/// </summary>
[Logging(Order = 1)]
public class GetOrdersSummaryHandler : IRequestHandler<GetOrdersSummary, OrdersSummaryDto>
{
    private readonly ILogger<GetOrdersSummaryHandler> _logger;

    public GetOrdersSummaryHandler(ILogger<GetOrdersSummaryHandler> logger)
    {
        _logger = logger;
    }

    public Task<Result<OrdersSummaryDto>> HandleAsync(GetOrdersSummary request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing GetOrdersSummary for ID {Id}", request.Id);

        var summary = new OrdersSummaryDto(
            Id: request.Id,
            Name: "Sample Resource",
            IsActive: true
        );

        return Task.FromResult(Result.Success(summary));
    }
}
