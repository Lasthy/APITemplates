using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to update a user's email and/or username.
/// </summary>
public record UpdateUser(DefaultId UserId, string? Email, string? UserName) : IRequest<bool>;
