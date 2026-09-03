using Identity.Contracts.Dtos;
using Identity.Contracts.Events;
using Identity.Services;
using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to create a new user with a specified profile.
/// </summary>
public record CreateUser(string Email, string UserName, string Password, string ProfileName) : IRequest<UserDto>;

/// <summary>
/// Handles <see cref="CreateUser"/> by creating the user via <see cref="IUserService"/>
/// and publishing a <see cref="UserCreatedEvent"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class CreateUserHandler : IRequestHandler<CreateUser, UserDto>
{
    private readonly IUserService _userService;
    private readonly IEventBus _eventBus;

    public CreateUserHandler(IUserService userService, IEventBus eventBus)
    {
        _userService = userService;
        _eventBus = eventBus;
    }

    public async Task<Result<UserDto>> HandleAsync(CreateUser request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.CreateUserAsync(
            request.Email,
            request.UserName,
            request.Password,
            request.ProfileName,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<UserDto>(result.Error!);
        }

        var user = result.Value!;

        await _eventBus.PublishAsync(
            new UserCreatedEvent(user.Id, user.ProfileName, user.Email ?? string.Empty),
            cancellationToken);

        return Result.Success(new UserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            UserName: user.UserName ?? string.Empty,
            ProfileName: user.ProfileName,
            IsBanned: user.IsBanned,
            IsDummy: user.IsDummy,
            CreatedAt: user.CreatedAt,
            DummyExpiresAt: user.DummyExpiresAt));
    }
}
