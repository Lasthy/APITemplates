using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Query to get a paginated list of all users (admin only).
/// </summary>
public record GetUsers(int Page = 1, int PageSize = 20, string? ProfileFilter = null) : IRequest<IEnumerable<UserDto>>;
