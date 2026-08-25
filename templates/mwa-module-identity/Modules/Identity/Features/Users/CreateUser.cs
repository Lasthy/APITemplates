using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to create a new user with a specified profile.
/// </summary>
public record CreateUser(string Email, string UserName, string Password, string ProfileName) : IRequest<UserDto>;
