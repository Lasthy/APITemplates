using Identity.Contracts.Requests;
using Identity.Entities;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features;

/// <summary>
/// Handles cross-module <see cref="ValidateUserAccess"/> requests.
/// Returns <c>true</c> if the user exists, is not banned, and holds the required role.
/// </summary>
[Logging(Order = 1)]
public class ValidateUserAccessHandler : IRequestHandler<ValidateUserAccess, bool>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ValidateUserAccessHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<bool>> HandleAsync(ValidateUserAccess request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null || user.IsBanned)
        {
            return Result.Success(false);
        }

        var isInRole = await _userManager.IsInRoleAsync(user, request.RequiredRole);
        return Result.Success(isInRole);
    }
}
