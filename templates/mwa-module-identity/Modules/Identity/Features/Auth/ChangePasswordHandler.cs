using Identity.Contracts.Events;
using Identity.Services;
using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Handles <see cref="ChangePassword"/> by verifying credentials, updating the password,
/// revoking refresh tokens, and publishing <see cref="PasswordChangedEvent"/>.
/// </summary>
[Logging(Order = 1)]
public class ChangePasswordHandler : IRequestHandler<ChangePassword, bool>
{
    private readonly IUserService _userService;
    private readonly IEventBus _eventBus;

    public ChangePasswordHandler(IUserService userService, IEventBus eventBus)
    {
        _userService = userService;
        _eventBus = eventBus;
    }

    public async Task<Result<bool>> HandleAsync(ChangePassword request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.ChangePasswordAsync(
            request.UserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        await _eventBus.PublishAsync(
            new PasswordChangedEvent(request.UserId),
            cancellationToken);

        return Result.Success(true);
    }
}
