using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;
using MultiTenancy.Contracts.Dtos;
using MultiTenancy.Persistence;

namespace MultiTenancy.Features.Members;

/// <summary>
/// Query to list members of a tenant with pagination.
/// </summary>
public record GetMembers(DefaultId TenantId) : PaginatedQuery, IRequest<PaginatedResult<TenantMemberDto>>;

/// <summary>
/// Handles <see cref="GetMembers"/> by querying the tenant's membership list.
/// </summary>
[Logging(Order = 1)]
public class GetMembersHandler : IRequestHandler<GetMembers, PaginatedResult<TenantMemberDto>>
{
    private readonly TenantModuleDbContext _dbContext;

    public GetMembersHandler(TenantModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PaginatedResult<TenantMemberDto>>> HandleAsync(
        GetMembers request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Members
            .AsNoTracking()
            .Where(m => m.TenantId == request.TenantId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new TenantMemberDto(
                m.UserId,
                m.Role,
                m.JoinedAt));

        var result = await query.ToPaginatedResultAsync(request, cancellationToken);
        return Result.Success(result);
    }
}
