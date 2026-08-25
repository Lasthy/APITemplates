using Identity.Contracts.Dtos;
using Kernel.Requests;

namespace Identity.Features.Profiles;

/// <summary>
/// Query to get detailed information about a specific profile.
/// </summary>
public record GetProfile(string Name) : IRequest<ProfileDetailDto>;
