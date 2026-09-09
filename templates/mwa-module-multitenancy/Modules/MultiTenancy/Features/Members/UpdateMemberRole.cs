using Kernel.Common;
using Kernel.Requests;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Members;

/// <summary>
/// Command to update the role of a tenant member (TenantOwner or System only).
/// </summary>
public record UpdateMemberRole(DefaultId TenantId, DefaultId UserId, string Role) : IRequest<bool>;

/// <summary>
/// Handles <see cref="UpdateMemberRole"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class UpdateMemberRoleHandler : IRequestHandler<UpdateMemberRole, bool>
{
    private readonly ITenantService _tenantService;

    public UpdateMemberRoleHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<Result<bool>> HandleAsync(UpdateMemberRole request, CancellationToken cancellationToken = default)
    {
        var result = await _tenantService.UpdateMemberRoleAsync(
            request.TenantId,
            request.UserId,
            request.Role,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
