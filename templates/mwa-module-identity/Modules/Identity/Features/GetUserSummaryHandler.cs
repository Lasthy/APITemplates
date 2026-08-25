using Identity.Contracts.Dtos;
using Identity.Contracts.Requests;
using Identity.Entities;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features;

/// <summary>
/// Handles cross-module <see cref="GetUserSummary"/> requests.
/// </summary>
[Logging(Order = 1)]
public class GetUserSummaryHandler : IRequestHandler<GetUserSummary, UserDto>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetUserSummaryHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<UserDto>> HandleAsync(GetUserSummary request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return Result.Failure<UserDto>(
                new InvalidOperationException($"User '{request.UserId}' not found."));
        }

        return Result.Success(new UserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            UserName: user.UserName ?? string.Empty,
            ProfileName: user.ProfileName,
            IsBanned: user.IsBanned,
            IsDummy: user.IsDummy,
            CreatedAt: user.CreatedAt));
    }
}
