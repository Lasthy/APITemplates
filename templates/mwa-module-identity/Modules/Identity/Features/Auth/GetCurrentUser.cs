using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Query for the currently authenticated user's details.
/// </summary>
public record GetCurrentUser(DefaultId UserId) : IRequest<UserDetailDto>;
