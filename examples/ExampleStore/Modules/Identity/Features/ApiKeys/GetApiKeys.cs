using Identity.Contracts.Dtos;
using Identity.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Identity.Features.ApiKeys;

/// <summary>
/// Query to get API keys, optionally filtered by user ID.
/// </summary>
public record GetApiKeys(
    DefaultId? UserId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<IEnumerable<ApiKeyDto>>;

/// <summary>
/// Handles <see cref="GetApiKeys"/> by querying the Identity database.
/// </summary>
[Logging(Order = 1)]
public class GetApiKeysHandler : IRequestHandler<GetApiKeys, IEnumerable<ApiKeyDto>>
{
    private readonly IdentityModuleDbContext _dbContext;

    public GetApiKeysHandler(IdentityModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IEnumerable<ApiKeyDto>>> HandleAsync(GetApiKeys request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ApiKeys.AsNoTracking().AsQueryable();

        if (request.UserId.HasValue)
        {
            query = query.Where(k => k.UserId == request.UserId.Value);
        }

        var keys = await query
            .OrderByDescending(k => k.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(k => new ApiKeyDto(
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.UserId,
                k.ExpiresAt,
                k.RevokedAt,
                k.LastUsedAt,
                k.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<ApiKeyDto>>(keys);
    }
}
