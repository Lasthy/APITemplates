using Kernel.Common;
using Kernel.Requests;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Invitations;

/// <summary>
/// Command to create and send a pending invitation to join a tenant.
/// </summary>
public record CreateInvitation(DefaultId TenantId, string Email, string Role) : IRequest<TenantInvitationDto>;

/// <summary>
/// Handles <see cref="CreateInvitation"/> via <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class CreateInvitationHandler : IRequestHandler<CreateInvitation, TenantInvitationDto>
{
    private readonly ITenantService _tenantService;

    public CreateInvitationHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<Result<TenantInvitationDto>> HandleAsync(
        CreateInvitation request,
        CancellationToken cancellationToken = default)
    {
        var result = await _tenantService.InviteAsync(
            request.TenantId,
            request.Email,
            request.Role,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<TenantInvitationDto>(result.Error!);
        }

        var inv = result.Value!;
        return Result.Success(new TenantInvitationDto(
            inv.Id,
            inv.Email,
            inv.Role,
            inv.ExpiresAt,
            inv.IsAccepted));
    }
}
