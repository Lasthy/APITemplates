using Kernel.Requests;

namespace Identity.Contracts.Requests;

/// <summary>
/// Cross-module request to check whether a user has the specified role.
/// Returns <c>true</c> if the user exists, is not banned, and holds the role.
/// Dispatched across modules via <see cref="IDispatcher"/>.
/// </summary>
public record ValidateUserAccess(DefaultId UserId, string RequiredRole) : IRequest<bool>;
