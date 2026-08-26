namespace Identity.Profiles;

/// <summary>
/// Immutable descriptor representing a registered profile.
/// Built from a concrete <see cref="IProfile"/> implementation at startup.
/// </summary>
/// <param name="Name">Unique profile identifier.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="IsPublic">Whether unauthenticated users can self-register with this profile.</param>
/// <param name="Roles">Roles assigned to users created with this profile.</param>
/// <param name="Claims">Additional claims assigned to users created with this profile.</param>
/// <param name="PreferencesType">Optional CLR type for preferences schema validation.</param>
/// <param name="OnUserCreated">The creation hook delegate.</param>
public record ProfileDescriptor(
    string Name,
    string Description,
    bool IsPublic,
    IReadOnlyList<string> Roles,
    IReadOnlyDictionary<string, string> Claims,
    Type? PreferencesType,
    Func<ProfileCreationContext, CancellationToken, Task> OnUserCreated);

/// <summary>
/// Singleton registry of all discovered <see cref="IProfile"/> implementations.
/// Populated at module startup via assembly scanning.
/// </summary>
public class ProfileRegistry
{
    private readonly Dictionary<string, ProfileDescriptor> _profiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a profile descriptor.
    /// </summary>
    public void Register(ProfileDescriptor descriptor) =>
        _profiles[descriptor.Name] = descriptor;

    /// <summary>
    /// Gets a profile descriptor by name, or <c>null</c> if not found.
    /// </summary>
    public ProfileDescriptor? Get(string name) =>
        _profiles.GetValueOrDefault(name);

    /// <summary>
    /// Gets all registered profile descriptors.
    /// </summary>
    public IReadOnlyCollection<ProfileDescriptor> GetAll() =>
        _profiles.Values;

    /// <summary>
    /// Checks whether the specified profile exists and is public.
    /// </summary>
    public bool IsPublic(string name) =>
        _profiles.TryGetValue(name, out var p) && p.IsPublic;

    /// <summary>
    /// Gets all unique role names declared across all registered profiles.
    /// </summary>
    public IReadOnlySet<string> GetAllDeclaredRoles() =>
        _profiles.Values
            .SelectMany(p => p.Roles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
