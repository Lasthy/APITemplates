using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Command to change the authenticated user's password.
/// </summary>
public record ChangePassword(
    DefaultId UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<bool>;
