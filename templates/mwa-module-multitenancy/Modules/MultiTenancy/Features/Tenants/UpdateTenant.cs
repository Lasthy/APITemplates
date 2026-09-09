using Kernel.Common;
using Kernel.Requests;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Tenants;

/// <summary>
/// Command to update tenant attributes (name, plan, metadata).
/// </summary>
public record UpdateTenant(
    DefaultId Id,
    string? Name,
    string? Plan,
    string? MetadataJson) : IRequest<bool>;

/// <summary>
/// Handles <see cref="UpdateTenant"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class UpdateTenantHandler : IRequestHandler<UpdateTenant, bool>
{
    private readonly ITenantService _tenantService;

    public UpdateTenantHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<Result<bool>> HandleAsync(UpdateTenant request, CancellationToken cancellationToken = default)
    {
        var result = await _tenantService.UpdateAsync(
            request.Id,
            request.Name,
            request.Plan,
            request.MetadataJson,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
