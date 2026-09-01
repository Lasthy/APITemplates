using Kernel.Common;
using Kernel.Requests;

namespace Kernel.Health.Features;

/// <summary>
/// CQRS request for retrieving the aggregated health report across all modules.
/// </summary>
public record GetHealthReport : IRequest<HealthReport>;

[Logging]
public class GetHealthReportHandler : IRequestHandler<GetHealthReport, HealthReport>
{
    private readonly IHealthService _healthService;

    public GetHealthReportHandler(IHealthService healthService)
    {
        _healthService = healthService;
    }

    public async Task<Result<HealthReport>> HandleAsync(GetHealthReport request, CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _healthService.GetHealthReportAsync(cancellationToken);
            return Result.Success(report);
        }
        catch (Exception ex)
        {
            return Result.Failure<HealthReport>(ex);
        }
    }
}
