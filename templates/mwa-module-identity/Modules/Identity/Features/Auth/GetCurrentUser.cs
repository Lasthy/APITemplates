using Identity.Contracts.Dtos;
using Identity.Entities;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features.Auth;

/// <summary>
/// Query for the currently authenticated user's details.
/// </summary>
public record GetCurrentUser(DefaultId UserId) : IRequest<UserDetailDto>;

/// <summary>
/// Handles <see cref="GetCurrentUser"/> requests by loading the user from Identity.
/// </summary>
[Logging(Order = 1)]
public class GetCurrentUserHandler : IRequestHandler<GetCurrentUser, UserDetailDto>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetCurrentUserHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<UserDetailDto>> HandleAsync(GetCurrentUser request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return Result.Failure<UserDetailDto>(
                new InvalidOperationException("User not found."));
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
