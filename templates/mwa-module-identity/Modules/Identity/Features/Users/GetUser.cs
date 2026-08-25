using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Query to get a single user's full details by ID (admin or self).
/// </summary>
public record GetUser(DefaultId UserId) : IRequest<UserDetailDto>;
