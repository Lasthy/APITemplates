using Identity.Contracts.Dtos;
using Identity.Profiles;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.Profiles;

/// <summary>
/// Query to get detailed information about a specific profile.
/// </summary>
public record GetProfile(string Name) : IRequest<ProfileDetailDto>;

/// <summary>
/// Handles <see cref="GetProfile"/> by looking up the profile in the <see cref="ProfileRegistry"/>.
/// </summary>
[Logging(Order = 1)]
public class GetProfileHandler : IRequestHandler<GetProfile, ProfileDetailDto>
{
    private readonly ProfileRegistry _profileRegistry;

    public GetProfileHandler(ProfileRegistry profileRegistry)
    {
        _profileRegistry = profileRegistry;
    }

    public Task<Result<ProfileDetailDto>> HandleAsync(GetProfile request, CancellationToken cancellationToken = default)
    {
        var profile = _profileRegistry.Get(request.Name);
        if (profile == null)
        {
            return Task.FromResult(Result.Failure<ProfileDetailDto>(
                new InvalidOperationException($"Profile '{request.Name}' not found.")));
        }

        var dto = new ProfileDetailDto(
            Name: profile.Name,
            Description: profile.Description,
            IsPublic: profile.IsPublic,
            Roles: profile.Roles,
            Claims: profile.Claims,
            PreferencesTypeName: profile.PreferencesType?.FullName,
            Requirements: profile.CreationRequirements.Select(r => r.Description).ToList());

        return Task.FromResult(Result.Success(dto));
    }
}
