using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Tenants;

/// <summary>
/// Command to create a new tenant organization. The authenticated caller becomes the tenant owner.
/// </summary>
public record CreateTenant(string Name, string? Plan = null) : IRequest<TenantDto>;

/// <summary>
/// Handles <see cref="CreateTenant"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class CreateTenantHandler : IRequestHandler<CreateTenant, TenantDto>
{
    private readonly ITenantService _tenantService;
    private readonly IScopeContext _scopeContext;

    public CreateTenantHandler(ITenantService tenantService, IScopeContext scopeContext)
    {
        _tenantService = tenantService;
        _scopeContext = scopeContext;
    }

    public async Task<Result<TenantDto>> HandleAsync(CreateTenant request, CancellationToken cancellationToken = default)
    {
        if (!_scopeContext.IsAuthenticated || !_scopeContext.UserId.HasValue)
        {
            return Result.Failure<TenantDto>(new UnauthorizedAccessException("Authentication required to create a tenant."));
        }

        var result = await _tenantService.CreateAsync(
            request.Name,
            _scopeContext.UserId.Value,
            request.Plan,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<TenantDto>(result.Error!);
        }

        var tenant = result.Value!;
        return Result.Success(new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.Plan,
            tenant.CreatedAt));
    }
}
