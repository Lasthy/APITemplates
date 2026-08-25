using Identity.Contracts.Dtos;
using Identity.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Token refresh request using an existing refresh token.
/// </summary>
public record RefreshToken(string Token) : IRequest<AuthTokenDto>;

/// <summary>
/// Handles <see cref="RefreshToken"/> requests by delegating to <see cref="IUserService"/>.
/// </summary>
[Logging(Order = 1)]
public class RefreshTokenHandler : IRequestHandler<RefreshToken, AuthTokenDto>
{
    private readonly IUserService _userService;

    public RefreshTokenHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<Result<AuthTokenDto>> HandleAsync(RefreshToken request, CancellationToken cancellationToken = default)
    {
        return await _userService.RefreshAsync(request.Token, cancellationToken);
    }
}
