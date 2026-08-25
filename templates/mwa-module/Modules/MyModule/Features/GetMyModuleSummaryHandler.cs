using Kernel.Common;
using Kernel.Requests;
using Microsoft.Extensions.Logging;
using MyModule.Contracts.Dtos;
using MyModule.Contracts.Requests;

namespace MyModule.Features;

/// <summary>
/// Handles <see cref="GetMyModuleSummary"/> requests dispatched across module boundaries.
/// </summary>
[Logging(Order = 1)]
public class GetMyModuleSummaryHandler : IRequestHandler<GetMyModuleSummary, MyModuleSummaryDto>
{
    private readonly ILogger<GetMyModuleSummaryHandler> _logger;

    public GetMyModuleSummaryHandler(ILogger<GetMyModuleSummaryHandler> logger)
    {
        _logger = logger;
    }

    public Task<Result<MyModuleSummaryDto>> HandleAsync(GetMyModuleSummary request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing GetMyModuleSummary for ID {Id}", request.Id);

        var summary = new MyModuleSummaryDto(
            Id: request.Id,
            Name: "Sample Resource",
            IsActive: true
        );

        return Task.FromResult(Result.Success(summary));
    }
}
