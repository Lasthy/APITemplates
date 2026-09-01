using Identity.Contracts.Dtos;
using Identity.Profiles;
using Kernel.Common;
using Kernel.Requests;

namespace Identity.Features.Profiles;

/// <summary>
/// Query to list all registered profiles.
/// Anonymous users see only public profiles; admin sees all.
/// </summary>
public record GetProfiles(bool IncludeNonPublic = false) : IRequest<IEnumerable<ProfileDto>>;

/// <summary>
/// Handles <see cref="GetProfiles"/> by returning profiles from the <see cref="ProfileRegistry"/>.
/// </summary>
[Logging(Order = 1)]
public class GetProfilesHandler : IRequestHandler<GetProfiles, IEnumerable<ProfileDto>>
{
    private readonly ProfileRegistry _profileRegistry;

    public GetProfilesHandler(ProfileRegistry profileRegistry)
    {
        _profileRegistry = profileRegistry;
    }

    public Task<Result<IEnumerable<ProfileDto>>> HandleAsync(GetProfiles request, CancellationToken cancellationToken = default)
    {
        var profiles = _profileRegistry.GetAll()
            .Where(p => request.IncludeNonPublic || p.IsPublic)
            .Select(p => new ProfileDto(p.Name, p.Description, p.IsPublic));

        return Task.FromResult(Result.Success(profiles));
    }
}
