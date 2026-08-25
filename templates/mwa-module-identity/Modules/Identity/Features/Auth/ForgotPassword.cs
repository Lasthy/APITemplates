using Identity.Contracts.Events;
using Identity.Services;
using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;

namespace Identity.Features.Auth;

/// <summary>
/// Command to initiate a password reset for the specified email address.
/// Generates a reset token and publishes <see cref="PasswordResetRequestedEvent"/>.
/// </summary>
public record ForgotPassword(string Email) : IRequest<bool>;

/// <summary>
/// Handles <see cref="ForgotPassword"/> by generating a password reset token
/// and publishing a <see cref="PasswordResetRequestedEvent"/>.
/// Always returns success to prevent email enumeration.
/// </summary>
[Logging(Order = 1)]
public class ForgotPasswordHandler : IRequestHandler<ForgotPassword, bool>
{
    private readonly IUserService _userService;
    private readonly IEventBus _eventBus;

    public ForgotPasswordHandler(IUserService userService, IEventBus eventBus)
    {
        _userService = userService;
        _eventBus = eventBus;
    }

    public async Task<Result<bool>> HandleAsync(ForgotPassword request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.GeneratePasswordResetTokenAsync(request.Email, cancellationToken);

        if (result.IsSuccess && result.Value.HasValue)
        {
            var (user, token) = result.Value.Value;
            await _eventBus.PublishAsync(
                new PasswordResetRequestedEvent(user.Id, request.Email, token),
                cancellationToken);
        }

        // Always return success to avoid leaking whether the email exists
        return Result.Success(true);
    }
}
