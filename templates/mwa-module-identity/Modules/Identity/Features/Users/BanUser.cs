using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to ban a user (admin only).
/// </summary>
public record BanUser(DefaultId UserId, DefaultId BannedBy, string? Reason) : IRequest<bool>;
