using Identity.Services;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.Users;

/// <summary>
/// Command to purge all expired dummy users and their associated tokens/keys (System role only).
/// Returns the number of purged dummy users.
/// </summary>
public record PurgeExpiredDummyUsers : IRequest<int>;

/// <summary>
/// Handles <see cref="PurgeExpiredDummyUsers"/> by deleting all expired dummy users.
/// </summary>
[Logging(Order = 1)]
public class PurgeExpiredDummyUsersHandler : IRequestHandler<PurgeExpiredDummyUsers, int>
{
    private readonly IUserService _userService;

    public PurgeExpiredDummyUsersHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<Result<int>> HandleAsync(PurgeExpiredDummyUsers request, CancellationToken cancellationToken = default)
    {
        return await _userService.PurgeExpiredDummyUsersAsync(cancellationToken);
    }
}
