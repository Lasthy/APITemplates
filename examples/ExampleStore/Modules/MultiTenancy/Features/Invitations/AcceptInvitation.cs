using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Invitations;

/// <summary>
/// Command to accept an invitation using an invitation token.
/// </summary>
public record AcceptInvitation(string Token) : IRequest<bool>;

/// <summary>
/// Handles <see cref="AcceptInvitation"/> by joining the current authenticated user to the inviting tenant.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class AcceptInvitationHandler : IRequestHandler<AcceptInvitation, bool>
{
    private readonly ITenantService _tenantService;
    private readonly IScopeContext _scopeContext;

    public AcceptInvitationHandler(ITenantService tenantService, IScopeContext scopeContext)
    {
        _tenantService = tenantService;
        _scopeContext = scopeContext;
    }

    public async Task<Result<bool>> HandleAsync(AcceptInvitation request, CancellationToken cancellationToken = default)
    {
        if (!_scopeContext.IsAuthenticated || !_scopeContext.UserId.HasValue)
        {
            return Result.Failure<bool>(new UnauthorizedAccessException("Authentication required to accept an invitation."));
        }

        var result = await _tenantService.AcceptInvitationAsync(request.Token, _scopeContext.UserId.Value, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
