using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Token refresh request using an existing refresh token.
/// </summary>
public record RefreshToken(string Token) : IRequest<AuthTokenDto>;
