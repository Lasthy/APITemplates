using Identity.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.ApiKeys;

/// <summary>
/// Command to revoke an API key.
/// </summary>
public record RevokeApiKey(
    DefaultId KeyId,
    DefaultId RequestingUserId,
    bool IsManagerOrSystem = false) : IRequest<bool>;

/// <summary>
/// Handles <see cref="RevokeApiKey"/> by delegating to <see cref="IApiKeyService"/>.
/// </summary>
[Logging(Order = 1)]
public class RevokeApiKeyHandler : IRequestHandler<RevokeApiKey, bool>
{
    private readonly IApiKeyService _apiKeyService;

    public RevokeApiKeyHandler(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    public async Task<Result<bool>> HandleAsync(RevokeApiKey request, CancellationToken cancellationToken = default)
    {
        var result = await _apiKeyService.RevokeKeyAsync(
            request.KeyId,
            request.RequestingUserId,
            request.IsManagerOrSystem,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        return Result.Success(true);
    }
}
