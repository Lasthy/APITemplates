using Identity.Contracts.Events;
using Identity.Services;
using Kernel.Common;
using Kernel.Events;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to ban a user (admin only).
/// </summary>
public record BanUser(DefaultId UserId, DefaultId BannedBy, string? Reason) : IRequest<bool>;

/// <summary>
/// Handles <see cref="BanUser"/> by banning the user and publishing a <see cref="UserBannedEvent"/>.
/// </summary>
[Logging(Order = 1)]
[Transaction(Order = 2)]
public class BanUserHandler : IRequestHandler<BanUser, bool>
{
    private readonly IUserService _userService;
    private readonly IEventBus _eventBus;

    public BanUserHandler(IUserService userService, IEventBus eventBus)
    {
        _userService = userService;
        _eventBus = eventBus;
    }

    public async Task<Result<bool>> HandleAsync(BanUser request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.BanUserAsync(request.UserId, request.BannedBy, request.Reason, cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<bool>(result.Error!);
        }

        await _eventBus.PublishAsync(
            new UserBannedEvent(request.UserId, request.BannedBy, request.Reason),
            cancellationToken);

        return Result.Success(true);
    }
}
