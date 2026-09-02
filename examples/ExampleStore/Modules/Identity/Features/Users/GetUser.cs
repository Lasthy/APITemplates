using Identity.Contracts.Dtos;
using Identity.Entities;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features.Users;

/// <summary>
/// Query to get a single user's full details by ID (admin or self).
/// </summary>
public record GetUser(DefaultId UserId) : IRequest<UserDetailDto>;

/// <summary>
/// Handles <see cref="GetUser"/> by loading user details from Identity.
/// </summary>
[Logging(Order = 1)]
public class GetUserHandler : IRequestHandler<GetUser, UserDetailDto>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetUserHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<UserDetailDto>> HandleAsync(GetUser request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return Result.Failure<UserDetailDto>(
                new InvalidOperationException($"User '{request.UserId}' not found."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);

        var claimsDict = claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value)
            .AsReadOnly();

        return Result.Success(new UserDetailDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            UserName: user.UserName ?? string.Empty,
            ProfileName: user.ProfileName,
            Roles: roles.ToList().AsReadOnly(),
            Claims: claimsDict,
            IsBanned: user.IsBanned,
            IsDummy: user.IsDummy,
            PreferencesJson: user.PreferencesJson,
            CreatedAt: user.CreatedAt,
            DummyExpiresAt: user.DummyExpiresAt));
    }
}
