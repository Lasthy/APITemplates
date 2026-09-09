using Kernel.Common;
using Kernel.Requests;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Invitations;

/// <summary>
/// Command to revoke a pending invitation.
/// </summary>
public record RevokeInvitation(DefaultId Id) : IRequest<bool>;

/// <summary>
/// Handles <see cref="RevokeInvitation"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class RevokeInvitationHandler : IRequestHandler<RevokeInvitation, bool>
{
    private readonly ITenantService _tenantService;

    public RevokeInvitationHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<Result<bool>> HandleAsync(RevokeInvitation request, CancellationToken cancellationToken = default)
    {
        var result = await _tenantService.RevokeInvitationAsync(request.Id, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
