using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Persistence;

namespace MultiTenancy.Features.Members;

/// <summary>
/// Query to get the current authenticated user's membership details in a tenant.
/// </summary>
public record GetCurrentMembership(DefaultId TenantId) : IRequest<TenantMemberDto>;

/// <summary>
/// Handles <see cref="GetCurrentMembership"/> by querying the caller's membership.
/// </summary>
[Logging(Order = 1)]
public class GetCurrentMembershipHandler : IRequestHandler<GetCurrentMembership, TenantMemberDto>
{
    private readonly TenantModuleDbContext _dbContext;
    private readonly IScopeContext _scopeContext;

    public GetCurrentMembershipHandler(TenantModuleDbContext dbContext, IScopeContext scopeContext)
    {
        _dbContext = dbContext;
        _scopeContext = scopeContext;
    }

    public async Task<Result<TenantMemberDto>> HandleAsync(
        GetCurrentMembership request,
        CancellationToken cancellationToken = default)
    {
        if (!_scopeContext.IsAuthenticated || !_scopeContext.UserId.HasValue)
        {
            return Result.Failure<TenantMemberDto>(new UnauthorizedAccessException("Authentication required."));
        }

        var userId = _scopeContext.UserId.Value;

        var member = await _dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == request.TenantId && m.UserId == userId, cancellationToken);

        if (member == null)
        {
            return Result.Failure<TenantMemberDto>(new KeyNotFoundException("You are not a member of this tenant."));
        }

        return Result.Success(new TenantMemberDto(member.UserId, member.Role, member.JoinedAt));
    }
}
