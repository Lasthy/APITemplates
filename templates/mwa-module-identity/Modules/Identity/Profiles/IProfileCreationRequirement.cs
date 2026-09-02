using Kernel.Common;

namespace Identity.Profiles;

/// <summary>
/// Defines a requirement that must be satisfied by the caller to create a user with a specific profile.
/// </summary>
public interface IProfileCreationRequirement
{
    /// <summary>
    /// Indicates whether this requirement permits unauthenticated (public) callers to create a user.
    /// Defaults to <c>false</c>.
    /// </summary>
    bool IsPublic => false;

    /// <summary>
    /// Human-readable description of this requirement.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Evaluates whether the caller in <paramref name="context"/> is authorized to create a user with this profile.
    /// Returns <see cref="Result.Success"/> if permitted, or <see cref="Result.Failure"/> with the reason.
    /// </summary>
    Task<Result> EvaluateAsync(ProfileCreationRequirementContext context, CancellationToken ct = default);
}
