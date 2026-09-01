using Identity.Contracts.Events;
using Identity.Services;
using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to unban a user (admin only).
/// </summary>
public record UnbanUser(DefaultId UserId, DefaultId UnbannedBy) : IRequest<bool>;

/// <summary>
/// Handles <see cref="UnbanUser"/> by lifting the ban and publishing a <see cref="UserUnbannedEvent"/>.
/// </summary>
[Logging(Order = 1)]
public class UnbanUserHandler : IRequestHandler<UnbanUser, bool>
{
    private readonly IUserService _userService;
    private readonly IEventBus _eventBus;

    public UnbanUserHandler(IUserService userService, IEventBus eventBus)
    {
        _userService = userService;
        _eventBus = eventBus;
    }

    public async Task<Result<bool>> HandleAsync(UnbanUser request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.UnbanUserAsync(request.UserId, cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        await _eventBus.PublishAsync(
            new UserUnbannedEvent(request.UserId, request.UnbannedBy),
            cancellationToken);

        return Result.Success(true);
    }
}
