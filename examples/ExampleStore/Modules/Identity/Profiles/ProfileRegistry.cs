namespace Identity.Profiles;

/// <summary>
/// Immutable descriptor representing a registered profile.
/// Built from a concrete <see cref="IProfile"/> implementation at startup.
/// </summary>
public record ProfileDescriptor
{
    /// <summary>Unique profile identifier.</summary>
    public string Name { get; init; }

    /// <summary>Human-readable description.</summary>
    public string Description { get; init; }

    /// <summary>Whether unauthenticated users can self-register with this profile.</summary>
    public bool IsPublic { get; init; }

    /// <summary>Roles assigned to users created with this profile.</summary>
    public IReadOnlyList<string> Roles { get; init; }

    /// <summary>Additional claims assigned to users created with this profile.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; init; }

    /// <summary>Optional CLR type for preferences schema validation.</summary>
    public Type? PreferencesType { get; init; }

    /// <summary>The creation hook delegate.</summary>
    public Func<ProfileCreationContext, CancellationToken, Task> OnUserCreated { get; init; }

    /// <summary>Whether users created with this profile are permitted to create and manage API keys.</summary>
    public bool CanManageApiKeys { get; init; }

    /// <summary>Requirements that must be satisfied by the caller to create a user with this profile.</summary>
    public IReadOnlyList<IProfileCreationRequirement> CreationRequirements { get; init; }

    public ProfileDescriptor(
        string Name,
        string Description,
        bool IsPublic,
        IReadOnlyList<string> Roles,
        IReadOnlyDictionary<string, string> Claims,
        Type? PreferencesType,
        Func<ProfileCreationContext, CancellationToken, Task> OnUserCreated,
        bool CanManageApiKeys = false,
        IReadOnlyList<IProfileCreationRequirement>? CreationRequirements = null)
    {
        this.Name = Name;
        this.Description = Description;
        this.Roles = Roles;
        this.Claims = Claims;
        this.PreferencesType = PreferencesType;
        this.OnUserCreated = OnUserCreated;
        this.CanManageApiKeys = CanManageApiKeys;

        if (CreationRequirements != null && CreationRequirements.Count > 0)
        {
            this.CreationRequirements = CreationRequirements;
            this.IsPublic = CreationRequirements.Any(r => r.IsPublic);
        }
        else
        {
            this.IsPublic = IsPublic;
            this.CreationRequirements = IsPublic
                ? [ProfileRequirements.Public]
                : [ProfileRequirements.RequireSystem()];
        }
    }
}

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
    /// Checks whether the specified profile exists and permits API key management.
    /// </summary>
    public bool CanManageApiKeys(string name) =>
        _profiles.TryGetValue(name, out var p) && p.CanManageApiKeys;

    /// <summary>
    /// Gets all creation requirements for the specified profile, or empty if not found.
    /// </summary>
    public IReadOnlyList<IProfileCreationRequirement> GetCreationRequirements(string name) =>
        _profiles.TryGetValue(name, out var p) ? p.CreationRequirements : [];

    /// <summary>
    /// Gets all unique role names declared across all registered profiles.
    /// </summary>
    public IReadOnlySet<string> GetAllDeclaredRoles() =>
        _profiles.Values
            .SelectMany(p => p.Roles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
