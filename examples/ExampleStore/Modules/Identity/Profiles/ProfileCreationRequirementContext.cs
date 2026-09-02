using Kernel.Context;

namespace Identity.Profiles;

/// <summary>
/// Context passed to <see cref="IProfileCreationRequirement.EvaluateAsync"/> when evaluating
/// whether a user can be created with a specific profile.
/// </summary>
public class ProfileCreationRequirementContext
{
    /// <summary>
    /// The current caller's ambient scope context (authentication, roles, claims, correlation).
    /// </summary>
    public required IScopeContext ScopeContext { get; init; }

    /// <summary>
    /// The target profile descriptor being created.
    /// </summary>
    public required ProfileDescriptor Profile { get; init; }

    /// <summary>
    /// The email address provided in the user registration request, if available.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The username provided in the user registration request, if available.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// The scoped service provider for resolving dependencies during evaluation.
    /// </summary>
    public IServiceProvider? Services { get; init; }

    /// <summary>
    /// Optional additional contextual data or metadata associated with the creation attempt.
    /// </summary>
    public IReadOnlyDictionary<string, object>? AdditionalData { get; init; }
}
