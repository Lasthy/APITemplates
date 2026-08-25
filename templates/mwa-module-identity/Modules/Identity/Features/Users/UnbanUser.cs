using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to unban a user (admin only).
/// </summary>
public record UnbanUser(DefaultId UserId, DefaultId UnbannedBy) : IRequest<bool>;
