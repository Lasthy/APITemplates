using Identity.Entities;
using Kernel.Common;
using Kernel.Requests;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features.Users;

/// <summary>
/// Command to update a user's email and/or username.
/// </summary>
public record UpdateUser(DefaultId UserId, string? Email, string? UserName) : IRequest<bool>;

/// <summary>
/// Handles <see cref="UpdateUser"/> by updating user email and/or username.
/// </summary>
[Logging(Order = 1)]
public class UpdateUserHandler : IRequestHandler<UpdateUser, bool>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateUserHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<bool>> HandleAsync(UpdateUser request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return Result.Failure<bool>(
                new InvalidOperationException($"User '{request.UserId}' not found."));
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var emailResult = await _userManager.SetEmailAsync(user, request.Email);
            if (!emailResult.Succeeded)
            {
                var errors = string.Join("; ", emailResult.Errors.Select(e => e.Description));
                return Result.Failure<bool>(new InvalidOperationException($"Failed to update email: {errors}"));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.UserName) && request.UserName != user.UserName)
        {
            var nameResult = await _userManager.SetUserNameAsync(user, request.UserName);
            if (!nameResult.Succeeded)
            {
                var errors = string.Join("; ", nameResult.Errors.Select(e => e.Description));
                return Result.Failure<bool>(new InvalidOperationException($"Failed to update username: {errors}"));
            }
        }

        return Result.Success(true);
    }
}
