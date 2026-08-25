using Identity.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Logout request: revokes all refresh tokens for the authenticated user.
/// </summary>
public record Logout(DefaultId UserId) : IRequest<bool>;

/// <summary>
/// Handles <see cref="Logout"/> requests by revoking all refresh tokens for the user.
/// </summary>
[Logging(Order = 1)]
public class LogoutHandler : IRequestHandler<Logout, bool>
{
    private readonly IUserService _userService;

    public LogoutHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<Result<bool>> HandleAsync(Logout request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.RevokeAllRefreshTokensAsync(request.UserId, cancellationToken);
        return result.IsSuccess
            ? Result.Success(true)
            : Result.Failure<bool>(result.Error!);
    }
}
