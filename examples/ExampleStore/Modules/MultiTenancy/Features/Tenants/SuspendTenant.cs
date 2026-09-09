using Kernel.Common;
using Kernel.Requests;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Tenants;

/// <summary>
/// Command to suspend a tenant organization (System administrators only).
/// </summary>
public record SuspendTenant(DefaultId Id, string? Reason = null) : IRequest<bool>;

/// <summary>
/// Handles <see cref="SuspendTenant"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class SuspendTenantHandler : IRequestHandler<SuspendTenant, bool>
{
    private readonly ITenantService _tenantService;

    public SuspendTenantHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<Result<bool>> HandleAsync(SuspendTenant request, CancellationToken cancellationToken = default)
    {
        var result = await _tenantService.SuspendAsync(request.Id, request.Reason, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
