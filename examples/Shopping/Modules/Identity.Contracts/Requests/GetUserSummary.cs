using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Contracts.Requests;

/// <summary>
/// Cross-module query request to fetch summary details of a user.
/// Dispatched across modules via <see cref="IDispatcher"/>.
/// </summary>
public record GetUserSummary(DefaultId UserId) : IRequest<UserDto>;
