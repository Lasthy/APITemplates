using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Logout request: revokes all refresh tokens for the authenticated user.
/// </summary>
public record Logout(DefaultId UserId) : IRequest<bool>;
