using Identity.Contracts.Events;
using Identity.Services;
using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Command to reset a user's password using a valid reset token.
/// </summary>
public record ResetPassword(
    string Email,
    string Token,
    string NewPassword) : IRequest<bool>;

/// <summary>
/// Handles <see cref="ResetPassword"/> by verifying the token, updating the password,
/// revoking active sessions, and publishing <see cref="PasswordResetCompletedEvent"/>.
/// </summary>
[Logging(Order = 1)]
public class ResetPasswordHandler : IRequestHandler<ResetPassword, bool>
{
    private readonly IUserService _userService;
    private readonly IEventBus _eventBus;

    public ResetPasswordHandler(IUserService userService, IEventBus eventBus)
    {
        _userService = userService;
        _eventBus = eventBus;
    }

    public async Task<Result<bool>> HandleAsync(ResetPassword request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.ResetPasswordAsync(
            request.Email,
            request.Token,
            request.NewPassword,
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        var user = result.Value!;

        await _eventBus.PublishAsync(
            new PasswordResetCompletedEvent(user.Id, request.Email),
            cancellationToken);

        return Result.Success(true);
    }
}
