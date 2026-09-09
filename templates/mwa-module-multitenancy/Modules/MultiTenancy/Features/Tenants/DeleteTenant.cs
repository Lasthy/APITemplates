using Kernel.Common;
using Kernel.Requests;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Tenants;

/// <summary>
/// Command to soft-delete a tenant organization.
/// </summary>
public record DeleteTenant(DefaultId Id) : IRequest<bool>;

/// <summary>
/// Handles <see cref="DeleteTenant"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class DeleteTenantHandler : IRequestHandler<DeleteTenant, bool>
{
    private readonly ITenantService _tenantService;

    public DeleteTenantHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<Result<bool>> HandleAsync(DeleteTenant request, CancellationToken cancellationToken = default)
    {
        var result = await _tenantService.DeleteAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
