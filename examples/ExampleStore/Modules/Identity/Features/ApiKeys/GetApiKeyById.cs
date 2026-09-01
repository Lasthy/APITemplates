using Identity.Contracts.Dtos;
using Identity.Persistence;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.EntityFrameworkCore;

namespace Identity.Features.ApiKeys;

/// <summary>
/// Query to get an API key by its unique ID.
/// </summary>
public record GetApiKeyById(DefaultId KeyId) : IRequest<ApiKeyDto>;

/// <summary>
/// Handles <see cref="GetApiKeyById"/> by querying the Identity database.
/// </summary>
[Logging(Order = 1)]
public class GetApiKeyByIdHandler : IRequestHandler<GetApiKeyById, ApiKeyDto>
{
    private readonly IdentityModuleDbContext _dbContext;

    public GetApiKeyByIdHandler(IdentityModuleDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ApiKeyDto>> HandleAsync(GetApiKeyById request, CancellationToken cancellationToken = default)
    {
        var key = await _dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.Id == request.KeyId)
            .Select(k => new ApiKeyDto(
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.UserId,
                k.ExpiresAt,
                k.RevokedAt,
                k.LastUsedAt,
                k.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (key == null)
        {
            return Result.Failure<ApiKeyDto>(new InvalidOperationException($"API key with ID '{request.KeyId}' was not found."));
        }

        return Result.Success(key);
    }
}
