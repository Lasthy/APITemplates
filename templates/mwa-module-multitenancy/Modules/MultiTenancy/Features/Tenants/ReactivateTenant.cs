using Kernel.Common;
using Kernel.Requests;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Tenants;

/// <summary>
/// Command to reactivate a suspended tenant organization (System administrators only).
/// </summary>
public record ReactivateTenant(DefaultId Id) : IRequest<bool>;

/// <summary>
/// Handles <see cref="ReactivateTenant"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class ReactivateTenantHandler : IRequestHandler<ReactivateTenant, bool>
{
    private readonly ITenantService _tenantService;

    public ReactivateTenantHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<Result<bool>> HandleAsync(ReactivateTenant request, CancellationToken cancellationToken = default)
    {
        var result = await _tenantService.ReactivateAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
