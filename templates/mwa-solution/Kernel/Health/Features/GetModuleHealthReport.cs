using Kernel.Common;
using Kernel.Requests;

namespace Kernel.Health.Features;

/// <summary>
/// CQRS request for retrieving the health report for a specific module.
/// </summary>
public record GetModuleHealthReport(string ModuleName) : IRequest<ModuleHealthReport>;

[Logging]
public class GetModuleHealthReportHandler : IRequestHandler<GetModuleHealthReport, ModuleHealthReport>
{
    private readonly IHealthService _healthService;

    public GetModuleHealthReportHandler(IHealthService healthService)
    {
        _healthService = healthService;
    }

    public async Task<Result<ModuleHealthReport>> HandleAsync(GetModuleHealthReport request, CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _healthService.GetModuleHealthReportAsync(request.ModuleName, cancellationToken);
            if (report == null)
            {
                return Result.Failure<ModuleHealthReport>(
                    new KeyNotFoundException($"No health check registrations found for module '{request.ModuleName}'."));
            }

            return Result.Success(report);
        }
        catch (Exception ex)
        {
            return Result.Failure<ModuleHealthReport>(ex);
        }
    }
}
