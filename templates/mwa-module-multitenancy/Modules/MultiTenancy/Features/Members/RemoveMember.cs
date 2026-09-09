using Kernel.Common;
using Kernel.Context;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Constants;
using MultiTenancy.Persistence;
using MultiTenancy.Services;

namespace MultiTenancy.Features.Members;

/// <summary>
/// Command to remove a member from a tenant (or self-leave).
/// </summary>
public record RemoveMember(DefaultId TenantId, DefaultId UserId) : IRequest<bool>;

/// <summary>
/// Handles <see cref="RemoveMember"/> with authorization check (Admin/Owner/System or Self) and calls <see cref="ITenantService"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class RemoveMemberHandler : IRequestHandler<RemoveMember, bool>
{
    private readonly ITenantService _tenantService;
    private readonly TenantModuleDbContext _dbContext;
    private readonly IScopeContext _scopeContext;

    public RemoveMemberHandler(
        ITenantService tenantService,
        TenantModuleDbContext dbContext,
        IScopeContext scopeContext)
    {
        _tenantService = tenantService;
        _dbContext = dbContext;
        _scopeContext = scopeContext;
    }

    public async Task<Result<bool>> HandleAsync(RemoveMember request, CancellationToken cancellationToken = default)
    {
        if (!_scopeContext.IsAuthenticated || !_scopeContext.UserId.HasValue)
        {
            return Result.Failure<bool>(new UnauthorizedAccessException("Authentication required."));
        }

        var callerId = _scopeContext.UserId.Value;
        var isSelf = callerId == request.UserId;

        if (!isSelf && !_scopeContext.IsSystem)
        {
            // Must be Admin or Owner in the tenant to remove another member
            var callerMember = await _dbContext.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantId == request.TenantId && m.UserId == callerId, cancellationToken);

            if (callerMember == null ||
                (callerMember.Role != TenantRoles.Admin && callerMember.Role != TenantRoles.Owner))
            {
                return Result.Failure<bool>(new UnauthorizedAccessException("Tenant administrator or owner role required to remove members."));
            }
        }

        var result = await _tenantService.RemoveMemberAsync(request.TenantId, request.UserId, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
