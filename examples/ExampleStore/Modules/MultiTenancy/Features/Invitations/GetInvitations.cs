using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Persistence;

namespace MultiTenancy.Features.Invitations;

/// <summary>
/// Query to list all pending invitations for a tenant.
/// </summary>
public record GetInvitations(DefaultId TenantId) : IRequest<IEnumerable<TenantInvitationDto>>;

/// <summary>
/// Handles <see cref="GetInvitations"/> by querying pending invitations from the database.
/// </summary>
[Logging(Order = 1)]
public class GetInvitationsHandler : IRequestHandler<GetInvitations, IEnumerable<TenantInvitationDto>>
{
    private readonly TenantModuleDbContext _dbContext;

    public GetInvitationsHandler(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IEnumerable<TenantInvitationDto>>> HandleAsync(
        GetInvitations request,
        CancellationToken cancellationToken = default)
    {
        var invitations = await _dbContext.Invitations
            .AsNoTracking()
            .Where(i => i.TenantId == request.TenantId && !i.IsAccepted && !i.IsRevoked)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new TenantInvitationDto(
                i.Id,
                i.Email,
                i.Role,
                i.ExpiresAt,
                i.IsAccepted))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<TenantInvitationDto>>(invitations);
    }
}
