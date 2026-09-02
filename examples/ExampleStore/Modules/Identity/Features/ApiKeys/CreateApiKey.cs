using Identity.Contracts.Dtos;
using Identity.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.ApiKeys;

/// <summary>
/// Command to create a new API key for a user.
/// </summary>
public record CreateApiKey(
    DefaultId UserId,
    string Name,
    DateTime? ExpiresAt = null,
    IReadOnlyList<string>? Scopes = null) : IRequest<ApiKeyCreatedDto>;

/// <summary>
/// Handles <see cref="CreateApiKey"/> by delegating to <see cref="IApiKeyService"/>.
/// </summary>
[Logging(Order = 1)]
public class CreateApiKeyHandler : IRequestHandler<CreateApiKey, ApiKeyCreatedDto>
{
    private readonly IApiKeyService _apiKeyService;

    public CreateApiKeyHandler(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    public async Task<Result<ApiKeyCreatedDto>> HandleAsync(CreateApiKey request, CancellationToken cancellationToken = default)
    {
        return await _apiKeyService.GenerateKeyAsync(
            request.UserId,
            request.Name,
            request.ExpiresAt,
            request.Scopes,
            cancellationToken);
    }
}
