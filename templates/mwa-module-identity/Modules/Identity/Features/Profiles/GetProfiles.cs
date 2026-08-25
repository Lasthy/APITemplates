using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Profiles;

/// <summary>
/// Query to list all registered profiles.
/// Anonymous users see only public profiles; admin sees all.
/// </summary>
public record GetProfiles(bool IncludeNonPublic = false) : IRequest<IEnumerable<ProfileDto>>;
