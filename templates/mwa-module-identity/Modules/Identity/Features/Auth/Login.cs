using Identity.Contracts.Dtos;
using Identity.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Authentication request: email + password login.
/// </summary>
public record Login(string Email, string Password) : IRequest<AuthTokenDto>;

/// <summary>
/// Handles <see cref="Login"/> requests by delegating to <see cref="IUserService"/>.
/// </summary>
[Logging(Order = 1)]
public class LoginHandler : IRequestHandler<Login, AuthTokenDto>
{
    private readonly IUserService _userService;

    public LoginHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<Result<AuthTokenDto>> HandleAsync(Login request, CancellationToken cancellationToken = default)
    {
        return await _userService.AuthenticateAsync(request.Email, request.Password, cancellationToken);
    }
}
