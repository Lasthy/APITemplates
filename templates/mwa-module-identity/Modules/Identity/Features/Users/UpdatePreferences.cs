using Identity.Entities;
using Identity.Profiles;
using Kernel.Common;
using Kernel.Json;
using Kernel.Requests;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features.Users;

/// <summary>
/// Command to update a user's preferences JSON blob.
/// </summary>
public record UpdatePreferences(DefaultId UserId, string PreferencesJson) : IRequest<bool>;

/// <summary>
/// Handles <see cref="UpdatePreferences"/> by validating the JSON against the profile's
/// preferences type (if specified) and persisting it on the user entity.
/// </summary>
[Logging(Order = 1)]
public class UpdatePreferencesHandler : IRequestHandler<UpdatePreferences, bool>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ProfileRegistry _profileRegistry;
    private readonly IJsonSerializer _jsonSerializer;

    public UpdatePreferencesHandler(
        UserManager<ApplicationUser> userManager,
        ProfileRegistry profileRegistry,
        IJsonSerializer jsonSerializer)
    {
        _userManager = userManager;
        _profileRegistry = profileRegistry;
        _jsonSerializer = jsonSerializer;
    }

    public async Task<Result<bool>> HandleAsync(UpdatePreferences request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return Result.Failure<bool>(
                new InvalidOperationException($"User '{request.UserId}' not found."));
        }

        // Validate preferences JSON against profile's PreferencesType if specified
        var profile = _profileRegistry.Get(user.ProfileName);
        if (profile?.PreferencesType != null)
        {
            try
            {
                _jsonSerializer.Deserialize(request.PreferencesJson, profile.PreferencesType);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool>(
                    new InvalidOperationException(
                        $"Preferences JSON does not match expected schema '{profile.PreferencesType.Name}': {ex.Message}"));
            }
        }

        user.PreferencesJson = request.PreferencesJson;
        await _userManager.UpdateAsync(user);

        return Result.Success(true);
    }
}
